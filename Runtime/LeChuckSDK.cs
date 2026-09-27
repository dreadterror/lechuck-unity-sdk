// MIT License — Copyright (c) 2026 LeChuck Bridge contributors
// See LICENSE.md in the package root for the full license text.

using System;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace LeChuck
{
    /// <summary>
    /// Unity entry point for the Minijuegos / Miniplay LeChuck JS SDK.
    /// </summary>
    /// <remarks>
    /// All calls are real only in WebGL player builds. Everywhere else they are silent no-ops
    /// (callbacks still fire), with an optional simulated user in the Editor
    /// (<see cref="LeChuckSettings.simulateInEditor"/>). All events and callbacks run on the Unity main thread.
    /// </remarks>
    public static class LeChuckSDK
    {
        private const string UnknownPortal = "unknown";
        private const string NoAuthUrlResponse = "{\"error\":\"no auth url\"}";
        private const float DefaultFlushDelayMs = 500f;

        private static LeChuckSettings s_Settings;
        private static LeChuckBridge s_Bridge;
        private static bool s_ReadyDispatched;
        private static LeChuckUser s_ReadyUser;
        private static Action<LeChuckUser> s_OnReady;

#if UNITY_WEBGL && !UNITY_EDITOR
        // The jslib answers Flush/Authenticate through one fixed method each, in call order.
        private static readonly Queue<Action> s_PendingFlushes = new Queue<Action>();
        private static readonly Queue<Action<string>> s_PendingAuths = new Queue<Action<string>>();
#else
        private const string NotWebGLAuthResponse = "{\"error\":\"webgl only\"}";
#endif
#if UNITY_EDITOR
        private const float SimulatedReadyDelaySeconds = 0.1f;
#endif

        /// <summary>
        /// Raised once when the SDK is ready. The user may be a guest (<see cref="LeChuckUser.IsSignedIn"/> false),
        /// e.g. while the game is unpublished. Handlers added after that moment are invoked immediately.
        /// </summary>
        public static event Action<LeChuckUser> OnReady
        {
            add
            {
                s_OnReady += value;
                if (s_ReadyDispatched && value != null) SafeInvoke(value, s_ReadyUser);
            }
            remove => s_OnReady -= value;
        }

        /// <summary>True after <see cref="Init"/> accepted a settings asset.</summary>
        public static bool IsInitialized => s_Settings != null;

        /// <summary>True once the vendor SDK reported ready (or the Editor simulation fired).</summary>
        public static bool IsReady
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return LeChuckBridge_IsReady() == 1;
#else
                return s_ReadyDispatched;
#endif
            }
        }

        /// <summary>True when the game runs inside a Minijuegos/Miniplay iframe. Always false outside WebGL.</summary>
        public static bool IsEmbedded
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return LeChuckBridge_IsEmbedded() == 1;
#else
                return false;
