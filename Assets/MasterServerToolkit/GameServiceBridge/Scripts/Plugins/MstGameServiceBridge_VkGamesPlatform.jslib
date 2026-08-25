var LibraryMstVkGamesPlatform = {
  $GB_Vk: {
    ready: false,
    initializing: false,
    subscribed: false,
    disposed: false,
    bridge: null,
    eventHandler: null,
    favoriteAdded: false,
    generation: 0,
    capabilityTimeoutMs: 5000,
    storageTimeoutMs: 10000,

    isCurrent: function (generation) {
      return !GB_Vk.disposed && generation === GB_Vk.generation;
    },

    sendUnity: function (method, payload, generation) {
      if (GB_Vk.disposed ||
        (typeof generation === 'number' && !GB_Vk.isCurrent(generation))) return;
      if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) {
        unityInstance.SendMessage('MST_GAME_BRIDGE', method,
          typeof payload === 'string' || typeof payload === 'number'
            ? payload
            : JSON.stringify(payload == null ? {} : payload));
      }
    },

    errorText: function (error) {
      if (!error) return 'unknown_error';
      return error.message || error.error_type || String(error);
    },

    params: function () {
      return new URLSearchParams((window.location && window.location.search) || '');
    },

    rawQuery: function () {
      var search = (window.location && window.location.search) || '';
      return search.charAt(0) === '?' ? search.substring(1) : search;
    },

    language: function (params) {
      var value = (params.get('language') || '').toLowerCase();
      if (value === '0') return 'ru';
      if (value === '3') return 'en';
      if (/^[a-z]{2}([_-][a-z]{2})?$/.test(value)) return value.substring(0, 2);

      var browserLanguage = (navigator.language || '').toLowerCase();
      return /^[a-z]{2}/.test(browserLanguage) ? browserLanguage.substring(0, 2) : 'en';
    },

    launchParameters: function (params) {
      var values = {};
      var excluded = {
        api_id: true,
        viewer_id: true,
        language: true,
        platform: true,
        hash: true,
        referrer: true,
        sid: true,
        secret: true,
        access_token: true,
        auth_key: true,
        timestamp: true,
        sign: true,
        sign_keys: true
      };

      params.forEach(function (value, key) {
        if (!excluded[key]) values[key] = value;
      });
      return values;
    },

    resolveBridge: function () {
      var value = window.vkBridge || null;
      return value && value.default ? value.default : value;
    },

    withTimeout: function (promise, timeoutMs) {
      return new Promise(function (resolve, reject) {
        var settled = false;
        var timer = setTimeout(function () {
          if (settled) return;
          settled = true;
          reject(new Error('vk_bridge_timeout'));
        }, timeoutMs);

        promise.then(function (value) {
          if (settled) return;
          settled = true;
          clearTimeout(timer);
          resolve(value);
        }).catch(function (error) {
          if (settled) return;
          settled = true;
          clearTimeout(timer);
          reject(error);
        });
      });
    },

    supports: function (method, generation) {
      if (!GB_Vk.isCurrent(generation) || !GB_Vk.ready || !GB_Vk.bridge) return Promise.resolve(false);
      var bridge = GB_Vk.bridge;
      if (bridge.supportsAsync) {
        try {
          return GB_Vk.withTimeout(
            Promise.resolve(bridge.supportsAsync(method)),
            GB_Vk.capabilityTimeoutMs
          ).catch(function () { return false; });
        } catch (error) {
          return Promise.resolve(false);
        }
      }
      if (bridge.supports) {
        try { return Promise.resolve(!!bridge.supports(method)); }
        catch (error) { return Promise.resolve(false); }
      }
      return Promise.resolve(false);
    },

    checkNativeAd: function (format, generation) {
      return Promise.all([
        GB_Vk.supports('VKWebAppCheckNativeAds', generation),
        GB_Vk.supports('VKWebAppShowNativeAds', generation)
      ]).then(function (support) {
        if (!support[0] || !support[1]) return false;
        return GB_Vk.withTimeout(
          GB_Vk.bridgeSend('VKWebAppCheckNativeAds', { ad_format: format }, generation),
          GB_Vk.capabilityTimeoutMs
        )
          .then(function (response) { return !!(response && response.result); });
      });
    },

    checkBannerAd: function (generation) {
      return Promise.all([
        GB_Vk.supports('VKWebAppCheckBannerAd', generation),
        GB_Vk.supports('VKWebAppShowBannerAd', generation)
      ]).then(function (support) {
        if (!support[0] || !support[1]) return false;
        return GB_Vk.withTimeout(
          GB_Vk.bridgeSend('VKWebAppCheckBannerAd', null, generation),
          GB_Vk.capabilityTimeoutMs
        )
          .then(function (response) { return !!(response && response.result); });
      });
    },

    isFavorite: function () {
      var value = (GB_Vk.params().get('is_favorite') || '').toLowerCase();
      return GB_Vk.favoriteAdded || value === '1' || value === 'true';
    },

    subscribe: function (generation) {
      if (GB_Vk.subscribed || !GB_Vk.bridge || !GB_Vk.bridge.subscribe) return;
      GB_Vk.eventHandler = function (event) {
        if (!event || !event.detail) return;
        if (event.detail.type === 'VKWebAppViewHide') GB_Vk.sendUnity('Vk_OnViewVisibility', 1, generation);
        if (event.detail.type === 'VKWebAppViewRestore') GB_Vk.sendUnity('Vk_OnViewVisibility', 0, generation);
      };
      GB_Vk.bridge.subscribe(GB_Vk.eventHandler);
      GB_Vk.subscribed = true;
    },

    unsubscribe: function () {
      if (GB_Vk.subscribed && GB_Vk.bridge && GB_Vk.bridge.unsubscribe && GB_Vk.eventHandler) {
        GB_Vk.bridge.unsubscribe(GB_Vk.eventHandler);
      }
      GB_Vk.eventHandler = null;
      GB_Vk.subscribed = false;
    },

    initializeBridge: function (generation) {
      if (!GB_Vk.isCurrent(generation)) return;
      var bridge = GB_Vk.resolveBridge();
      GB_Vk.bridge = bridge;
      if (!bridge || !bridge.send) {
        GB_Vk.initializing = false;
        return;
      }
      GB_Vk.withTimeout(
        Promise.resolve().then(function () {
          if (!GB_Vk.isCurrent(generation)) throw new Error('vk_bridge_disposed');
          return bridge.send('VKWebAppInit');
        }),
        GB_Vk.capabilityTimeoutMs
      )
        .then(function () {
          if (!GB_Vk.isCurrent(generation)) return;
          GB_Vk.ready = true;
          GB_Vk.initializing = false;
          GB_Vk.subscribe(generation);
        })
        .catch(function (error) {
          if (!GB_Vk.isCurrent(generation)) return;
          GB_Vk.initializing = false;
          if (typeof GB_Mst_Util !== 'undefined') GB_Mst_Util.error('VKWebAppInit failed: ' + GB_Vk.errorText(error));
        });
    },

    bridgeSend: function (method, params, generation) {
      if (!GB_Vk.isCurrent(generation) || !GB_Vk.ready || !GB_Vk.bridge)
        return Promise.reject(new Error('vk_bridge_not_ready'));
      var bridge = GB_Vk.bridge;
      return Promise.resolve().then(function () {
        if (!GB_Vk.isCurrent(generation)) throw new Error('vk_bridge_disposed');
        return bridge.send(method, params || {});
      });
    }
  },

  Gb_Vk_InitSdk__deps: ['$GB_Vk', '$GB_Mst_Util'],
  Gb_Vk_InitSdk__proxy: 'sync',
  Gb_Vk_InitSdk__sig: 'vi',
  Gb_Vk_InitSdk: function (urlPtr) {
    if (GB_Vk.ready || GB_Vk.initializing) return;
    GB_Vk.disposed = false;
    GB_Vk.generation += 1;
    var generation = GB_Vk.generation;
    GB_Vk.initializing = true;
    if (GB_Vk.resolveBridge()) {
      GB_Vk.initializeBridge(generation);
      return;
    }

    var url = UTF8ToString(urlPtr);
    if (!url) {
      GB_Vk.initializing = false;
      return;
    }
    var script = document.createElement('script');
    script.src = url;
    script.async = true;
    script.onload = function () { GB_Vk.initializeBridge(generation); };
    script.onerror = function () {
      if (!GB_Vk.isCurrent(generation)) return;
      GB_Vk.initializing = false;
      if (typeof GB_Mst_Util !== 'undefined') GB_Mst_Util.error('Could not load VK Bridge: ' + url);
    };
    document.head.appendChild(script);
  },

  Gb_Vk_IsReady__deps: ['$GB_Vk'],
  Gb_Vk_IsReady__proxy: 'sync',
  Gb_Vk_IsReady__sig: 'i',
  Gb_Vk_IsReady: function () { return GB_Vk.ready ? 1 : 0; },

  Gb_Vk_Environment__deps: ['$GB_Vk'],
  Gb_Vk_Environment__proxy: 'sync',
  Gb_Vk_Environment__sig: 'i',
  Gb_Vk_Environment: function () {
    var p = GB_Vk.params();
    var payload = {};
    var hash = p.get('hash') || '';
    var ref = p.get('referrer') || '';
    if (hash) payload.hash = hash;
    var result = JSON.stringify({
      app: { id: p.get('api_id') || '' },
      i18n: { lang: GB_Vk.language(p) },
      payload: payload,
      referrer: ref ? { type: ref, promoId: '', intent: '', inappId: '' } : {},
      launchParameters: GB_Vk.launchParameters(p)
    });
    var size = lengthBytesUTF8(result) + 1;
    var buffer = _malloc(size);
    stringToUTF8(result, buffer, size);
    return buffer;
  },

  Gb_Vk_Device__deps: ['$GB_Vk'],
  Gb_Vk_Device__proxy: 'sync',
  Gb_Vk_Device__sig: 'i',
  Gb_Vk_Device: function () {
    var platform = (GB_Vk.params().get('platform') || '').toLowerCase();
    var device = platform.indexOf('mobile') !== -1 ||
      platform.indexOf('android') !== -1 ||
      platform.indexOf('ios') !== -1 ||
      platform === 'mvk' ? 'Mobile' : 'Desktop';
    var size = lengthBytesUTF8(device) + 1;
    var buffer = _malloc(size);
    stringToUTF8(device, buffer, size);
    return buffer;
  },

  Gb_Vk_ServerTime__proxy: 'sync',
  Gb_Vk_ServerTime__sig: 'd',
  Gb_Vk_ServerTime: function () { return Date.now(); },

  Gb_Vk_GetPlayer__deps: ['$GB_Vk'],
  Gb_Vk_GetPlayer__proxy: 'sync',
  Gb_Vk_GetPlayer__sig: 'v',
  Gb_Vk_GetPlayer: function () {
    var generation = GB_Vk.generation;
    var p = GB_Vk.params();
    var id = p.get('viewer_id') || '';
    var isGuest = !id || id === '0';
    var fallback = {
      id: id,
      name: isGuest ? '' : ('VK ' + id),
      avatar: '',
      is_guest: isGuest,
      extra: { signature: GB_Vk.rawQuery() }
    };

    if (isGuest) {
      GB_Vk.sendUnity('Vk_OnGetPlayer', fallback, generation);
      return;
    }

    GB_Vk.bridgeSend('VKWebAppGetUserInfo', null, generation)
      .then(function (user) {
        if (String(user.id || '') !== id) throw new Error('vk_profile_id_mismatch');
        fallback.name = ((user.first_name || '') + ' ' + (user.last_name || '')).trim();
        fallback.avatar = user.photo_200 || user.photo_100 || '';
        fallback.is_guest = false;
        GB_Vk.sendUnity('Vk_OnGetPlayer', fallback, generation);
      })
      .catch(function (error) {
        fallback.error = GB_Vk.errorText(error);
        GB_Vk.sendUnity('Vk_OnGetPlayer', fallback, generation);
      });
  },

  Gb_Vk_GetPlayerData__deps: ['$GB_Vk'],
  Gb_Vk_GetPlayerData__proxy: 'sync',
  Gb_Vk_GetPlayerData__sig: 'vi',
  Gb_Vk_GetPlayerData: function (keyPtr) {
    var generation = GB_Vk.generation;
    var key = UTF8ToString(keyPtr);
    GB_Vk.withTimeout(
      GB_Vk.bridgeSend('VKWebAppStorageGet', { keys: [key] }, generation),
      GB_Vk.storageTimeoutMs
    )
      .then(function (response) {
        var text = response.keys && response.keys.length ? response.keys[0].value : '';
        var data = text ? JSON.parse(text) : {};
        GB_Vk.sendUnity('Vk_OnPlayerGetData', { data: data }, generation);
      })
      .catch(function (error) {
        GB_Vk.sendUnity('Vk_OnPlayerGetData', { error: GB_Vk.errorText(error) }, generation);
      });
  },

  Gb_Vk_SetPlayerData__deps: ['$GB_Vk'],
  Gb_Vk_SetPlayerData__proxy: 'sync',
  Gb_Vk_SetPlayerData__sig: 'vii',
  Gb_Vk_SetPlayerData: function (keyPtr, dataPtr) {
    var generation = GB_Vk.generation;
    var key = UTF8ToString(keyPtr);
    var value = UTF8ToString(dataPtr);
    GB_Vk.withTimeout(
      GB_Vk.bridgeSend('VKWebAppStorageSet', { key: key, value: value }, generation),
      GB_Vk.storageTimeoutMs
    )
      .then(function (response) {
        var success = !!(response && response.result);
        GB_Vk.sendUnity('Vk_OnPlayerSetData', {
          success: success,
          error: success ? '' : 'storage_rejected'
        }, generation);
      })
      .catch(function (error) {
        GB_Vk.sendUnity('Vk_OnPlayerSetData', {
          success: false,
          error: GB_Vk.errorText(error)
        }, generation);
      });
  },

  Gb_Vk_CheckInterstitial__deps: ['$GB_Vk'],
  Gb_Vk_CheckInterstitial__proxy: 'sync',
  Gb_Vk_CheckInterstitial__sig: 'v',
  Gb_Vk_CheckInterstitial: function () {
    var generation = GB_Vk.generation;
    GB_Vk.checkNativeAd('interstitial', generation)
      .then(function (available) {
        GB_Vk.sendUnity('Vk_OnInterstitialAvailability', { available: available }, generation);
      })
      .catch(function () {
        GB_Vk.sendUnity('Vk_OnInterstitialAvailability', { available: false }, generation);
      });
  },

  Gb_Vk_ShowInterstitial__deps: ['$GB_Vk'],
  Gb_Vk_ShowInterstitial__proxy: 'sync',
  Gb_Vk_ShowInterstitial__sig: 'v',
  Gb_Vk_ShowInterstitial: function () {
    var generation = GB_Vk.generation;
    GB_Vk.checkNativeAd('interstitial', generation)
      .then(function (available) {
        GB_Vk.sendUnity('Vk_OnInterstitialAvailability', { available: available }, generation);
        if (!available) {
          GB_Vk.sendUnity('Vk_OnFullScreenVideoStatus', 'Error', generation);
          return null;
        }

        GB_Vk.sendUnity('Vk_OnFullScreenVideoStatus', 'Opened', generation);
        return GB_Vk.bridgeSend('VKWebAppShowNativeAds', { ad_format: 'interstitial' }, generation)
          .then(function (response) {
            GB_Vk.sendUnity(
              'Vk_OnFullScreenVideoStatus',
              response && response.result ? 'Closed' : 'Error',
              generation);
          });
      })
      .catch(function () { GB_Vk.sendUnity('Vk_OnFullScreenVideoStatus', 'Error', generation); });
  },

  Gb_Vk_ShowRewarded__deps: ['$GB_Vk'],
  Gb_Vk_ShowRewarded__proxy: 'sync',
  Gb_Vk_ShowRewarded__sig: 'v',
  Gb_Vk_ShowRewarded: function () {
    var generation = GB_Vk.generation;
    GB_Vk.checkNativeAd('reward', generation)
      .then(function (available) {
        if (!available) {
          GB_Vk.sendUnity('Vk_OnRewardedVideoStatus', 'Error', generation);
          return null;
        }

        GB_Vk.sendUnity('Vk_OnRewardedVideoStatus', 'Opened', generation);
        return GB_Vk.bridgeSend('VKWebAppShowNativeAds', { ad_format: 'reward' }, generation)
          .then(function (response) {
            if (response && response.result) {
              GB_Vk.sendUnity('Vk_OnRewardedVideoStatus', 'Rewarded', generation);
            } else {
              GB_Vk.sendUnity('Vk_OnRewardedVideoStatus', 'Closed', generation);
            }
          });
      })
      .catch(function () { GB_Vk.sendUnity('Vk_OnRewardedVideoStatus', 'Error', generation); });
  },

  Gb_Vk_ShowBanner__deps: ['$GB_Vk'],
  Gb_Vk_ShowBanner__proxy: 'sync',
  Gb_Vk_ShowBanner__sig: 'v',
  Gb_Vk_ShowBanner: function () {
    var generation = GB_Vk.generation;
    GB_Vk.checkBannerAd(generation)
      .then(function (available) {
        if (!available) {
          GB_Vk.sendUnity('Vk_OnBannerAdvertisementStatus', 'Error', generation);
          return null;
        }

        return GB_Vk.withTimeout(
          GB_Vk.bridgeSend('VKWebAppShowBannerAd', { banner_location: 'bottom' }, generation),
          GB_Vk.capabilityTimeoutMs
        )
          .then(function (response) {
            GB_Vk.sendUnity(
              'Vk_OnBannerAdvertisementStatus',
              response && response.result ? 'Shown' : 'Error',
              generation);
          });
      })
      .catch(function () { GB_Vk.sendUnity('Vk_OnBannerAdvertisementStatus', 'Error', generation); });
  },

  Gb_Vk_HideBanner__deps: ['$GB_Vk'],
  Gb_Vk_HideBanner__proxy: 'sync',
  Gb_Vk_HideBanner__sig: 'v',
  Gb_Vk_HideBanner: function () {
    var generation = GB_Vk.generation;
    GB_Vk.supports('VKWebAppHideBannerAd', generation)
      .then(function (supported) {
        if (!supported) {
          GB_Vk.sendUnity('Vk_OnBannerAdvertisementStatus', 'Error', generation);
          return null;
        }

        return GB_Vk.withTimeout(
          GB_Vk.bridgeSend('VKWebAppHideBannerAd', null, generation),
          GB_Vk.capabilityTimeoutMs
        )
          .then(function (response) {
            GB_Vk.sendUnity(
              'Vk_OnBannerAdvertisementStatus',
              response && response.result ? 'Hidden' : 'Error',
              generation);
          });
      })
      .catch(function () { GB_Vk.sendUnity('Vk_OnBannerAdvertisementStatus', 'Error', generation); });
  },

  Gb_Vk_CanAddToFavorites__deps: ['$GB_Vk'],
  Gb_Vk_CanAddToFavorites__proxy: 'sync',
  Gb_Vk_CanAddToFavorites__sig: 'v',
  Gb_Vk_CanAddToFavorites: function () {
    var generation = GB_Vk.generation;
    if (GB_Vk.isFavorite()) {
      GB_Vk.sendUnity(
        'Vk_OnCanAddToFavorites',
        { canShow: false, reason: 'already_added' },
        generation);
      return;
    }

    GB_Vk.supports('VKWebAppAddToFavorites', generation)
      .then(function (supported) {
        GB_Vk.sendUnity('Vk_OnCanAddToFavorites', {
          canShow: supported,
          reason: supported ? '' : 'unsupported'
        }, generation);
      })
      .catch(function () {
        GB_Vk.sendUnity(
          'Vk_OnCanAddToFavorites',
          { canShow: false, reason: 'unsupported' },
          generation);
      });
  },

  Gb_Vk_AddToFavorites__deps: ['$GB_Vk'],
  Gb_Vk_AddToFavorites__proxy: 'sync',
  Gb_Vk_AddToFavorites__sig: 'v',
  Gb_Vk_AddToFavorites: function () {
    var generation = GB_Vk.generation;
    if (GB_Vk.isFavorite()) {
      GB_Vk.sendUnity(
        'Vk_OnAddToFavorites',
        { success: false, error: 'already_added' },
        generation);
      return;
    }

    GB_Vk.supports('VKWebAppAddToFavorites', generation)
      .then(function (supported) {
        if (!supported) throw new Error('unsupported');
        return GB_Vk.bridgeSend('VKWebAppAddToFavorites', null, generation);
      })
      .then(function (response) {
        if (!GB_Vk.isCurrent(generation)) return;
        if (!response || !response.result) throw new Error('favorites_rejected');
        GB_Vk.favoriteAdded = true;
        GB_Vk.sendUnity('Vk_OnAddToFavorites', { success: true }, generation);
      })
      .catch(function (error) {
        GB_Vk.sendUnity(
          'Vk_OnAddToFavorites',
          { success: false, error: GB_Vk.errorText(error) },
          generation);
      });
  },

  Gb_Vk_Dispose__deps: ['$GB_Vk'],
  Gb_Vk_Dispose__sig: 'v',
  Gb_Vk_Dispose: function () {
    GB_Vk.generation += 1;
    GB_Vk.disposed = true;
    GB_Vk.unsubscribe();
    GB_Vk.ready = false;
    GB_Vk.initializing = false;
    GB_Vk.bridge = null;
  }
};

autoAddDeps(LibraryMstVkGamesPlatform, '$GB_Vk');
autoAddDeps(LibraryMstVkGamesPlatform, '$GB_Mst_Util');
mergeInto(LibraryManager.library, LibraryMstVkGamesPlatform);
