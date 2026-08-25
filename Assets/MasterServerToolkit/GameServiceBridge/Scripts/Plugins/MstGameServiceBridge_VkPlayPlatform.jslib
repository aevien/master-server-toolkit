var LibraryMstVkPlayPlatform = {
  $GB_VkPlay: {
    ready: false,
    initializing: false,
    disposed: false,
    externalApi: null,
    generation: 0,
    appId: '',
    loginStatus: -1,
    player: null,
    profile: null,
    token: null,

    isCurrent: function (generation) {
      return !GB_VkPlay.disposed && generation === GB_VkPlay.generation;
    },

    sendUnity: function (method, payload, generation) {
      if (!GB_VkPlay.isCurrent(generation)) return;
      if (typeof unityInstance === 'undefined' || !unityInstance || !unityInstance.SendMessage) return;
      unityInstance.SendMessage(
        'MST_GAME_BRIDGE',
        method,
        typeof payload === 'string'
          ? payload
          : JSON.stringify(payload == null ? {} : payload)
      );
    },

    errorText: function (error) {
      if (!error) return 'unknown_error';
      if (typeof error === 'string') return error;
      return error.errmsg || error.message || String(error);
    },

    loginStatusValue: function (result) {
      if (typeof result === 'number') return result;
      if (typeof result === 'string' && result.trim() !== '') return Number(result);
      if (!result || typeof result !== 'object') return -1;
      if (typeof result.loginStatus !== 'undefined') return Number(result.loginStatus);
      if (typeof result.login_status !== 'undefined') return Number(result.login_status);
      if (typeof result.status === 'number') return result.status;
      return -1;
    },

    params: function () {
      return new URLSearchParams((window.location && window.location.search) || '');
    },

    environment: function () {
      var params = GB_VkPlay.params();
      var lang = (params.get('lang') || navigator.language || 'ru').toLowerCase();
      if (lang.length > 2) lang = lang.substring(0, 2);
      return {
        app: { id: params.get('appid') || GB_VkPlay.appId || '' },
        i18n: { lang: lang || 'ru' },
        currency: (params.get('currency') || '').toUpperCase(),
        uid: params.get('uid') || '',
        signed_launch: !!params.get('sign')
      };
    },

    resetPlayer: function () {
      GB_VkPlay.player = null;
      GB_VkPlay.profile = null;
      GB_VkPlay.token = null;
    },

    emitPlayer: function (generation) {
      if (!GB_VkPlay.isCurrent(generation)) return;

      var params = GB_VkPlay.params();
      var player = GB_VkPlay.player || {};
      var profile = GB_VkPlay.profile || {};
      var token = GB_VkPlay.token || {};
      var id = String(token.uid || player.uid || params.get('uid') || '');
      var hash = String(token.hash || '');
      var error = token.status === 'error'
        ? GB_VkPlay.errorText(token)
        : (player.status === 'error' ? GB_VkPlay.errorText(player) : '');

      GB_VkPlay.sendUnity('VkPlay_OnPlayer', {
        id: id,
        is_guest: !id || !hash,
        name: String(profile.nick || ''),
        avatar: String(profile.avatar || ''),
        error: error,
        extra: {
          signature: hash,
          token: hash,
          vkplay_token: hash,
          login_status: GB_VkPlay.loginStatus
        }
      }, generation);
    },

    requestPlayer: function (generation) {
      if (!GB_VkPlay.isCurrent(generation) || !GB_VkPlay.externalApi) return;
      GB_VkPlay.resetPlayer();

      try { GB_VkPlay.externalApi.userInfo(); }
      catch (error) {
        GB_VkPlay.player = { status: 'error', errmsg: GB_VkPlay.errorText(error) };
      }

      try { GB_VkPlay.externalApi.userProfile(); }
      catch (error) {
        GB_VkPlay.profile = { status: 'error', errmsg: GB_VkPlay.errorText(error) };
      }

      try { GB_VkPlay.externalApi.getAuthToken(); }
      catch (error) {
        GB_VkPlay.token = { status: 'error', errmsg: GB_VkPlay.errorText(error) };
        GB_VkPlay.emitPlayer(generation);
      }
    },

    requestLoginStatus: function (generation) {
      if (!GB_VkPlay.isCurrent(generation) || !GB_VkPlay.externalApi) return;
      try {
        GB_VkPlay.externalApi.getLoginStatus();
      } catch (error) {
        GB_VkPlay.sendUnity('VkPlay_OnLoginStatus', {
          status: 'error',
          errmsg: GB_VkPlay.errorText(error)
        }, generation);
      }
    },

    connect: function (generation) {
      if (!GB_VkPlay.isCurrent(generation)) return;
      if (typeof window.iframeApi !== 'function') {
        GB_VkPlay.initializing = false;
        return;
      }

      var callbacks = {
        appid: GB_VkPlay.appId,
        getLoginStatusCallback: function (result) {
          if (!GB_VkPlay.isCurrent(generation)) return;
          var loginStatus = GB_VkPlay.loginStatusValue(result);
          if (loginStatus >= 0) {
            GB_VkPlay.loginStatus = loginStatus;
            if (GB_VkPlay.loginStatus >= 2) {
              GB_VkPlay.requestPlayer(generation);
            } else {
              GB_VkPlay.player = { uid: GB_VkPlay.params().get('uid') || '' };
              GB_VkPlay.profile = {};
              GB_VkPlay.token = {};
              GB_VkPlay.emitPlayer(generation);
            }
          } else {
            GB_VkPlay.player = result || {};
            GB_VkPlay.profile = {};
            GB_VkPlay.token = result || {};
            GB_VkPlay.emitPlayer(generation);
          }
          GB_VkPlay.sendUnity('VkPlay_OnLoginStatus', result || {}, generation);
        },
        userInfoCallback: function (result) {
          if (!GB_VkPlay.isCurrent(generation)) return;
          GB_VkPlay.player = result || {};
          if (GB_VkPlay.token) GB_VkPlay.emitPlayer(generation);
        },
        userProfileCallback: function (result) {
          if (!GB_VkPlay.isCurrent(generation)) return;
          GB_VkPlay.profile = result || {};
          if (GB_VkPlay.token) GB_VkPlay.emitPlayer(generation);
        },
        registerUserCallback: function (result) {
          if (!GB_VkPlay.isCurrent(generation)) return;
          var success = !result || result.status === 'ok' || result.success === true;
          GB_VkPlay.sendUnity('VkPlay_OnAuthentication', {
            success: success,
            error: success ? '' : GB_VkPlay.errorText(result)
          }, generation);
          if (success) {
            GB_VkPlay.loginStatus = 2;
            if (GB_VkPlay.externalApi && typeof GB_VkPlay.externalApi.reloadWindow === 'function') {
              GB_VkPlay.externalApi.reloadWindow();
            } else if (window.location && typeof window.location.reload === 'function') {
              window.location.reload();
            }
          }
        },
        getAuthTokenCallback: function (result) {
          if (!GB_VkPlay.isCurrent(generation)) return;
          GB_VkPlay.token = result || {};
          GB_VkPlay.emitPlayer(generation);
        },
        userFriendsCallback: function (result) {
          GB_VkPlay.sendUnity('VkPlay_OnFriends', result || {}, generation);
        },
        userSocialFriendsCallback: function (result) {
          GB_VkPlay.sendUnity('VkPlay_OnSocialFriends', result || {}, generation);
        },
        paymentFrameUrlCallback: function () {},
        paymentReceivedCallback: function () {},
        paymentWindowClosedCallback: function () {},
        confirmWindowClosedCallback: function () {},
        userConfirmCallback: function () {},
        getGameInventoryItemsCallback: function () {}
      };

      Promise.resolve(window.iframeApi(callbacks))
        .then(function (api) {
          if (!GB_VkPlay.isCurrent(generation)) return;
          GB_VkPlay.externalApi = api;
          GB_VkPlay.ready = !!api;
          GB_VkPlay.initializing = false;
          if (GB_VkPlay.ready) GB_VkPlay.requestLoginStatus(generation);
        })
        .catch(function (error) {
          if (!GB_VkPlay.isCurrent(generation)) return;
          GB_VkPlay.initializing = false;
          if (typeof GB_Mst_Util !== 'undefined') {
            GB_Mst_Util.error('VK Play iframe API handshake failed: ' + GB_VkPlay.errorText(error));
          }
        });
    }
  },

  Gb_VkPlay_InitSdk__deps: ['$GB_VkPlay', '$GB_Mst_Util'],
  Gb_VkPlay_InitSdk__proxy: 'sync',
  Gb_VkPlay_InitSdk__sig: 'vii',
  Gb_VkPlay_InitSdk: function (appIdPtr, scriptUrlPtr) {
    if (GB_VkPlay.ready || GB_VkPlay.initializing) return;
    GB_VkPlay.disposed = false;
    GB_VkPlay.generation += 1;
    var generation = GB_VkPlay.generation;
    GB_VkPlay.initializing = true;
    GB_VkPlay.appId = GB_VkPlay.params().get('appid') || UTF8ToString(appIdPtr) || '';

    if (typeof window.iframeApi === 'function') {
      GB_VkPlay.connect(generation);
      return;
    }

    var scriptUrl = UTF8ToString(scriptUrlPtr);
    scriptUrl = scriptUrl.replace('{0}', encodeURIComponent(GB_VkPlay.appId));
    scriptUrl = scriptUrl.replace('[GMRID]', encodeURIComponent(GB_VkPlay.appId));
    if (!scriptUrl) {
      GB_VkPlay.initializing = false;
      return;
    }

    var script = document.createElement('script');
    script.src = scriptUrl;
    script.async = true;
    script.onload = function () { GB_VkPlay.connect(generation); };
    script.onerror = function () {
      if (!GB_VkPlay.isCurrent(generation)) return;
      GB_VkPlay.initializing = false;
      if (typeof GB_Mst_Util !== 'undefined') {
        GB_Mst_Util.error('Failed to load VK Play JS API: ' + scriptUrl);
      }
    };
    document.head.appendChild(script);
  },

  Gb_VkPlay_IsReady__deps: ['$GB_VkPlay'],
  Gb_VkPlay_IsReady__proxy: 'sync',
  Gb_VkPlay_IsReady__sig: 'i',
  Gb_VkPlay_IsReady: function () {
    return GB_VkPlay.ready ? 1 : 0;
  },

  Gb_VkPlay_Environment__deps: ['$GB_VkPlay'],
  Gb_VkPlay_Environment__proxy: 'sync',
  Gb_VkPlay_Environment__sig: 'i',
  Gb_VkPlay_Environment: function () {
    var value = JSON.stringify(GB_VkPlay.environment());
    var length = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(length);
    stringToUTF8(value, buffer, length);
    return buffer;
  },

  Gb_VkPlay_Device__proxy: 'sync',
  Gb_VkPlay_Device__sig: 'i',
  Gb_VkPlay_Device: function () {
    var userAgent = (navigator.userAgent || '').toLowerCase();
    var device = /ipad|tablet/.test(userAgent)
      ? 'Tablet'
      : (/android|iphone|ipod|mobile/.test(userAgent) ? 'Mobile' : 'Desktop');
    var length = lengthBytesUTF8(device) + 1;
    var buffer = _malloc(length);
    stringToUTF8(device, buffer, length);
    return buffer;
  },

  Gb_VkPlay_GetPlayer__deps: ['$GB_VkPlay'],
  Gb_VkPlay_GetPlayer__proxy: 'sync',
  Gb_VkPlay_GetPlayer__sig: 'v',
  Gb_VkPlay_GetPlayer: function () {
    GB_VkPlay.requestPlayer(GB_VkPlay.generation);
  },

  Gb_VkPlay_Authenticate__deps: ['$GB_VkPlay'],
  Gb_VkPlay_Authenticate__proxy: 'sync',
  Gb_VkPlay_Authenticate__sig: 'v',
  Gb_VkPlay_Authenticate: function () {
    var generation = GB_VkPlay.generation;
    if (!GB_VkPlay.isCurrent(generation) || !GB_VkPlay.externalApi) {
      GB_VkPlay.sendUnity('VkPlay_OnAuthentication', {
        success: false,
        error: 'vkplay_api_not_ready'
      }, generation);
      return;
    }

    try {
      if (GB_VkPlay.loginStatus === 0) {
        GB_VkPlay.externalApi.authUser();
      } else if (GB_VkPlay.loginStatus === 1) {
        GB_VkPlay.externalApi.registerUser();
      } else if (GB_VkPlay.loginStatus >= 2) {
        GB_VkPlay.sendUnity('VkPlay_OnAuthentication', { success: true, error: '' }, generation);
        GB_VkPlay.requestPlayer(generation);
      } else {
        GB_VkPlay.requestLoginStatus(generation);
      }
    } catch (error) {
      GB_VkPlay.sendUnity('VkPlay_OnAuthentication', {
        success: false,
        error: GB_VkPlay.errorText(error)
      }, generation);
    }
  },

  Gb_VkPlay_GetFriends__deps: ['$GB_VkPlay'],
  Gb_VkPlay_GetFriends__proxy: 'sync',
  Gb_VkPlay_GetFriends__sig: 'vi',
  Gb_VkPlay_GetFriends: function (social) {
    var generation = GB_VkPlay.generation;
    if (!GB_VkPlay.isCurrent(generation) || !GB_VkPlay.externalApi) return;
    try {
      if (social) GB_VkPlay.externalApi.userSocialFriends();
      else GB_VkPlay.externalApi.userFriends();
    } catch (error) {
      GB_VkPlay.sendUnity(social ? 'VkPlay_OnSocialFriends' : 'VkPlay_OnFriends', {
        status: 'error',
        errmsg: GB_VkPlay.errorText(error)
      }, generation);
    }
  },

  Gb_VkPlay_Dispose__deps: ['$GB_VkPlay'],
  Gb_VkPlay_Dispose__proxy: 'sync',
  Gb_VkPlay_Dispose__sig: 'v',
  Gb_VkPlay_Dispose: function () {
    GB_VkPlay.disposed = true;
    GB_VkPlay.generation += 1;
    GB_VkPlay.ready = false;
    GB_VkPlay.initializing = false;
    GB_VkPlay.externalApi = null;
    GB_VkPlay.resetPlayer();
  }
};

autoAddDeps(LibraryMstVkPlayPlatform, '$GB_Mst_Util');
mergeInto(LibraryManager.library, LibraryMstVkPlayPlatform);
