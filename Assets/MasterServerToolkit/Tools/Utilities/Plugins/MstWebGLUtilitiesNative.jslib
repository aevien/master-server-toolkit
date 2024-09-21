const MstWebBrowserUtils = {
    GetQueryStringFromBrowser: function () {
        var queryString = window.location.search;
        var params = new URLSearchParams(queryString);

        var obj = {};
        params.forEach((value, key) => {
            obj[key] = value;
        });

        var json = JSON.stringify(obj);

        var bufferSize = lengthBytesUTF8(json) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(json, buffer, bufferSize);
        return buffer;
    }
}

mergeInto(LibraryManager.library, MstWebBrowserUtils);