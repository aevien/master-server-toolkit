const libraryYandexGamesPlatform = {
	$gamePlatform: {
		/** Gets url query string params after ? */
		getUrlParams: function () {
			var urlParams = new URLSearchParams(window.location.search);
			var paramsObject = {};

			urlParams.forEach((value, key) => {
				paramsObject[key] = value;
			});

			return paramsObject;
		},

		/** Gets full url */
		getUrl: function () {
			return window.location.href;
		},

		initPlayer: function () {
			const player = {
					id: 'player-id-' + Date.now(),
					is_guest: true,
					name: 'No Name',
					avatar: '',
					extra: {}
				}

			ysdk.getPlayer({ signed: true }).then(_player => {

				window.yandexPlayer = _player;
				
				player.id = _player.getUniqueID();
				player.is_guest = _player.getMode() === 'lite'
				player.name = _player.getName();
				player.avatar = _player.getPhoto('medium');
				player.extra = {
					payingStatus: _player.getPayingStatus()
				}

				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetPlayer', JSON.stringify(player));
			}).catch(error => {
				console.error('Could not get Yandex Games player', error);
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetPlayer', JSON.stringify(player));
			});
		}
	},

	Gb_Yg_SetApiReady: function() {
		console.log('Yandex loading Api ready')
		window.ysdk.features.LoadingAPI.ready();
	},

	Gb_Yg_initSdk: function() {
		function initSdk ()
		{
			YaGames.init().then(ysdk => {
				console.log('Yandex SDK initialized');
				window.ysdk = ysdk;

				console.log('Environment info: ', window.ysdk.environment)

				// Init leaderboard
				window.ysdk.getLeaderboards().then(_lb => {
					window.yandexLeaderboards = _lb
				}).catch(error => {
					console.log('An error occurred while getting leaderboards', error);
				});

				window.ysdk.getPayments({ signed: true }).then(payments => {
					window.yandexPayments = payments;
				}).catch(error => {
					console.log('An error occurred while getting Yandex payments', error);
					// Покупки недоступны. Включите монетизацию в консоли разработчика.
					// Убедитесь, что на вкладке Покупки консоли разработчика присутствует таблица
					// хотя бы с одним внутриигровым товаром и надписью «Покупки разрешены».
				})

				// Listen to yandex events
				window.ysdk.on('game_api_pause', () => {
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGameApiPause', 1);
				});

				window.ysdk.on('game_api_resume', () => {
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGameApiPause', 0);
				});
			});
		}
		
		// Удаленный https://sdk.games.s3.yandex.net/sdk.js
		const sdk = '/sdk.js';
		const s = document.createElement('script');
		s.src = sdk;
		s.async = true;
		s.onload = initSdk;
		document.body.append(s);
	},

	Gb_Yg_Environment: function () {
		const environment = {
			app: {
				id: window.ysdk.environment.app.id
			},
			i18n: {
				lang: window.ysdk.environment.i18n.lang,
				tld: window.ysdk.environment.i18n.tld
			},
			payload: window.ysdk.environment.payload
		}
		var bufferSize = lengthBytesUTF8(JSON.stringify(environment)) + 1;
		var buffer = _malloc(bufferSize);
		stringToUTF8(JSON.stringify(environment), buffer, bufferSize);
		return buffer;
	},

	Gb_Yg_Device: function () {
		const device = window.ysdk.deviceInfo;
		var bufferSize = lengthBytesUTF8(device) + 1;
		var buffer = _malloc(bufferSize);
		stringToUTF8(device, buffer, bufferSize);
		return buffer;
	},

	Gb_Yg_isReady: function () {
		return window.ysdk ? 1: 0;
	},

	Gb_Yg_GameStart: function () {
		if(window.ysdk && window.ysdk.features.GameplayAPI)
			window.ysdk.features.GameplayAPI.start();
	},

	Gb_Yg_GameStop: function () {
		if(window.ysdk && window.ysdk.features.GameplayAPI)
			window.ysdk.features.GameplayAPI.stop();
	},

	Gb_Yg_GetPlayer: function () {
		gamePlatform.initPlayer();
	},

	Gb_Yg_AuthPlayer: function() {
		if (window.yandexPlayer.getMode() === 'lite') {
			const result = {
				success: false,
				error: ''
			}

            window.ysdk.auth.openAuthDialog().then(() => {
				result.success = true;
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnAuthPlayer', JSON.stringify(result));
			}).catch(error => {
				console.error('Could not auth Yandex Games player', error);
				result.error = error;
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnAuthPlayer', JSON.stringify(result));
			});
        }
	},

	Gb_Yg_GetPlayerData: function() {
		window.yandexPlayer.getData()
		.then(data=> {
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnPlayerGetData', JSON.stringify(data));
		}).catch(error => {
			console.error('Could not get player data from yandex cloud', error);
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnPlayerGetData', JSON.stringify({}));
		});
	},

	Gb_Yg_SetPlayerData: function(data, saveAsStats) {
		const json = JSON.parse(UTF8ToString(data));
		if(saveAsStats) {
			window.yandexPlayer.setStats(json).then(() => {
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnPlayerSetData', JSON.stringify({ success: true, error: '' }));
			}).catch(error => {
				console.error('Could not set player data to yandex cloud', error);
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnPlayerSetData', JSON.stringify({ success: false, error: 'Could not set player data'}));
			});
		}else{
			window.yandexPlayer.setData(json).then(() => {
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnPlayerSetData', JSON.stringify({ success: true, error: '' }));
			}).catch(error => {
				console.error('Could not set player data to yandex cloud', error);
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnPlayerSetData', JSON.stringify({ success: false, error: 'Could not set player data'}));
			});
		}
	},

	Gb_Yg_ShowFullScreenAdv: function () {
		window.ysdk.adv.showFullscreenAdv({
			callbacks: {
				onOpen: function (wasShown) {
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnFullScreenVideoStatus', 'Opened');
				},
				onClose: function (wasShown) {
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnFullScreenVideoStatus', 'Closed');
				},
				onError: function (error) {
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnFullScreenVideoStatus', 'Error');
					console.error("Error while showing full screen vide", error);
				}
			}
		});
	},

	Gb_Yg_ShowRewardedVideo: function() {
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
					unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnRewardedVideoStatus', 'Error');
					console.error('Error while open rewarded video adv:', error);
				}
			}
		})
	},

	Gb_Yg_SetLeaderboardScore: function(data) {
		const json = JSON.parse(UTF8ToString(data));
		window.ysdk.isAvailableMethod('leaderboards.setLeaderboardScore').then(isAvailable => {
			if (isAvailable === true) {
				window.yandexLeaderboards.setLeaderboardScore(json.leaderboardName, json.score, json.extraData);
			}
		});
	},

	Gb_Yg_GetLeaderboardDescription: function(name) {
		window.yandexLeaderboards.getLeaderboardDescription(UTF8ToString(name)).then(data => {
			const leaderboard = {
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
		}).catch(error => {
			console.error('An error occurred while getting description of ' + UTF8ToString(name), error);
		});
	},

	Gb_Yg_GetLeaderboardEntries: function(name, options) {

		window.yandexLeaderboards.getLeaderboardEntries(UTF8ToString(name), JSON.parse(UTF8ToString(options))).then(data => {
			const leaderboardEntries = {
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
				const leaderboardPlayerEntry = {
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
		}).catch(error => {
			console.error('An error occurred while getting entries of leaderboard', error);
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetLeaderboardEntries', JSON.stringify({error: error}));
		});
	},

	Gb_Yg_GetLeaderboardPlayerEntry: function(name) {
		window.yandexLeaderboards.getLeaderboardPlayerEntry(UTF8ToString(name)).then(data => {
			const leaderboardPlayerEntry = {
				score: data.score,
				extraData: data.extraData,
				rank: data.rank,
				player: {
					avatar: data.player.getAvatarSrc('medium'),
					lang: data.player.lang,
					publicName: data.publicName,
					scopePermissions: data.player.scopePermissions,
					uniqueID: data.player.uniqueID,
				},
				formattedScore: data.formattedScore
			}
			
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetLeaderboardPlayerEntry', JSON.stringify(leaderboardPlayerEntry));
		}).catch(error => {
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetLeaderboardPlayerEntry', JSON.stringify({error: error}));
			console.error('An error occurred while getting leaderboard player entry. ', error);
		});
	},

	Gb_Yg_ReviewGame: function(){
		window.ysdk.feedback.canReview().then(({ value, reason }) => {
            if (value) {
                ysdk.feedback.requestReview()
                    .then(({ feedbackSent }) => {
                        console.log(feedbackSent);
                    })
            } else {
                console.log(reason)
            }
        })
	},

	Gb_Yg_Purchase: function(productId, payload) {
		if(window.yandexPayments) {
			window.yandexPayments.purchase({ id: UTF8ToString(productId), developerPayload: UTF8ToString(payload) }).then(purchase => {
				const data = {
					productID: purchase.productID,
					purchaseToken: purchase.purchaseToken,
					developerPayload: purchase.developerPayload,
					signature: purchase.signature
				}

				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnPurchaseResult', JSON.stringify(data));
			}).catch(error => {
				console.error('An error occurred while trying to make purchase.', error);
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnPurchaseResult', JSON.stringify({error: error}));
				// Покупка не удалась: в консоли разработчика не добавлен товар с таким id,
				// пользователь не авторизовался, передумал и закрыл окно оплаты,
				// истекло отведенное на покупку время, не хватило денег и т. д.
			})
		} else {
			console.error('An error occurred while trying to make purchase. Payments not defined');
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnPurchaseResult', JSON.stringify({error: 'Not defined'}));
		}
	},

	Gb_Yg_GetProducts: function() {
		if(window.yandexPayments) {
			window.yandexPayments.getCatalog().then(_products => {
				const products = [];

				_products.forEach(product => {
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

				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetProducts', JSON.stringify(products));
			});
		} else {
			console.error('An error occurred while getting products. Payments not defined');
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetProducts', JSON.stringify({error: 'Not defined'}));
		}
	},

	Gb_Yg_GetPurchases: function() {
		if(window.yandexPayments) {
			window.yandexPayments.getPurchases().then(_purchases => {

				const data = {
					purchases: [],
					signature: _purchases.signature
				}

				_purchases.forEach(p => {
					data.purchases.push({
						productID: p.productID,
						purchaseToken: p.purchaseToken,
						developerPayload: p.developerPayload
					})
				});

				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetPurchases', JSON.stringify(data))
			}).catch(error => {
				console.error('An error occurred while getting purchases', error);
				unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetPurchases', JSON.stringify({error: 'Not defined'}));
			});
		} else {
			console.error('An error occurred while getting purchases. Payments not defined');
			unityInstance.SendMessage('MST_GAME_BRIDGE', 'Yg_OnGetPurchases', JSON.stringify({error: 'Not defined'}));
		}
	},

	Gb_Yg_ConsumePurchase: function (purchaseToken) {
		console.log('Consume purchase ' + UTF8ToString(purchaseToken))
		if(window.yandexPayments) {
			window.yandexPayments.consumePurchase(UTF8ToString(purchaseToken));
		} else {
			console.error('An error occurred while consuming purchase. Payments not defined');
		}
	}
}

autoAddDeps(libraryYandexGamesPlatform, '$gamePlatform');
mergeInto(LibraryManager.library, libraryYandexGamesPlatform);