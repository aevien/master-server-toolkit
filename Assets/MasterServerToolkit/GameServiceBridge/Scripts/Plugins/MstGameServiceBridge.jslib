mergeInto(LibraryManager.library, {
	MstGetPlatformId: function() {
		const id = window.gameBridge.platform.Id;
		var bufferSize = lengthBytesUTF8(id) + 1;
		var buffer = _malloc(bufferSize);
		stringToUTF8(id, buffer, bufferSize);
		return buffer;
	}
});
