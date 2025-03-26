const libraryWebGlGameBridge = {
    $gamePlatform: {
        /** Gets url query string params after ? */
        getUrlParams: function () {
            var urlParams = new URLSearchParams(window.location.search);
            var paramsObject = {};

            urlParams.forEach((value, key) => {
                paramsObject[key] = value;
            });

            return paramsObject;
        }
    },

    MstGetPlatformId: function() {
        const url = window.location.href;
		const hash = window.location.hash;
        const query = gamePlatform.getUrlParams();
        let platformId = '';

        if (query.pw3_auth) {
            platformId = 'PlayWeb3';
        } else if (url.includes("itch.zone")) {
            platformId = 'Itch';
        } else if (url.includes("yandex.net") || window.location.hash.includes('yandex')) {
            platformId = 'YandexGames';
        } else if (url.includes("play.unity.com")) {
            platformId = 'PlayUnity';
        } else {
            platformId = 'Self';
        }

        var bufferSize = lengthBytesUTF8(platformId) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(platformId, buffer, bufferSize);
        return buffer;
    },

    MstAnalyticsEvent: function(eventData) {
        if (analyticsEvent) {
            analyticsEvent(UTF8ToString(eventData));
        }
    }
};

autoAddDeps(libraryWebGlGameBridge, '$gamePlatform');
mergeInto(LibraryManager.library, libraryWebGlGameBridge);
