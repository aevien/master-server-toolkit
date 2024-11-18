mergeInto(LibraryManager.library, {
	MstPrompt: function (name, title, defaultValue) {
		const isMobile = /iPhone|iPad|iPod|Android/i.test(navigator.userAgent);

		if (isMobile) {
			const nameStr = UTF8ToString(name);
			const titleStr = UTF8ToString(title);
			const defaultValueStr = UTF8ToString(defaultValue);

			const result = window.prompt(titleStr, defaultValueStr);

			if (result === null) {
				SendMessage(nameStr, 'OnPromptCancel');
			}
			else {
				SendMessage(nameStr, 'OnPromptOk', result);
			}
		}
	},
	MstIsMobile: function () {
		return (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent));
	},
	MstGetQueryString: function () {
		var queryString = window.location.search.replace(/\?/g, '&').replace(/^&/, '?');
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
	},
	MstGetCurrentUrl: function () {
		var url = window.location.href;

		var obj = { currentUrl: url };
		var json = JSON.stringify(obj);

		var bufferSize = lengthBytesUTF8(json) + 1;
		var buffer = _malloc(bufferSize);
		stringToUTF8(json, buffer, bufferSize);
		return buffer;
	}
});