#endif
            }
        }

        /// <summary>Last bridge error reported by the JavaScript side, or null.</summary>
        public static string LastError
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return LeChuckBridge_GetError();
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// Loads the vendor SDK with the game id from <paramref name="settings"/>. Call once, as early as possible;
        /// further calls are ignored.
        /// </summary>
        /// <param name="settings">Settings asset with at least a valid <see cref="LeChuckSettings.gameId"/>.</param>
        public static void Init(LeChuckSettings settings)
        {
            if (IsInitialized) return;
            if (settings == null)
            {
                Debug.LogError("[LeChuck] Init: settings asset is null.");
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            if (settings.gameId <= 0)
            {
                Debug.LogError("[LeChuck] Init: gameId must be greater than zero (see the Minijuegos developer panel).");
                return;
            }
            s_Settings = settings;
            // The receiver must exist first: SetReadyCallback fires synchronously if the SDK is already ready.
            EnsureBridge();
            LeChuckBridge_Init(settings.gameId.ToString(CultureInfo.InvariantCulture), settings.debug ? 1 : 0);
            LeChuckBridge_SetReadyCallback(LeChuckBridge.ObjectName, LeChuckBridge.ReadyMethod);
#else
            s_Settings = settings;
#if UNITY_EDITOR
            if (settings.simulateInEditor && Application.isPlaying)
            {
                string id = settings.simulatedUserId;
                var user = string.IsNullOrEmpty(id)
                    ? default
                    : new LeChuckUser { Id = id, Token = string.Empty, Name = "Player_" + id };
                EnsureBridge().SimulateReady(SimulatedReadyDelaySeconds, user);
            }
#endif
#endif
        }

        /// <summary>Current user, or null when not ready, not signed in, or outside WebGL (unless simulated).</summary>
        public static LeChuckUser? GetUser()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return ParseUser(LeChuckBridge_GetUserJson());
#else
            return s_ReadyDispatched && s_ReadyUser.IsSignedIn ? s_ReadyUser : (LeChuckUser?)null;
#endif
        }

        /// <summary>Hosting portal: <c>"miniplay"</c>, <c>"minijuegos"</c> or <c>"unknown"</c>.</summary>
        public static string DetectPortal()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return LeChuckBridge_GetPortal() ?? UnknownPortal;
#else
            return UnknownPortal;
#endif
        }

        /// <summary>Submits the session score. The SDK accepts one score per game session.</summary>
        /// <param name="score">Final score. NaN and infinities are ignored.</param>
        public static void SetScore(double score)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (IsFinite(score)) LeChuckBridge_SetScore(FormatNumber(score));
#endif
        }

        /// <summary>Stores a stat. Stats use REPLACE semantics: send accumulated totals, never deltas.</summary>
        /// <param name="key">Stat uid as registered in the developer panel.</param>
        /// <param name="value">Total value. NaN and infinities are ignored.</param>
        public static void SetStat(string key, double value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(key) && IsFinite(value)) LeChuckBridge_SetStat(key, FormatNumber(value));
#endif
        }

        /// <summary>
        /// Unlocks an achievement (and sets its boolean stat). Repeated unlocks of the same uid are
        /// skipped per browser.
        /// </summary>
        /// <param name="uid">Achievement uid as registered in the developer panel.</param>
        public static void UnlockAchievement(string uid)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(uid)) LeChuckBridge_UnlockAchievement(uid);
#endif
        }

        /// <summary>
        /// Waits <see cref="LeChuckSettings.flushDelayMs"/> so pending SDK requests can complete, then invokes
        /// <paramref name="onFlushed"/>. Call before restarting or reloading the game. Outside WebGL the
        /// callback runs immediately.
        /// </summary>
        /// <param name="onFlushed">Callback invoked on the main thread; may be null.</param>
        public static void Flush(Action onFlushed)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            EnsureBridge();
            s_PendingFlushes.Enqueue(onFlushed);
            float delay = s_Settings != null ? Mathf.Max(0f, s_Settings.flushDelayMs) : DefaultFlushDelayMs;
            LeChuckBridge_Flush(delay, LeChuckBridge.ObjectName, LeChuckBridge.FlushedMethod);
#else
            SafeInvoke(onFlushed);
