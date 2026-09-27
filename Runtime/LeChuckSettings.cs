// MIT License — Copyright (c) 2026 LeChuck Bridge contributors
// See LICENSE.md in the package root for the full license text.

using UnityEngine;

namespace LeChuck
{
    /// <summary>
    /// Per-game configuration for <see cref="LeChuckSDK"/>. Create one with
    /// <c>Assets &gt; Create &gt; LeChuck &gt; Settings</c> and pass it to <see cref="LeChuckSDK.Init"/>.
    /// </summary>
    /// <remarks>Everything stored here is public client data: never put API keys or secrets in this asset.</remarks>
    [CreateAssetMenu(menuName = "LeChuck/Settings", fileName = "LeChuckSettings")]
    public sealed class LeChuckSettings : ScriptableObject
    {
        /// <summary>Public game id from the Minijuegos developer panel. Must be greater than zero.</summary>
        [Tooltip("Public game id from the Minijuegos developer panel.")]
        public int gameId = 0;

        /// <summary>Enables the vendor SDK debug logging in the browser console.</summary>
        [Tooltip("Enables the LeChuck SDK debug logging in the browser console.")]
        public bool debug = false;

        /// <summary>
        /// Optional endpoint on your own backend used by <see cref="LeChuckSDK.Authenticate"/>.
        /// Leave empty if you do not need backend sessions.
        /// </summary>
        [Tooltip("Optional: your backend endpoint that validates the LeChuck token server-side. Leave empty to disable.")]
        public string authUrl = "";

        /// <summary>Delay, in milliseconds, that <see cref="LeChuckSDK.Flush"/> waits before invoking its callback.</summary>
        [Tooltip("Milliseconds Flush() waits so pending SDK requests can complete before a restart.")]
        [Min(0f)]
        public float flushDelayMs = 500f;

        /// <summary>When true, <see cref="LeChuckSDK.Init"/> fires <see cref="LeChuckSDK.OnReady"/> with a fake user in the Editor.</summary>
        [Tooltip("Editor only: simulate the SDK becoming ready with a fake user.")]
        public bool simulateInEditor = false;

        /// <summary>User id reported by the Editor simulation. Leave empty to simulate a guest (not signed in).</summary>
        [Tooltip("Editor only: simulated user id. Empty simulates a guest.")]
        public string simulatedUserId = "TESTER";
    }
}
