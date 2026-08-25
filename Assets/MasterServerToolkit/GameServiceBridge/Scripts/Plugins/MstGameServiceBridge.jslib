// ===== WebGLGameBridge.jslib =====
// JavaScript library for Unity WebGL (Emscripten).
// All functions defined in this object are merged into Emscripten's
// LibraryManager and become callable from C# via [DllImport("__Internal")].
//
// Style notes:
// - Use classic `var` and function syntax for maximum compatibility with Unity/Emscripten.
// - Add function metadata (__proxy, __sig) so the runtime marshals types correctly
//   and runs code on the correct thread (main thread for any DOM access).

var LibraryMstWebGlGameBridge = {
  // ------------------------------ UTILITIES -----------------------------------
  $GB_Mst_Util: {
    log:   function (msg) { console.log('[GB_MST] ' + msg); },
    error: function (msg) { console.error('[GB_MST] ' + msg); },
    toErrorString: function (err) {
      if (!err) return '';
      if (typeof err === 'string') return err;
      if (err instanceof Error) return err.message || String(err);
      if (typeof err.message === 'string') return err.message;
      try { return JSON.stringify(err); } catch (_e) { return String(err); }
    },
    UNDEFINED_ERROR: 'undefined'
  },

  // Expose helper on window for convenient debugging (optional but handy).
  $GB_Mst_Util__postset: `
    if (typeof window !== 'undefined') {
      window.GB_Mst_Util = window.GB_Mst_Util || GB_Mst_Util;
    }
  `,

  // ---------------------------- PLATFORM DETECTION ----------------------------
  //
  // Purpose:
  //   Detect where the game is currently running (e.g., Itch, YandexGames, local WebGL).
  //   Returns a C-style string pointer (char*) to be read on the C# side.
  //
  // Why __proxy: 'sync'? DOM access must be on main thread.
  // Why __sig: 'i'? Returns an int pointer.
  MstGetPlatformId__proxy: 'sync',
  MstGetPlatformId__sig: 'i',
  MstGetPlatformId: function () {
    var w = (typeof window !== 'undefined') ? window : null;
    var href   = w && w.location ? (w.location.href   || '') : '';
    var host   = w && w.location ? (w.location.host   || '') : '';
    var hash   = w && w.location ? (w.location.hash   || '') : '';
    var search = w && w.location ? (w.location.search || '') : '';

    function lower(s){ return (s||'').toLowerCase(); }
    function incl(s,a){ return s.indexOf(a) !== -1; }

    function isVkGamesLaunch() {
      try {
        var params = new URLSearchParams(search || '');
        var apiUrl = new URL(params.get('api_url') || '');
        var isVkApi = apiUrl.protocol === 'https:' &&
          (apiUrl.hostname === 'api.vk.ru' || apiUrl.hostname === 'api.vk.com');
        return isVkApi &&
          !!params.get('api_id') &&
          !!params.get('viewer_id') &&
          !!params.get('auth_key') &&
          !!params.get('sign') &&
          !!params.get('sign_keys');
      } catch (_e) {
        return false;
      }
    }

    function isVkPlayLaunch() {
      try {
        var params = new URLSearchParams(search || '');
        return !params.get('api_url') &&
          !!params.get('appid') &&
          !!params.get('uid') &&
          !!params.get('sign');
      } catch (_e) {
        return false;
      }
    }

    var urlL  = lower(href), hostL = lower(host), hashL = lower(hash);
    var platformId = 'Web';

    if (isVkGamesLaunch()) {
      platformId = 'VKGames';
    } else if (isVkPlayLaunch()) {
      platformId = 'VKPlay';
    } else if (incl(hostL, 'itch.io') || incl(urlL, 'itch.zone')) {
      platformId = 'Itch';
    } else if (incl(hostL, 'yandex.net') || incl(urlL, 'yandexgames') || incl(urlL, 'yandex.com/games') || incl(hashL, 'yandex')) {
      platformId = 'YandexGames';
    }

    var n = lengthBytesUTF8(platformId) + 1;
    var buf = _malloc(n);
    stringToUTF8(platformId, buf, n);
    return buf;
  },

  // ----------------------------- ANALYTICS EVENT ------------------------------
  //
  // Purpose: forward payload to page-level analyticsEvent(jsonString) if present.
  // Why __proxy: 'sync'? Touches browser globals. Why __sig: 'vi'? void + char*.
  MstAnalyticsEvent__proxy: 'sync',
  MstAnalyticsEvent__sig: 'vi',
  MstAnalyticsEvent: function (eventDataPtr) {
    var payload = UTF8ToString(eventDataPtr);

    if (typeof analyticsEvent === 'function') {
      try {
        analyticsEvent(payload);
      } catch (e) {
        if (typeof GB_Mst_Util !== 'undefined') {
          GB_Mst_Util.error('analyticsEvent() threw: ' + GB_Mst_Util.toErrorString(e));
        } else {
          console.error('[MstAnalyticsEvent] analyticsEvent() threw:', e);
        }
      }
    } else {
      if (typeof GB_Mst_Util !== 'undefined') {
        GB_Mst_Util.log('analyticsEvent() is not defined on the hosting page.');
      } else {
        console.log('[MstAnalyticsEvent] analyticsEvent() is not defined on the hosting page.');
      }
    }
  }
};

// Make utilities available by default to functions in this library (optional).
autoAddDeps(LibraryMstWebGlGameBridge, '$GB_Mst_Util');

// Merge this library into Emscripten's LibraryManager.
mergeInto(LibraryManager.library, LibraryMstWebGlGameBridge);
