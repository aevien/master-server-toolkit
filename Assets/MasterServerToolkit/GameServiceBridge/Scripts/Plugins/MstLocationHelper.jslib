var LibraryMstLocationHelper = {
  $mstLocationHelper: {
    getUrlParams: function () {
      var out = {};
      if (typeof window !== 'undefined' && window.location && window.location.search) {
        var usp = new URLSearchParams(window.location.search);
        usp.forEach(function (v, k) { out[k] = v; });
      }
      return out;
    }
  }
};

mergeInto(LibraryManager.library, LibraryMstLocationHelper);