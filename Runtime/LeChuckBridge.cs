// MIT License — Copyright (c) 2026 LeChuck Bridge contributors
// See LICENSE.md in the package root for the full license text.

#if UNITY_EDITOR
using System.Collections;
#endif
using UnityEngine;
using UnityEngine.Scripting;

namespace LeChuck
{
    /// <summary>
    /// Hidden persistent receiver for the jslib <c>SendMessage</c> callbacks. Created on demand by
    /// <see cref="LeChuckSDK"/>; forwards everything back to it on the Unity main thread.
    /// </summary>
    [Preserve]
    [AddComponentMenu("")]
    internal sealed class LeChuckBridge : MonoBehaviour
    {
        internal const string ObjectName = "LeChuckBridge(Persistent)";
#if UNITY_WEBGL && !UNITY_EDITOR
        internal const string ReadyMethod = nameof(OnSdkReady);
        internal const string FlushedMethod = nameof(OnFlushed);
        internal const string AuthenticatedMethod = nameof(OnAuthenticated);

        // Invoked from JavaScript via SendMessage; [Preserve] keeps them from being stripped.
        [Preserve] private void OnSdkReady(string uid) => LeChuckSDK.HandleSdkReady(uid);
        [Preserve] private void OnFlushed(string _) => LeChuckSDK.HandleFlushed();
        [Preserve] private void OnAuthenticated(string json) => LeChuckSDK.HandleAuthenticated(json);
#endif

        internal static LeChuckBridge Create()
        {
            var go = new GameObject(ObjectName) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            return go.AddComponent<LeChuckBridge>();
        }

#if UNITY_EDITOR
        internal void SimulateReady(float delaySeconds, LeChuckUser user) => StartCoroutine(SimulateReadyRoutine(delaySeconds, user));

        private static IEnumerator SimulateReadyRoutine(float delaySeconds, LeChuckUser user)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            LeChuckSDK.DispatchReady(user);
        }
#endif
    }
}
