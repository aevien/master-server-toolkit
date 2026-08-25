const LibraryWebSockets = {
    $webSocketInstances: [],

    MstSocketCreate: function (url, maxMessageBytes, maxQueuedMessages, maxQueuedBytes) {
        var str = UTF8ToString(url);

        var socket = {
            socket: new WebSocket(str),
            buffer: new Uint8Array(0),
            error: null,
            code: 0,
            messages: [],
            queuedCount: 0,
            queuedBytes: 0,
            acceptingMessages: true
        }

        socket.rejectMessages = function (reason) {
            socket.error = reason;
            socket.code = 1009;
            socket.acceptingMessages = false;
            socket.messages = [];
            socket.queuedCount = 0;
            socket.queuedBytes = 0;

            if (socket.socket.readyState === WebSocket.OPEN ||
                socket.socket.readyState === WebSocket.CONNECTING) {
                socket.socket.close(1009, reason);
            }
        };

        socket.tryReserveMessage = function (length) {
            if (!socket.acceptingMessages)
                return false;

            if (length < 0 || length > maxMessageBytes) {
                socket.rejectMessages("Message payload is too large");
                return false;
            }

            if (socket.queuedCount + 1 > maxQueuedMessages ||
                socket.queuedBytes + length > maxQueuedBytes) {
                socket.rejectMessages("Incoming message backlog is too large");
                return false;
            }

            socket.queuedCount++;
            socket.queuedBytes += length;
            return true;
        };

        socket.socket.binaryType = 'arraybuffer';

        socket.socket.onmessage = function (e) {
            // MST expects binary packets. Text messages are ignored.
            if (e.data instanceof Blob) {
                var blobLength = e.data.size;
                if (!socket.tryReserveMessage(blobLength))
                    return;

                var reader = new FileReader();
                reader.addEventListener("loadend", function () {
                    if (!socket.acceptingMessages)
                        return;

                    if (!(reader.result instanceof ArrayBuffer) ||
                        reader.result.byteLength !== blobLength) {
                        socket.queuedCount--;
                        socket.queuedBytes -= blobLength;
                        socket.rejectMessages("Failed to read incoming message");
                        return;
                    }

                    var array = new Uint8Array(reader.result);
                    socket.messages.push(array);
                });

                try {
                    reader.readAsArrayBuffer(e.data);
                } catch (error) {
                    socket.queuedCount--;
                    socket.queuedBytes -= blobLength;
                    socket.rejectMessages("Failed to read incoming message");
                }
            } else if (e.data instanceof ArrayBuffer) {
                if (!socket.tryReserveMessage(e.data.byteLength))
                    return;

                var array = new Uint8Array(e.data);
                socket.messages.push(array);
            }
        };

        socket.socket.onclose = function (e) {
            socket.code = e.code;
            socket.acceptingMessages = false;

            if (e.code != 1000) {
                if (e.reason != null && e.reason.length > 0)
                    socket.error = e.reason;
                else {
                    switch (e.code) {
						case 1001:
							socket.error = "Endpoint going away.";
							break;
						case 1002:
							socket.error = "Protocol error.";
							break;
						case 1003:
							socket.error = "Unsupported message.";
							break;
						case 1005:
							socket.error = "No status.";
							break;
						case 1006:
							socket.error = "Abnormal disconnection.";
							break;
						case 1009:
							socket.error = "Data frame too large.";
							break;
						default:
							socket.error = "Error " + e.code;
                    }
                }
            }
        }
        var instance = webSocketInstances.push(socket) - 1;
        return instance;
    },

    MstSocketState: function (socketInstance) {
        var socket = webSocketInstances[socketInstance];
        if (socket == null)
            return WebSocket.CLOSED;

        return socket.socket.readyState;
    },

    MstSocketCode: function (socketInstance) {
        var socket = webSocketInstances[socketInstance];
        if (socket == null)
            return 1000;

        return socket.code;
    },

    MstSocketError: function (socketInstance, ptr, bufsize) {
        var socket = webSocketInstances[socketInstance];
        if (socket == null)
            return 0;

        if (socket.error == null)
            return 0;

        if (bufsize <= 0)
            return 0;

        return stringToUTF8(socket.error, ptr, bufsize);
    },

    MstSocketSend: function (socketInstance, ptr, length) {
        var socket = webSocketInstances[socketInstance];
        if (socket == null)
            return;

        if (socket.socket.readyState !== WebSocket.OPEN)
            return;

        socket.socket.send(HEAPU8.buffer.slice(ptr, ptr + length));
    },

    MstSocketRecvLength: function (socketInstance) {
        var socket = webSocketInstances[socketInstance];
        if (socket == null)
            return 0;

        if (socket.messages.length == 0)
            return 0;
        return socket.messages[0].length;
    },

    MstSocketRecv: function (socketInstance, ptr, length) {
        var socket = webSocketInstances[socketInstance];
        if (socket == null)
            return 0;

        if (socket.messages.length == 0)
            return 0;
        if (socket.messages[0].length > length)
            return 0;

        var message = socket.messages.shift();
        socket.queuedCount--;
        socket.queuedBytes -= message.length;
        HEAPU8.set(message, ptr);
    },

    MstSocketClose: function (socketInstance, code, reason) {
        var socket = webSocketInstances[socketInstance];
        if (socket == null)
            return;

        var closeReason = reason ? UTF8ToString(reason) : '';
        socket.code = code;
        socket.acceptingMessages = false;
        socket.messages = [];
        socket.queuedCount = 0;
        socket.queuedBytes = 0;

        if (socket.socket.readyState === WebSocket.OPEN ||
            socket.socket.readyState === WebSocket.CONNECTING) {
            socket.socket.close(code, closeReason);
        }
    },

    MstSocketRelease: function (socketInstance) {
        var socket = webSocketInstances[socketInstance];
        if (socket == null)
            return;

        socket.code = 1000;
        socket.acceptingMessages = false;
        socket.messages = [];
        socket.queuedCount = 0;
        socket.queuedBytes = 0;

        if (socket.socket.readyState === WebSocket.OPEN ||
            socket.socket.readyState === WebSocket.CONNECTING) {
            socket.socket.close(1000, "WebSocket released");
        }

        socket.socket.onmessage = null;
        socket.socket.onclose = null;
        socket.socket.onerror = null;
        socket.socket.onopen = null;
        webSocketInstances[socketInstance] = null;
    },

    MstAlert:function(msg){
        alert(msg);
    }
};

autoAddDeps(LibraryWebSockets, '$webSocketInstances');
mergeInto(LibraryManager.library, LibraryWebSockets);
