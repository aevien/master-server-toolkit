mergeInto(LibraryManager.library, {
  $MstSecurityDecodeBase64: function (value) {
    var binary = atob(value);
    var bytes = new Uint8Array(binary.length);
    var index;

    for (index = 0; index < binary.length; index++) {
      bytes[index] = binary.charCodeAt(index);
    }

    return bytes;
  },

  $MstSecurityEncodeBase64: function (bytes) {
    var binary = "";
    var chunkSize = 0x8000;
    var offset;

    for (offset = 0; offset < bytes.length; offset += chunkSize) {
      var chunk = bytes.subarray(offset, Math.min(offset + chunkSize, bytes.length));
      binary += String.fromCharCode.apply(null, chunk);
    }

    return btoa(binary);
  },

  $MstSecurityEncodeError: function (error) {
    var message = error && error.message ? error.message : String(error);
    return MstSecurityEncodeBase64(new TextEncoder().encode(message));
  },

  MstSecurity_EncryptForMaster__deps: [
    "$MstSecurityDecodeBase64",
    "$MstSecurityEncodeBase64",
    "$MstSecurityEncodeError"
  ],

  MstSecurity_EncryptForMaster: function (
    bridgeObjectNamePointer,
    requestIdPointer,
    publicKeyPointer,
    plaintextPointer,
    associatedDataPointer) {
    var bridgeObjectName = UTF8ToString(bridgeObjectNamePointer);
    var requestId = UTF8ToString(requestIdPointer);
    var publicKey = MstSecurityDecodeBase64(UTF8ToString(publicKeyPointer));
    var plaintext = MstSecurityDecodeBase64(UTF8ToString(plaintextPointer));
    var associatedData = MstSecurityDecodeBase64(UTF8ToString(associatedDataPointer));
    var dataKey = new Uint8Array(32);
    var nonce = new Uint8Array(12);

    crypto.getRandomValues(dataKey);
    crypto.getRandomValues(nonce);

    Promise.all([
      crypto.subtle.importKey(
        "raw",
        dataKey,
        { name: "AES-GCM" },
        false,
        ["encrypt"]),
      crypto.subtle.importKey(
        "spki",
        publicKey,
        { name: "RSA-OAEP", hash: "SHA-256" },
        false,
        ["encrypt"])
    ]).then(function (keys) {
      return Promise.all([
        crypto.subtle.encrypt(
          {
            name: "AES-GCM",
            iv: nonce,
            additionalData: associatedData,
            tagLength: 128
          },
          keys[0],
          plaintext),
        crypto.subtle.encrypt(
          { name: "RSA-OAEP" },
          keys[1],
          dataKey)
      ]);
    }).then(function (results) {
      var combined = new Uint8Array(results[0]);
      var cipherText = combined.subarray(0, combined.length - 16);
      var authenticationTag = combined.subarray(combined.length - 16);
      var encryptedDataKey = new Uint8Array(results[1]);
      var payload = requestId + "|1|" +
        MstSecurityEncodeBase64(encryptedDataKey) + "|" +
        MstSecurityEncodeBase64(nonce) + "|" +
        MstSecurityEncodeBase64(cipherText) + "|" +
        MstSecurityEncodeBase64(authenticationTag);
      SendMessage(bridgeObjectName, "OnMstSecurityResult", payload);
    }).catch(function (error) {
      SendMessage(
        bridgeObjectName,
        "OnMstSecurityResult",
        requestId + "|0|" + MstSecurityEncodeError(error));
    });
  }
});