#endif
        }

        /// <summary>
        /// POSTs <c>{miniplay_id, token}</c> to <see cref="LeChuckSettings.authUrl"/> and returns the raw response body.
        /// </summary>
        /// <param name="onDone">
        /// Receives the response body, or a JSON <c>{"error": ...}</c> when no auth url is configured,
        /// the user is not signed in, the request failed, or the platform is not WebGL.
        /// </param>
        public static void Authenticate(Action<string> onDone)
        {
            string url = s_Settings != null ? s_Settings.authUrl?.Trim() : null;
            if (string.IsNullOrEmpty(url))
            {
                SafeInvoke(onDone, NoAuthUrlResponse);
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            EnsureBridge();
            s_PendingAuths.Enqueue(onDone);
            LeChuckBridge_Authenticate(url, LeChuckBridge.ObjectName, LeChuckBridge.AuthenticatedMethod);
#else
            SafeInvoke(onDone, NotWebGLAuthResponse);
#endif
        }

        internal static void DispatchReady(LeChuckUser user)
        {
            if (s_ReadyDispatched) return;
            s_ReadyDispatched = true;
            s_ReadyUser = user;
            if (s_OnReady == null) return;
            foreach (Delegate handler in s_OnReady.GetInvocationList())
                SafeInvoke((Action<LeChuckUser>)handler, user);
        }

        private static LeChuckBridge EnsureBridge()
        {
            if (s_Bridge == null) s_Bridge = LeChuckBridge.Create();
            return s_Bridge;
        }

        private static void SafeInvoke(Action callback)
        {
            if (callback == null) return;
            try { callback(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private static void SafeInvoke<T>(Action<T> callback, T arg)
        {
            if (callback == null) return;
            try { callback(arg); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // Clears static state when Enter Play Mode runs without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_Settings = null;
            s_Bridge = null;
            s_ReadyDispatched = false;
            s_ReadyUser = default;
            s_OnReady = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            s_PendingFlushes.Clear();
            s_PendingAuths.Clear();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        internal static void HandleSdkReady(string uid)
        {
            LeChuckUser user = default;
            if (!string.IsNullOrEmpty(uid)) user = GetUser() ?? new LeChuckUser { Id = uid, Name = "Player_" + uid };
            DispatchReady(user);
        }

        internal static void HandleFlushed()
        {
            if (s_PendingFlushes.Count > 0) SafeInvoke(s_PendingFlushes.Dequeue());
        }

        internal static void HandleAuthenticated(string json)
        {
            if (s_PendingAuths.Count > 0) SafeInvoke(s_PendingAuths.Dequeue(), json);
        }

        private static LeChuckUser? ParseUser(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            UserJson parsed;
            try { parsed = JsonUtility.FromJson<UserJson>(json); }
            catch (ArgumentException) { return null; }
            if (parsed == null || string.IsNullOrEmpty(parsed.uid)) return null;
            return new LeChuckUser { Id = parsed.uid, Token = parsed.token, Name = parsed.name };
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        // Numbers cross the bridge as invariant strings so doubles keep full precision.
        private static string FormatNumber(double value) => value.ToString("R", CultureInfo.InvariantCulture);

#pragma warning disable CS0649 // Assigned by JsonUtility.
        [Serializable]
        private sealed class UserJson
        {
            public string uid;
            public string token;
            public string name;
        }
#pragma warning restore CS0649

        [DllImport("__Internal")] private static extern void LeChuckBridge_Init(string gameId, int debug);
        [DllImport("__Internal")] private static extern int LeChuckBridge_IsReady();
        [DllImport("__Internal")] private static extern int LeChuckBridge_IsEmbedded();
        // String returns: the jslib allocates UTF-8 with _malloc; IL2CPP copies it and frees the buffer.
        [DllImport("__Internal")] private static extern string LeChuckBridge_GetUserJson();
        [DllImport("__Internal")] private static extern string LeChuckBridge_GetPortal();
        [DllImport("__Internal")] private static extern string LeChuckBridge_GetError();
        [DllImport("__Internal")] private static extern void LeChuckBridge_SetScore(string score);
        [DllImport("__Internal")] private static extern void LeChuckBridge_SetStat(string key, string value);
        [DllImport("__Internal")] private static extern void LeChuckBridge_UnlockAchievement(string uid);
        [DllImport("__Internal")] private static extern void LeChuckBridge_SetReadyCallback(string gameObjectName, string methodName);
        [DllImport("__Internal")] private static extern void LeChuckBridge_Authenticate(string url, string gameObjectName, string methodName);
        [DllImport("__Internal")] private static extern void LeChuckBridge_Flush(float delayMs, string gameObjectName, string methodName);
#endif
    }
}
