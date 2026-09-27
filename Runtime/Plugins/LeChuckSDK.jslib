// ─────────────────────────────────────────────────────────────────────────────
// LeChuckSDK.jslib — WebGL bridge between Unity C# and the Minijuegos/Miniplay
// LeChuck JS SDK (https://ssl.minijuegosgratis.com/lechuck/js/latest.js).
//
// Fully generic: the game id is passed at init, no ids or secrets are bundled.
// The C# side (LeChuckSDK.cs) is the only supported entry point.
//
// MIT License — Copyright (c) 2026 LeChuck Bridge contributors
// ─────────────────────────────────────────────────────────────────────────────
mergeInto(LibraryManager.library, {

  // Shared bridge state, created lazily at runtime. jslib top-level code runs
  // at build time inside Emscripten (no `window`), so it must live in a helper.
  $lechuckState: function () {
    if (!window.__lechuckBridge) {
      window.__lechuckBridge = { ready: false, user: null, instance: null, gameId: 0, debug: false, error: '', readyCb: null };
    }
    return window.__lechuckBridge;
  },

  // Returns a heap-allocated UTF-8 copy of a JS string (0 for null). The C#
  // extern declares a `string` return, so IL2CPP copies and frees the buffer.
  $lechuckAllocString: function (str) {
    if (str === null || str === undefined) return 0;
    var size = lengthBytesUTF8(str) + 1;
    var ptr = _malloc(size);
    stringToUTF8(str, ptr, size);
    return ptr;
  },

  // Injects the vendor SDK script (once) and constructs the API instance.
  // gameIdStr: decimal game id from the Minijuegos developer panel.
  // debugFlag: 1 to enable SDK debug logging.
  LeChuckBridge_Init__deps: ['$lechuckState'],
  LeChuckBridge_Init: function (gameIdStr, debugFlag) {
    var B = lechuckState();
    var gameId = parseInt(UTF8ToString(gameIdStr), 10);
    if (!gameId || isNaN(gameId)) { B.error = 'invalid game id'; return; }
    B.gameId = gameId;
    B.debug = debugFlag === 1;

    function construct() {
      try {
        // Some SDK builds auto-create a global `lechuck` from URL params;
        // prefer it when present (battle-tested pattern), fall back to ours.
        B.instance = (typeof lechuck !== 'undefined') ? lechuck
          : new LeChuckAPI({ id: B.gameId, debug: B.debug });
      } catch (e) { B.error = 'construct: ' + e; }
      try {
        LeChuckAPI.events.onApiReady(function () {
          B.ready = true;
          try {
            var lc = B.instance || (typeof lechuck !== 'undefined' ? lechuck : null);
            if (!lc) return;
            var uid = lc.user && lc.user.getId ? lc.user.getId() : null;
            var tok = lc.user && lc.user.getToken ? lc.user.getToken() : null;
            var name = (lc.user && lc.user.getUid ? lc.user.getUid() : null)
              || (lc.user && lc.user.getName ? lc.user.getName() : null)
              || (uid ? 'Player_' + uid : null);
            // Strings only: the C# side parses this with JsonUtility.
            B.user = { uid: uid ? String(uid) : null, token: tok ? String(tok) : null, name: name ? String(name) : null };
          } catch (e) { B.error = 'user: ' + e; }
          if (B.readyCb) { SendMessage(B.readyCb[0], B.readyCb[1], B.user ? (B.user.uid || '') : ''); }
        });
      } catch (e) { B.error = 'onApiReady: ' + e; }
    }

    if (typeof LeChuckAPI !== 'undefined') { construct(); return; }
    var s = document.createElement('script');
    s.src = 'https://ssl.minijuegosgratis.com/lechuck/js/latest.js';
    s.async = true;
    s.onload = construct;
    s.onerror = function () { B.error = 'failed to load SDK script'; };
    document.head.appendChild(s);
  },

  // 1 once the SDK fired onApiReady (user may still be null when the game is
  // unpublished: Minijuegos only injects mp_api_user_id in published games).
  LeChuckBridge_IsReady__deps: ['$lechuckState'],
  LeChuckBridge_IsReady: function () {
    return lechuckState().ready ? 1 : 0;
  },

  // True when running inside a Minijuegos/Miniplay iframe (regardless of ready).
  LeChuckBridge_IsEmbedded: function () {
    try {
      var a = window.location.ancestorOrigins;
      if (a && a.length) {
        for (var i = 0; i < a.length; i++) {
          if (a[i].indexOf('miniplay.com') >= 0 || a[i].indexOf('minijuegos.com') >= 0) return 1;
        }
      }
      if (document.referrer && (document.referrer.indexOf('miniplay.com') >= 0 || document.referrer.indexOf('minijuegos.com') >= 0)) return 1;
    } catch (e) {}
    return 0;
  },

  // Returns JSON {uid, token, name} or null when not signed in / not ready.
  LeChuckBridge_GetUserJson__deps: ['$lechuckState', '$lechuckAllocString'],
  LeChuckBridge_GetUserJson: function () {
    var B = lechuckState();
    if (!B.user || !B.user.uid) return 0;
    return lechuckAllocString(JSON.stringify(B.user));
  },

  // 'miniplay' | 'minijuegos' | 'unknown'.
  LeChuckBridge_GetPortal__deps: ['$lechuckAllocString'],
  LeChuckBridge_GetPortal: function () {
    var portal = 'unknown';
    try {
      var a = window.location.ancestorOrigins;
      var r = document.referrer || '';
      if (a && a.length && a[0].indexOf('miniplay.com') >= 0) portal = 'miniplay';
      else if (a && a.length && a[0].indexOf('minijuegos.com') >= 0) portal = 'minijuegos';
      else if (r.indexOf('miniplay.com') >= 0) portal = 'miniplay';
      else if (r.indexOf('minijuegos.com') >= 0) portal = 'minijuegos';
    } catch (e) {}
    return lechuckAllocString(portal);
  },

  // One score per game session (SDK contract).
  LeChuckBridge_SetScore__deps: ['$lechuckState'],
  LeChuckBridge_SetScore: function (scoreStr) {
    var B = lechuckState();
    if (!B.ready) return;
    var lc = B.instance || (typeof lechuck !== 'undefined' ? lechuck : null);
    if (lc && lc.set_score) { try { lc.set_score(parseFloat(UTF8ToString(scoreStr))); } catch (e) {} }
  },

  // REPLACE semantics: send accumulated totals, never deltas.
  LeChuckBridge_SetStat__deps: ['$lechuckState'],
  LeChuckBridge_SetStat: function (keyStr, valueStr) {
    var B = lechuckState();
    if (!B.ready) return;
    var lc = B.instance || (typeof lechuck !== 'undefined' ? lechuck : null);
    if (lc && lc.stat && lc.stat.put) {
      try { lc.stat.put(function () {}, UTF8ToString(keyStr), parseFloat(UTF8ToString(valueStr))); } catch (e) {}
    }
  },

  // Unlock + boolean stat guard, with per-browser double-unlock protection.
  LeChuckBridge_UnlockAchievement__deps: ['$lechuckState'],
  LeChuckBridge_UnlockAchievement: function (uidStr) {
    var B = lechuckState();
    var uid = UTF8ToString(uidStr);
    if (!uid) return;
    try {
      var done = {};
      try { JSON.parse(localStorage.getItem('__lechuck_unlocked_v1') || '[]').forEach(function (k) { done[k] = 1; }); } catch (e) {}
      if (done[uid]) return;
      done[uid] = 1;
      localStorage.setItem('__lechuck_unlocked_v1', JSON.stringify(Object.keys(done)));
    } catch (e) {}
    var lc = B.instance || (typeof lechuck !== 'undefined' ? lechuck : null);
    if (lc && lc.unlockAchievement) { try { lc.unlockAchievement(uid); } catch (e) {} }
    if (lc && lc.stat && lc.stat.put) { try { lc.stat.put(function () {}, uid, 1); } catch (e) {} }
  },

  // Registers a Unity object/method to receive the ready callback (uid or '').
  LeChuckBridge_SetReadyCallback__deps: ['$lechuckState'],
  LeChuckBridge_SetReadyCallback: function (goName, funcName) {
    var B = lechuckState();
    B.readyCb = [UTF8ToString(goName), UTF8ToString(funcName)];
    if (B.ready) { SendMessage(B.readyCb[0], B.readyCb[1], B.user ? (B.user.uid || '') : ''); }
  },

  // Optional: POST {miniplay_id, token} to a developer-provided auth endpoint.
  // Response body is forwarded verbatim to the Unity method as a string.
  LeChuckBridge_Authenticate__deps: ['$lechuckState'],
  LeChuckBridge_Authenticate: function (urlStr, goName, funcName) {
    var B = lechuckState();
    var url = UTF8ToString(urlStr), target = [UTF8ToString(goName), UTF8ToString(funcName)];
    var user = B.user;
    if (!user || !user.uid) { SendMessage(target[0], target[1], JSON.stringify({ error: 'not signed in' })); return; }
    fetch(url, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ miniplay_id: user.uid, token: user.token })
    }).then(function (r) { return r.text(); })
      .then(function (t) { SendMessage(target[0], target[1], t || '{}'); })
      .catch(function (e) { SendMessage(target[0], target[1], JSON.stringify({ error: String(e) })); });
  },

  // Flush guard: the SDK sends data over async HTTP; give it a beat before the
  // game resets/reloads, then notify Unity. This is a pragmatic best-effort
  // window (default 500 ms, configurable in the settings asset).
  LeChuckBridge_Flush: function (ms, goName, funcName) {
    var target = [UTF8ToString(goName), UTF8ToString(funcName)];
    setTimeout(function () { SendMessage(target[0], target[1], '1'); }, ms);
  },

  // Diagnostics: last bridge error or null.
  LeChuckBridge_GetError__deps: ['$lechuckState', '$lechuckAllocString'],
  LeChuckBridge_GetError: function () {
    var B = lechuckState();
    return B.error ? lechuckAllocString(B.error) : 0;
  }
});
