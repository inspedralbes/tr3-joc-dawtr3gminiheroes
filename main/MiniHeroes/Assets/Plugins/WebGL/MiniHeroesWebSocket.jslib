mergeInto(LibraryManager.library, {
  MHWsCreate: function (urlPtr, gameObjectPtr) {
    var url = UTF8ToString(urlPtr);
    var go = UTF8ToString(gameObjectPtr);

    if (!Module.MHWs) {
      Module.MHWs = { nextId: 1, sockets: {} };
    }

    var id = Module.MHWs.nextId++;
    Module.MHWs.sockets[id] = { url: url, go: go, ws: null };
    return id;
  },

  MHWsConnect: function (id) {
    if (!Module.MHWs || !Module.MHWs.sockets[id]) return;
    var entry = Module.MHWs.sockets[id];

    try {
      var ws = new WebSocket(entry.url);
      entry.ws = ws;

      ws.onopen = function () {
        if (typeof SendMessage !== "undefined") {
          SendMessage(entry.go, "OnWsOpen", id.toString());
        }
      };

      ws.onmessage = function (evt) {
        if (typeof SendMessage !== "undefined") {
          SendMessage(entry.go, "OnWsMessage", id.toString() + "|" + evt.data);
        }
      };

      ws.onerror = function () {
        if (typeof SendMessage !== "undefined") {
          SendMessage(entry.go, "OnWsError", id.toString() + "|WebSocket error");
        }
      };

      ws.onclose = function (evt) {
        if (typeof SendMessage !== "undefined") {
          SendMessage(entry.go, "OnWsClose", id.toString() + "|" + (evt && evt.code ? evt.code.toString() : "0"));
        }
      };
    } catch (e) {
      if (typeof SendMessage !== "undefined") {
        SendMessage(entry.go, "OnWsError", id.toString() + "|" + (e && e.message ? e.message : "WebSocket exception"));
      }
    }
  },

  MHWsSend: function (id, msgPtr) {
    if (!Module.MHWs || !Module.MHWs.sockets[id]) return;
    var entry = Module.MHWs.sockets[id];
    if (!entry.ws) return;
    var msg = UTF8ToString(msgPtr);
    try {
      if (entry.ws.readyState === 1) {
        entry.ws.send(msg);
      }
    } catch (e) {
      if (typeof SendMessage !== "undefined") {
        SendMessage(entry.go, "OnWsError", id.toString() + "|Send failed");
      }
    }
  },

  MHWsClose: function (id) {
    if (!Module.MHWs || !Module.MHWs.sockets[id]) return;
    var entry = Module.MHWs.sockets[id];
    try {
      if (entry.ws) {
        entry.ws.close();
      }
    } catch (e) {
      // Ignore.
    }
    delete Module.MHWs.sockets[id];
  }
});

