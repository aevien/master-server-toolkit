var LibraryWebSockets = {
    $webSocketInstances: [],

    MstSocketCreate: function (url) {
        var str = UTF8ToString(url);

        var socket = {
            socket: new WebSocket(str),
            buffer: new Uint8Array(0),
            error: null,
			code:-1,
            messages: []
        }

        socket.socket.binaryType = 'arraybuffer';

        socket.socket.onmessage = function (e) {
            // Todo: handle other data types?
            if (e.data instanceof Blob) {
                var reader = new FileReader();
                reader.addEventListener("loadend", function () {
                    var array = new Uint8Array(reader.result);
                    socket.messages.push(array);
                });
                reader.readAsArrayBuffer(e.data);
            } else if (e.data instanceof ArrayBuffer) {
                var array = new Uint8Array(e.data);
                socket.messages.push(array);
            }
        };

        socket.socket.onclose = function (e) {
            if (e.code != 1000) {
				socket.code = e.code;
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
        return socket.socket.readyState;
    },

    MstSocketCode: function (socketInstance) {
        var socket = webSocketInstances[socketInstance];
        return socket.socket.code;
    },

    MstSocketError: function (socketInstance, ptr, bufsize) {
        var socket = webSocketInstances[socketInstance];
        if (socket.error == null)
            return 0;
        var str = socket.error.slice(0, Math.max(0, bufsize - 1));
        writeStringToMemory(str, ptr, false);
        return 1;
    },

    MstSocketSend: function (socketInstance, ptr, length) {
        var socket = webSocketInstances[socketInstance];
        socket.socket.send(HEAPU8.buffer.slice(ptr, ptr + length));
    },

    MstSocketRecvLength: function (socketInstance) {
        var socket = webSocketInstances[socketInstance];
        if (socket.messages.length == 0)
            return 0;
        return socket.messages[0].length;
    },

    MstSocketRecv: function (socketInstance, ptr, length) {
        var socket = webSocketInstances[socketInstance];
        if (socket.messages.length == 0)
            return 0;
        if (socket.messages[0].length > length)
            return 0;
        HEAPU8.set(socket.messages[0], ptr);
        socket.messages = socket.messages.slice(1);
    },

    MstSocketClose: function (socketInstance, code, reason) {
        var socket = webSocketInstances[socketInstance];
        socket.socket.close();
		console.log('Client closed connection with ' + code + ' by reason: ' + reason);
    },
	
	MstAlert:function(msg){
		alert(msg);
	}
};

autoAddDeps(LibraryWebSockets, '$webSocketInstances');
mergeInto(LibraryManager.library, LibraryWebSockets);