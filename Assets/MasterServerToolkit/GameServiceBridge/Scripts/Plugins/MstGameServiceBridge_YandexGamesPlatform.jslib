// ===== Yandex Games Platform Bridge (.jslib) =====
// Unity WebGL / Emscripten-compatible library.
// Uses the global helper from WebGLGameBridge: `$GB_Mst_Util`.
// `$GB_Yg_helpers` remains opt-in via __deps (only where needed).

var LibraryYandexGamesPlatform = {
	// ---------------------- Yandex-specific helper(s) ---------------------------
	/**
	* Initialize Yandex Player object and send a lightweight profile to Unity.
	* Sends MST_GAME_BRIDGE.Yg_OnGetPlayer(JSON<string>) on success or fallback.
	*/
	$GB_Yg_helpers: {
		send: function (method, payload) {
			unityInstance.SendMessage('MST_GAME_BRIDGE', method, JSON.stringify(payload || {}));
		},

		sendError: function (method, error) {
			var e = GB_Mst_Util.toErrorString(error) || 'unknown_error';
			GB_Mst_Util.error(method + ' failed. Error: ' + e);
			GB_Yg_helpers.send(method, { error: e });
		},

		getPayments: function (onReady, onError) {
			if (!window.ysdk) {
				if (onError) onError('Yandex SDK is not initialized');
				return;
			}

			if (window.ysdkPayments) {
				onReady(window.ysdkPayments);
				return;
			}

			window.ysdk.getPayments({ signed: true })
				.then(function (payments) {
					window.ysdkPayments = payments;
					onReady(payments);
				})
				.catch(function (error) {
					if (onError) onError(error);
				});
		},

		initPlayer: function () {
		var player = { id: '', is_guest: true, name: '', avatar: '', extra: {} };

		if (!window.ysdk) {
			GB_Yg_helpers.send('Yg_OnGetPlayer', { error: 'Yandex SDK is not initialized' });
			return;
		}

		window.ysdk.getPlayer({ signed: true })
			.then(function (_player) {
				window.player = _player;
				player.id = _player.getUniqueID();
				player.is_guest = (_player.isAuthorized() === false);
				player.name = _player.getName();
				player.avatar = _player.getPhoto('large');
				player.extra = {
					payingStatus: _player.getPayingStatus(),
					signature: _player.signature || ''
				};

				GB_Yg_helpers.send('Yg_OnGetPlayer', player);
			})
			.catch(function (error) {
				GB_Yg_helpers.sendError('Yg_OnGetPlayer', error);
			});
		}
	},

	// ---------------------- SDK bootstrap --------------------------------------
	/**
	* Load the Yandex Games SDK script (if not already on the page) and initialize it.
	* Hook pause/resume events and expose window.ysdk for subsequent calls.
	* C#: extern void Gb_Yg_initSdk();
	*/
	Gb_Yg_initSdk__proxy: 'sync',
	Gb_Yg_initSdk__sig: 'v',
	Gb_Yg_initSdk: function () {
		function bindSdkEvents(ysdkInstance) {
			if (!ysdkInstance || window.GB_Yg_eventsBound || !ysdkInstance.on) {
				return;
			}

			function bindEvent(eventName, callback, optional) {
				if (!eventName) {
					return;
				}

				try {
					ysdkInstance.on(eventName, callback);
				} catch (error) {
					if (!optional) {
						GB_Mst_Util.error('Could not bind Yandex event ' + eventName + '. Error: ' + GB_Mst_Util.toErrorString(error));
					}
				}
			}

			var events = ysdkInstance.EVENTS || {};

			bindEvent('game_api_pause', function () {
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGameApiPause', 1);
			}, false);

			bindEvent('game_api_resume', function () {
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGameApiPause', 0);
			}, false);

			bindEvent(events.ACCOUNT_SELECTION_DIALOG_OPENED || 'ACCOUNT_SELECTION_DIALOG_OPENED', function () {
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnAccountSelectionDialog', 1);
			}, true);

			bindEvent(events.ACCOUNT_SELECTION_DIALOG_CLOSED || 'ACCOUNT_SELECTION_DIALOG_CLOSED', function () {
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnAccountSelectionDialog', 0);
			}, true);

			window.GB_Yg_eventsBound = true;
		}

		if (window.ysdk) {
			bindSdkEvents(window.ysdk);
			return;
		}

		if (window.GB_Yg_initStarted) {
			return;
		}

		window.GB_Yg_initStarted = true;

		function initSdk () {
			YaGames.init({ signed: true })
				.then(function (ysdkInstance) {
					window.ysdk = ysdkInstance;

					GB_Mst_Util.log('YG SDK initialized; env=' + JSON.stringify(ysdkInstance.environment || {}));
					bindSdkEvents(window.ysdk);
				})
				.catch(function (error) {
					var e = GB_Mst_Util.toErrorString(error);
					GB_Mst_Util.error('YG SDK initialize failed. Error: ' + e);
					window.GB_Yg_initStarted = false;
				});
		}

		if (typeof YaGames !== 'undefined') {
			initSdk();
			return;
		}

		var s = document.createElement('script');
		s.src = '/sdk.js';
		s.async = true;
		s.onload = initSdk;
		s.onerror = function (error) {
			var e = GB_Mst_Util.toErrorString(error);
			GB_Mst_Util.error('Failed to load YG SDK from ' + s.src + ', Error: ' + e);
			window.GB_Yg_initStarted = false;
		};

		document.body.appendChild(s);
	},

	// ---------------------- Loading API ----------------------------------------
	/**
	* Mark game loading complete in Yandex LoadingAPI (if available).
	* C#: extern void Gb_Yg_SetApiReady();
	*/
	Gb_Yg_SetApiReady__proxy: 'sync',
	Gb_Yg_SetApiReady__sig: 'v',
	Gb_Yg_SetApiReady: function () {
		if (window.ysdk && window.ysdk.features && window.ysdk.features.LoadingAPI) {
			window.ysdk.features.LoadingAPI.ready();
		}
	},

	// ---------------------- Environment / Device / Readiness / Time ------------
	/**
	* Get environment info (app id, i18n, payload) as JSON string.
	* Returns char* (UTF8) pointer.
	* C#: extern IntPtr Gb_Yg_Environment();
	*/
	Gb_Yg_Environment__proxy: 'sync',
	Gb_Yg_Environment__sig: 'i',
	Gb_Yg_Environment: function () {
		var e = window.ysdk && window.ysdk.environment ? window.ysdk.environment : {};
		var env = {
			app:   { id: e.app && e.app.id ? e.app.id : '' },
			i18n:  {
				lang: e.i18n && e.i18n.lang ? e.i18n.lang : '',
				tld: e.i18n && e.i18n.tld ? e.i18n.tld : ''
			},
			payload: e.payload || '',
			referrer: e.referrer || null
		};

		var json = JSON.stringify(env);
		var n = lengthBytesUTF8(json) + 1;
		var buf = _malloc(n);
		stringToUTF8(json, buf, n);
		return buf;
	},

	/**
	* Get device info as JSON string (or string).
	* Returns char* (UTF8) pointer.
	* C#: extern IntPtr Gb_Yg_Device();
	*/
	Gb_Yg_Device__proxy: 'sync',
	Gb_Yg_Device__sig: 'i',
	Gb_Yg_Device: function () {
		var info = null;
		var device = 'Desktop';

		if (window.ysdk && window.ysdk.deviceInfo) {
			try {
				info = (typeof window.ysdk.deviceInfo === 'function')
					? window.ysdk.deviceInfo()
					: window.ysdk.deviceInfo;
			} catch (error) {
				GB_Mst_Util.error('Could not read Yandex device info. Error: ' + GB_Mst_Util.toErrorString(error));
			}
		}

		if (info) {
			var type = (typeof info.type === 'string') ? info.type.toLowerCase() : '';
			if (!type && typeof info.isMobile === 'function' && info.isMobile()) type = 'mobile';
			else if (!type && typeof info.isTablet === 'function' && info.isTablet()) type = 'tablet';
			else if (!type && typeof info.isTV === 'function' && info.isTV()) type = 'tv';
			else if (!type && typeof info.isDesktop === 'function' && info.isDesktop()) type = 'desktop';

			if (type === 'mobile') device = 'Mobile';
			else if (type === 'tablet') device = 'Tablet';
			else if (type === 'tv') device = 'TV';
		}

		var bufferSize = lengthBytesUTF8(device) + 1;
		var buffer = _malloc(bufferSize);
		stringToUTF8(device, buffer, bufferSize);
		return buffer;
	},

	/**
	* Check if SDK is initialized (1 ready, 0 otherwise).
	* C#: extern int Gb_Yg_isReady();
	*/
	Gb_Yg_isReady__proxy: 'sync',
	Gb_Yg_isReady__sig: 'i',
	Gb_Yg_isReady: function () { return window.ysdk ? 1 : 0; },

	/**
	* Get server time (ms since epoch) as double to avoid 32-bit overflow.
	* C#: extern double Gb_Yg_serverTime();
	*/
	Gb_Yg_serverTime__proxy: 'sync',
	Gb_Yg_serverTime__sig: 'd',
	Gb_Yg_serverTime: function() {
		return window.ysdk && window.ysdk.serverTime ? window.ysdk.serverTime() : Date.now();
	},

	/**
	* Fetch Yandex remote config flags once and forward them to Unity.
	* Input JSON: defaultFlags object and clientFeatures array.
	* Sends MST_GAME_BRIDGE.Yg_OnGetRemoteFlags(JSON<string>).
	* C#: extern void Gb_Yg_GetRemoteFlags(string defaultFlagsJson, string clientFeaturesJson);
	*/
	Gb_Yg_GetRemoteFlags__deps: ['$GB_Yg_helpers'],
	Gb_Yg_GetRemoteFlags__proxy: 'sync',
	Gb_Yg_GetRemoteFlags__sig: 'vii',
	Gb_Yg_GetRemoteFlags: function(defaultFlagsJson, clientFeaturesJson) {
		if (!window.ysdk || !window.ysdk.getFlags) {
			GB_Yg_helpers.send('Yg_OnGetRemoteFlags', { error: 'Yandex remote config is not supported' });
			return;
		}

		function parseJson(pointer, fallback) {
			var text = pointer ? UTF8ToString(pointer) : '';

			if (!text) {
				return fallback;
			}

			try {
				return JSON.parse(text);
			} catch (error) {
				GB_Mst_Util.error('Could not parse Yandex remote config argument. Error: ' + GB_Mst_Util.toErrorString(error));
				return fallback;
			}
		}

		var defaultFlags = parseJson(defaultFlagsJson, {});
		var clientFeatures = parseJson(clientFeaturesJson, []);
		var options = {};

		if (defaultFlags && typeof defaultFlags === 'object' && !Array.isArray(defaultFlags) && Object.keys(defaultFlags).length > 0) {
			options.defaultFlags = defaultFlags;
		}

		if (Array.isArray(clientFeatures) && clientFeatures.length > 0) {
			options.clientFeatures = clientFeatures;
		}

		try {
			var result = window.ysdk.getFlags(options);

			if (result && typeof result.then === 'function') {
				result
					.then(function (flags) {
						GB_Yg_helpers.send('Yg_OnGetRemoteFlags', flags || {});
					})
					.catch(function (error) {
						GB_Yg_helpers.sendError('Yg_OnGetRemoteFlags', error);
					});
			} else {
				GB_Yg_helpers.send('Yg_OnGetRemoteFlags', result || {});
			}
		} catch (error) {
			GB_Yg_helpers.sendError('Yg_OnGetRemoteFlags', error);
		}
	},

	// ---------------------- Gameplay API ---------------------------------------
	/**
	* Notify Yandex SDK that gameplay has started.
	* C#: extern void Gb_Yg_GameStart();
	*/
	Gb_Yg_GameStart__proxy: 'sync',
	Gb_Yg_GameStart__sig: 'v',
	Gb_Yg_GameStart: function () {
		if (window.ysdk && window.ysdk.features && window.ysdk.features.GameplayAPI) {
			window.ysdk.features.GameplayAPI.start();
		}
	},

	/**
	* Notify Yandex SDK that gameplay has stopped/paused.
	* C#: extern void Gb_Yg_GameStop();
	*/
	Gb_Yg_GameStop__proxy: 'sync',
	Gb_Yg_GameStop__sig: 'v',
	Gb_Yg_GameStop: function () {
		if (window.ysdk && window.ysdk.features && window.ysdk.features.GameplayAPI) {
			window.ysdk.features.GameplayAPI.stop();
		}
	},

	// ---------------------- Player: profile, auth, data ------------------------
	/**
	* Fetch minimal player profile and send it to Unity.
	* Sends MST_GAME_BRIDGE.Yg_OnGetPlayer(JSON<string>).
	* C#: extern void Gb_Yg_GetPlayer();
	*/
	Gb_Yg_GetPlayer__deps: ['$GB_Yg_helpers'],
	Gb_Yg_GetPlayer__proxy: 'sync',
	Gb_Yg_GetPlayer__sig: 'v',
	Gb_Yg_GetPlayer: function () { GB_Yg_helpers.initPlayer(); },

	/**
	* Trigger Yandex auth dialog if the player is a guest; return result to Unity.
	* Sends MST_GAME_BRIDGE.Yg_OnAuthPlayer(JSON<string>).
	* C#: extern void Gb_Yg_AuthPlayer();
	*/
	Gb_Yg_AuthPlayer__proxy: 'sync',
	Gb_Yg_AuthPlayer__sig: 'v',
	Gb_Yg_AuthPlayer: function() {
		var result = {
			success: false,
			error: ''
		}

		if (!window.ysdk) {
			result.error = 'Yandex SDK is not initialized';
			GB_Yg_helpers.send('Yg_OnAuthPlayer', result);
			return;
		}

		if (!window.player) {
			GB_Yg_helpers.initPlayer();
			result.error = 'Yandex player is not initialized';
			GB_Yg_helpers.send('Yg_OnAuthPlayer', result);
			return;
		}

		if (window.player.isAuthorized() === false) {
            window.ysdk.auth.openAuthDialog()
			.then(() => {
				result.success = true;
				GB_Yg_helpers.send('Yg_OnAuthPlayer', result);
			})
			.catch(error => {
				var e = GB_Mst_Util.toErrorString(error);
				result.error = e;
				GB_Mst_Util.error('Could not auth Yandex Games player. Error: ' + e);
				GB_Yg_helpers.send('Yg_OnAuthPlayer', result);
			});
		} else {
			result.success = true;
			GB_Yg_helpers.send('Yg_OnAuthPlayer', result);
		}
	},

	/**
	* Read arbitrary player data from Yandex cloud and forward to Unity as JSON.
	* Sends MST_GAME_BRIDGE.Yg_OnPlayerGetData(JSON<string>).
	* C#: extern void Gb_Yg_GetPlayerData();
	*/
	Gb_Yg_GetPlayerData__proxy: 'sync',
	Gb_Yg_GetPlayerData__sig: 'v',
	Gb_Yg_GetPlayerData: function() {
		if (!window.player) {
			GB_Yg_helpers.send('Yg_OnPlayerGetData', { error: 'Yandex player is not initialized' });
			return;
		}

		window.player.getData()
		.then(data=> {
			GB_Yg_helpers.send('Yg_OnPlayerGetData', data);
		}).catch(error => {
			GB_Yg_helpers.sendError('Yg_OnPlayerGetData', error);
		});
	},

	/**
	* Write player data to Yandex cloud or stats API.
	* Input: JSON pointer (char*) and flag saveAsStats (int 0/1).
	* Sends MST_GAME_BRIDGE.Yg_OnPlayerSetData(JSON<string>).
	* C#: extern void Gb_Yg_SetPlayerData(IntPtr json, int saveAsStats);
	*/
	Gb_Yg_SetPlayerData__proxy: 'sync',
	Gb_Yg_SetPlayerData__sig: 'vii',
	Gb_Yg_SetPlayerData: function(data, saveAsStats) {
		var json = JSON.parse(UTF8ToString(data));

		if (!window.player) {
			GB_Yg_helpers.send('Yg_OnPlayerSetData', { success: false, error: 'Yandex player is not initialized' });
			return;
		}

		if(saveAsStats) {
			window.player.setStats(json)
			.then(() => {
				GB_Yg_helpers.send('Yg_OnPlayerSetData', { success: true, error: '' });
			})
			.catch(error => {
				var e = GB_Mst_Util.toErrorString(error);
				GB_Mst_Util.error('Could not set player stats data to yandex cloud. Error: ' + e);
				GB_Yg_helpers.send('Yg_OnPlayerSetData', { success: false, error: e });
			});
		}else{
			window.player.setData(json)
			.then(() => {
				GB_Yg_helpers.send('Yg_OnPlayerSetData', { success: true, error: '' });
			})
			.catch(error => {
				var e = GB_Mst_Util.toErrorString(error);
				GB_Mst_Util.error('Could not set player data to yandex cloud. Error: ' + e);
				GB_Yg_helpers.send('Yg_OnPlayerSetData', { success: false, error: e });
			});
		}
	},

	// ---------------------- Ads -------------------------------------------------
	/**
	* Show fullscreen ad; forward status to Unity via Yg_OnFullScreenVideoStatus.
	* C#: extern void Gb_Yg_ShowFullScreenAdv();
	*/
	Gb_Yg_ShowFullScreenAdv__proxy: 'sync',
	Gb_Yg_ShowFullScreenAdv__sig: 'v',
	Gb_Yg_ShowFullScreenAdv: function () {
		if (!window.ysdk || !window.ysdk.adv || !window.ysdk.adv.showFullscreenAdv) {
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnFullScreenVideoStatus', 'Error');
			return;
		}

		window.ysdk.adv.showFullscreenAdv({
			callbacks: {
				onOpen: function (wasShown) {
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnFullScreenVideoStatus', 'Opened');
				},
				onClose: function (wasShown) {
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnFullScreenVideoStatus', 'Closed');
				},
				onError: function (error) {
					var e = GB_Mst_Util.toErrorString(error);
					GB_Mst_Util.error('Could not show full screen video. Error: ' + e);
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnFullScreenVideoStatus', 'Error');
				}
			}
		});
	},

	/**
	* Show rewarded video; forward status to Unity via Yg_OnRewardedVideoStatus.
	* C#: extern void Gb_Yg_ShowRewardedVideo();
	*/
	Gb_Yg_ShowRewardedVideo__proxy: 'sync',
	Gb_Yg_ShowRewardedVideo__sig: 'v',
	Gb_Yg_ShowRewardedVideo: function() {
		if (!window.ysdk || !window.ysdk.adv || !window.ysdk.adv.showRewardedVideo) {
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnRewardedVideoStatus', 'Error');
			return;
		}

		window.ysdk.adv.showRewardedVideo({
			callbacks: {
				onOpen: () => {
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnRewardedVideoStatus', 'Opened');
				},
				onRewarded: () => {
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnRewardedVideoStatus', 'Rewarded');
				},
				onClose: () => {
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnRewardedVideoStatus', 'Closed');
				},
				onError: (error) => {
					var e = GB_Mst_Util.toErrorString(error);
					GB_Mst_Util.error('Could not show rewarded video. Error: ' + e);
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnRewardedVideoStatus', 'Error');
				}
			}
		})
	},

	/**
	* Show sticky banner and forward status to Unity via Yg_OnBannerAdvertisementStatus.
	* C#: extern void Gb_Yg_ShowStickyBanner();
	*/
	Gb_Yg_ShowStickyBanner__proxy: 'sync',
	Gb_Yg_ShowStickyBanner__sig: 'v',
	Gb_Yg_ShowStickyBanner: function() {
		if (!window.ysdk || !window.ysdk.adv || !window.ysdk.adv.showBannerAdv) {
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnBannerAdvertisementStatus', 'Error');
			return;
		}

		window.ysdk.adv.showBannerAdv()
		.then(function (result) {
			if (result && result.stickyAdvIsShowing) {
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnBannerAdvertisementStatus', 'Shown');
			} else {
				GB_Mst_Util.error('Could not show sticky banner. Reason: ' + ((result && result.reason) || 'unknown_error'));
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnBannerAdvertisementStatus', 'Error');
			}
		})
		.catch(function (error) {
			var e = GB_Mst_Util.toErrorString(error);
			GB_Mst_Util.error('Could not show sticky banner. Error: ' + e);
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnBannerAdvertisementStatus', 'Error');
		});
	},

	/**
	* Hide sticky banner and forward status to Unity via Yg_OnBannerAdvertisementStatus.
	* C#: extern void Gb_Yg_HideStickyBanner();
	*/
	Gb_Yg_HideStickyBanner__proxy: 'sync',
	Gb_Yg_HideStickyBanner__sig: 'v',
	Gb_Yg_HideStickyBanner: function() {
		if (!window.ysdk || !window.ysdk.adv || !window.ysdk.adv.hideBannerAdv) {
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnBannerAdvertisementStatus', 'Error');
			return;
		}

		window.ysdk.adv.hideBannerAdv()
		.then(function (result) {
			if (!result || result.stickyAdvIsShowing === false) {
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnBannerAdvertisementStatus', 'Hidden');
			} else {
				GB_Mst_Util.error('Could not hide sticky banner. Banner is still visible.');
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnBannerAdvertisementStatus', 'Error');
			}
		})
		.catch(function (error) {
			var e = GB_Mst_Util.toErrorString(error);
			GB_Mst_Util.error('Could not hide sticky banner. Error: ' + e);
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnBannerAdvertisementStatus', 'Error');
		});
	},

	// ---------------------- Leaderboards ---------------------------------------
	/**
	* Set score on a leaderboard (if method available in current environment).
	* Input JSON: { requestId, leaderboardName, score, extraData }
	* C#: extern void Gb_Yg_SetLeaderboardScore(IntPtr json);
	*/
	Gb_Yg_SetLeaderboardScore__proxy: 'sync',
	Gb_Yg_SetLeaderboardScore__sig: 'vi',
	Gb_Yg_SetLeaderboardScore: function(data) {
		var json = JSON.parse(UTF8ToString(data));
		Promise.resolve()
		.then(function() {
			return window.ysdk.isAvailableMethod('leaderboards.setScore');
		})
		.then(function(isAvailable) {
			if (isAvailable !== true) {
				throw new Error('leaderboards.setScore is not available');
			}

			return window.ysdk.leaderboards.setScore(
				json.leaderboardName,
				json.score,
				json.extraData);
		})
		.then(function() {
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnSetLeaderboardScore', JSON.stringify({
				requestId: json.requestId,
				success: true,
				error: ''
			}));
		})
		.catch(function(error) {
			var e = GB_Mst_Util.toErrorString(error);
			GB_Mst_Util.error('Could not set leaderboard score. Error: ' + e);
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnSetLeaderboardScore', JSON.stringify({
				requestId: json.requestId,
				success: false,
				error: e
			}));
		});
	},

	/**
	* Get leaderboard description and forward a compact projection to Unity.
	* C#: extern void Gb_Yg_GetLeaderboardDescription(IntPtr name);
	*/
	Gb_Yg_GetLeaderboardDescription__proxy: 'sync',
	Gb_Yg_GetLeaderboardDescription__sig: 'vi',
	Gb_Yg_GetLeaderboardDescription: function(name) {
		var leaderboardName = UTF8ToString(name);
		window.ysdk.leaderboards.getDescription(leaderboardName)
		.then(data => {
			var leaderboard = {
				appID: data.appID,
				default: data.default,
				description: {
					invert_sort_order: data.description.invert_sort_order,
					score_format: {
						options: {
							decimal_offset: data.description.score_format.options.decimal_offset
						},
						type: data.description.score_format.type
					}
				},
				name: data.name,
				title: data.title
			}

			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetLeaderboardDescription', JSON.stringify(leaderboard));
		})
		.catch(error => {
			var e = GB_Mst_Util.toErrorString(error);
			GB_Mst_Util.error('An error occurred while getting description of ' + leaderboardName + '. Error: ' + e);
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetLeaderboardDescription', JSON.stringify({ error: e }));
		});
	},

	/**
	* Get leaderboard entries with options and forward to Unity.
	* C#: extern void Gb_Yg_GetLeaderboardEntries(IntPtr name, IntPtr optionsJson);
	*/
	Gb_Yg_GetLeaderboardEntries__proxy: 'sync',
	Gb_Yg_GetLeaderboardEntries__sig: 'vii',
	Gb_Yg_GetLeaderboardEntries: function(name, options) {
		var leaderboardName = UTF8ToString(name);
		var leaderboardOptions = JSON.parse(UTF8ToString(options));
		window.ysdk.leaderboards.getEntries(leaderboardName, leaderboardOptions)
		.then(data => {
			var leaderboardEntries = {
				leaderboard: {
					appID: data.leaderboard.appID,
					default: data.leaderboard.default,
					description: data.leaderboard.description,
					name: data.leaderboard.name,
					title: data.leaderboard.title
				},
				ranges: data.ranges,
				userRank: data.userRank,
				entries: []
			}

			data.entries.forEach(e => {
				var leaderboardPlayerEntry = {
					score: e.score,
					extraData: e.extraData,
					rank: e.rank,
					player: {
						avatar: e.player.getAvatarSrc('medium'),
						lang: e.player.lang,
						publicName: e.player.publicName,
						scopePermissions: e.player.scopePermissions,
						uniqueID: e.player.uniqueID,
					},
					formattedScore: e.formattedScore
				}

				leaderboardEntries.entries.push(leaderboardPlayerEntry);
			})

			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetLeaderboardEntries', JSON.stringify(leaderboardEntries));
		})
		.catch(error => {
			var e = GB_Mst_Util.toErrorString(error);
			GB_Mst_Util.error('An error occurred while getting entries of ' + leaderboardName + '. Error: ' + e);
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetLeaderboardEntries', JSON.stringify({ error: e }));
		});
	},

	/**
	* Get current player's leaderboard entry (if available) and forward to Unity.
	* C#: extern void Gb_Yg_GetLeaderboardPlayerEntry(IntPtr name);
	*/
	Gb_Yg_GetLeaderboardPlayerEntry__proxy: 'sync',
	Gb_Yg_GetLeaderboardPlayerEntry__sig: 'vi',
	Gb_Yg_GetLeaderboardPlayerEntry: function(name) {
		var leaderboardName = UTF8ToString(name);
		window.ysdk.isAvailableMethod('leaderboards.getPlayerEntry')
		.then(isAvailable => {
			if (isAvailable === true) {
				window.ysdk.leaderboards.getPlayerEntry(leaderboardName)
				.then(data => {
					var leaderboardPlayerEntry = {
						score: data.score,
						extraData: data.extraData,
						rank: data.rank,
						player: {
							avatar: data.player.getAvatarSrc('medium'),
							lang: data.player.lang,
							publicName: data.player.publicName,
							scopePermissions: data.player.scopePermissions,
							uniqueID: data.player.uniqueID,
						},
						formattedScore: data.formattedScore
					}

					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetLeaderboardPlayerEntry', JSON.stringify(leaderboardPlayerEntry));
				}).catch(error => {
					var e = GB_Mst_Util.toErrorString(error);
					GB_Mst_Util.error('An error occurred while getting ' + leaderboardName + ' player entry. Error: ' + e);
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetLeaderboardPlayerEntry', JSON.stringify({ error: e }));
				});
			} else {
					var e = 'Leaderboards player entry not available';
					GB_Mst_Util.error('An error occurred while getting ' + leaderboardName + ' player entry. Error: ' + e);
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetLeaderboardPlayerEntry', JSON.stringify({ error: e }));
			}
		});
	},

	// ---------------------- Feedback -------------------------------------------
	/**
	* Check if Yandex review dialog can be shown.
	* Sends MST_GAME_BRIDGE.Yg_OnCanReview(JSON<string>).
	* C#: extern void Gb_Yg_CanReview();
	*/
	Gb_Yg_CanReview__proxy: 'sync',
	Gb_Yg_CanReview__sig: 'v',
	Gb_Yg_CanReview: function(){
		if (!window.ysdk || !window.ysdk.feedback) {
			GB_Yg_helpers.send('Yg_OnCanReview', { value: false, reason: 'unsupported' });
			return;
		}

		window.ysdk.feedback.canReview()
		.then(function (result) {
			GB_Yg_helpers.send('Yg_OnCanReview', result);
		})
		.catch(function (error) {
			GB_Yg_helpers.sendError('Yg_OnCanReview', error);
		});
	},

	/**
	* Try to open Yandex review dialog if allowed; logs reason otherwise.
	* Sends MST_GAME_BRIDGE.Yg_OnReviewGame(JSON<string>).
	* C#: extern void Gb_Yg_ReviewGame();
	*/
	Gb_Yg_ReviewGame__proxy: 'sync',
	Gb_Yg_ReviewGame__sig: 'v',
	Gb_Yg_ReviewGame: function(){
		if (!window.ysdk || !window.ysdk.feedback) {
			GB_Yg_helpers.send('Yg_OnReviewGame', { feedbackSent: false, error: 'unsupported' });
			return;
		}

		window.ysdk.feedback.canReview()
		.then(function (result) {
            if (result.value) {
                window.ysdk.feedback.requestReview()
				.then(function (reviewResult) {
					GB_Yg_helpers.send('Yg_OnReviewGame', reviewResult);
				})
				.catch(function (error) {
					GB_Yg_helpers.sendError('Yg_OnReviewGame', error);
				});
            } else {
				GB_Mst_Util.log('Cannot make a review. Reason: ' + result.reason);
				GB_Yg_helpers.send('Yg_OnReviewGame', {
					feedbackSent: false,
					error: result.reason || ''
				});
            }
        })
		.catch(function (error) {
			GB_Yg_helpers.sendError('Yg_OnReviewGame', error);
		});
	},

	// ---------------------- Shortcut -------------------------------------------
	/**
	* Check if Yandex shortcut prompt can be shown.
	* Sends MST_GAME_BRIDGE.Yg_OnCanShowShortcutPrompt(JSON<string>).
	* C#: extern void Gb_Yg_CanShowShortcutPrompt();
	*/
	Gb_Yg_CanShowShortcutPrompt__proxy: 'sync',
	Gb_Yg_CanShowShortcutPrompt__sig: 'v',
	Gb_Yg_CanShowShortcutPrompt: function(){
		if (!window.ysdk || !window.ysdk.shortcut) {
			GB_Yg_helpers.send('Yg_OnCanShowShortcutPrompt', { canShow: false, reason: 'unsupported' });
			return;
		}

		window.ysdk.shortcut.canShowPrompt()
		.then(function (result) {
			GB_Yg_helpers.send('Yg_OnCanShowShortcutPrompt', result);
		})
		.catch(function (error) {
			GB_Yg_helpers.sendError('Yg_OnCanShowShortcutPrompt', error);
		});
	},

	/**
	* Show Yandex shortcut prompt.
	* Sends MST_GAME_BRIDGE.Yg_OnShowShortcutPrompt(JSON<string>).
	* C#: extern void Gb_Yg_ShowShortcutPrompt();
	*/
	Gb_Yg_ShowShortcutPrompt__proxy: 'sync',
	Gb_Yg_ShowShortcutPrompt__sig: 'v',
	Gb_Yg_ShowShortcutPrompt: function(){
		if (!window.ysdk || !window.ysdk.shortcut) {
			GB_Yg_helpers.send('Yg_OnShowShortcutPrompt', { outcome: '', error: 'unsupported' });
			return;
		}

		window.ysdk.shortcut.showPrompt()
		.then(function (result) {
			GB_Yg_helpers.send('Yg_OnShowShortcutPrompt', result);
		})
		.catch(function (error) {
			GB_Yg_helpers.sendError('Yg_OnShowShortcutPrompt', error);
		});
	},

	// ---------------------- Payments -------------------------------------------
	/**
	* Purchase a product by id with developer payload; forward result to Unity.
	* Sends MST_GAME_BRIDGE.Yg_OnPurchaseResult(JSON<string>).
	* C#: extern void Gb_Yg_Purchase(IntPtr productId, IntPtr payload);
	*/
	Gb_Yg_Purchase__proxy: 'sync',
	Gb_Yg_Purchase__sig: 'vii',
	Gb_Yg_Purchase: function(productId, payload) {
		var pId = UTF8ToString(productId);
		var pPayload = UTF8ToString(payload);
		GB_Yg_helpers.getPayments(function (payments) {
			payments.purchase({ id: pId, developerPayload: pPayload })
			.then(function (purchase) {
				var data = {};

				if (purchase && purchase.signature) {
					data.signature = purchase.signature;
				} else {
					data.productID = purchase.productID;
					data.purchaseToken = purchase.purchaseToken;
					data.developerPayload = purchase.developerPayload;
				}

				GB_Yg_helpers.send('Yg_OnPurchaseResult', data);
			})
			.catch(function (error) {
				GB_Yg_helpers.sendError('Yg_OnPurchaseResult', error);
			});
		}, function (error) {
			GB_Yg_helpers.sendError('Yg_OnPurchaseResult', error);
		});
	},

	/**
	* Fetch products catalog and forward a compact list to Unity.
	* Sends MST_GAME_BRIDGE.Yg_OnGetProducts(JSON<string>).
	* C#: extern void Gb_Yg_GetProducts();
	*/
	Gb_Yg_GetProducts__proxy: 'sync',
	Gb_Yg_GetProducts__sig: 'v',
	Gb_Yg_GetProducts: function() {
		GB_Yg_helpers.getPayments(function (payments) {
		payments.getCatalog()
		.then(function (_products) {
			var products = [];

			_products.forEach(function (product) {
				products.push({
					id: product.id,
					title: product.title,
					description : product.description,
					imageUrl: product.imageURI,
					price: product.price,
					priceValue: product.priceValue,
					priceCurrencyCode: product.priceCurrencyCode,
					priceCurrencyImage: {
						small: product.getPriceCurrencyImage('small'),
						medium : product.getPriceCurrencyImage('medium'),
						svg: product.getPriceCurrencyImage('svg'),
					}
				})
			});

			GB_Yg_helpers.send('Yg_OnGetProducts', products);
		})
		.catch(function (error) {
			GB_Yg_helpers.sendError('Yg_OnGetProducts', error);
		});
		}, function (error) {
			GB_Yg_helpers.sendError('Yg_OnGetProducts', error);
		});
	},

	/**
	* Fetch purchases and forward to Unity (with signature).
	* Sends MST_GAME_BRIDGE.Yg_OnGetPurchases(JSON<string>).
	* C#: extern void Gb_Yg_GetPurchases();
	*/
	Gb_Yg_GetPurchases__proxy: 'sync',
	Gb_Yg_GetPurchases__sig: 'v',
	Gb_Yg_GetPurchases: function() {
		GB_Yg_helpers.getPayments(function (payments) {
		payments.getPurchases()
		.then(function (_purchases) {
			var data = {};

			if (_purchases && _purchases.signature) {
				data.signature = _purchases.signature;
			} else {
				data.purchases = [];

				_purchases.forEach(function (p) {
					data.purchases.push({
						productID: p.productID,
						purchaseToken: p.purchaseToken,
						developerPayload: p.developerPayload
					})
				});
			}

			GB_Yg_helpers.send('Yg_OnGetPurchases', data);
		})
		.catch(function (error) {
			GB_Yg_helpers.sendError('Yg_OnGetPurchases', error);
		});
		}, function (error) {
			GB_Yg_helpers.sendError('Yg_OnGetPurchases', error);
		});
	},

	/**
	* Consume a purchase by token (one-time items); logs errors if any.
	* C#: extern void Gb_Yg_ConsumePurchase(IntPtr purchaseToken);
	*/
	Gb_Yg_ConsumePurchase__proxy: 'sync',
	Gb_Yg_ConsumePurchase__sig: 'vi',
	Gb_Yg_ConsumePurchase: function (purchaseToken) {
		var token = UTF8ToString(purchaseToken);
		GB_Yg_helpers.getPayments(function (payments) {
		payments.consumePurchase(token)
		.then(function () {
			GB_Mst_Util.log('Purchase consumed.');
			GB_Yg_helpers.send('Yg_OnConsumePurchaseResult', {
				purchaseToken: token,
				success: true
			});
		})
		.catch(function (error) {
			var e = GB_Mst_Util.toErrorString(error);
			GB_Mst_Util.error('An error occurred while consuming purchase. Error: ' + e);
			GB_Yg_helpers.send('Yg_OnConsumePurchaseResult', {
				purchaseToken: token,
				success: false,
				error: e
			});
		});
		}, function (error) {
			var e = GB_Mst_Util.toErrorString(error);
			GB_Yg_helpers.send('Yg_OnConsumePurchaseResult', {
				purchaseToken: token,
				success: false,
				error: e
			});
		});
	}
}

// Ensure global helper from WebGLGameBridge is available here.
autoAddDeps(LibraryYandexGamesPlatform, '$GB_Mst_Util');

// Merge into Emscripten's library registry.
mergeInto(LibraryManager.library, LibraryYandexGamesPlatform);
