var LibraryMstItchGameBridge = {
  $GB_Itch_Util: {
    allocString: function (value) {
      value = value || '';
      var n = lengthBytesUTF8(value) + 1;
      var buf = _malloc(n);
      stringToUTF8(value, buf, n);
      return buf;
    },
    getEnv: function (name) {
      var w = (typeof window !== 'undefined') ? window : null;

      if (!w || !w.Itch || !w.Itch.env) {
        return '';
      }

      return w.Itch.env[name] || '';
    }
  },

  Gb_Itch_GetApiKey__proxy: 'sync',
  Gb_Itch_GetApiKey__sig: 'i',
  Gb_Itch_GetApiKey: function () {
    return GB_Itch_Util.allocString(GB_Itch_Util.getEnv('ITCHIO_API_KEY'));
  },

  Gb_Itch_GetApiKeyExpiresAt__proxy: 'sync',
  Gb_Itch_GetApiKeyExpiresAt__sig: 'i',
  Gb_Itch_GetApiKeyExpiresAt: function () {
    return GB_Itch_Util.allocString(GB_Itch_Util.getEnv('ITCHIO_API_KEY_EXPIRES_AT'));
  },

  Gb_Itch_OpenOAuth__proxy: 'sync',
  Gb_Itch_OpenOAuth__sig: 'iiiii',
  Gb_Itch_OpenOAuth: function (clientIdPtr, scopePtr, redirectUriPtr, statePtr) {
    var clientId = UTF8ToString(clientIdPtr);
    var scope = UTF8ToString(scopePtr);
    var redirectUri = UTF8ToString(redirectUriPtr);
    var state = UTF8ToString(statePtr);

    if (!clientId || !redirectUri) {
      return 0;
    }

    var url = 'https://itch.io/user/oauth'
      + '?client_id=' + encodeURIComponent(clientId)
      + '&scope=' + encodeURIComponent(scope || 'profile:me')
      + '&redirect_uri=' + encodeURIComponent(redirectUri)
      + '&response_type=token';

    if (state) {
      url += '&state=' + encodeURIComponent(state);
    }

    if (typeof window !== 'undefined') {
      window.open(url, '_blank', 'noopener,noreferrer');
      return 1;
    }

    return 0;
  }
};

autoAddDeps(LibraryMstItchGameBridge, '$GB_Itch_Util');
mergeInto(LibraryManager.library, LibraryMstItchGameBridge);
