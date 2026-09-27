// MIT License — Copyright (c) 2026 LeChuck Bridge contributors
// See LICENSE.md in the package root for the full license text.

using UnityEditor;
using UnityEngine;

namespace LeChuck.Editor
{
    /// <summary>Inspector for <see cref="LeChuckSettings"/>: grouped fields, validation hints and a Play Mode test panel.</summary>
    [CustomEditor(typeof(LeChuckSettings))]
    internal sealed class LeChuckSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _gameId;
        private SerializedProperty _debug;
        private SerializedProperty _authUrl;
        private SerializedProperty _flushDelayMs;
        private SerializedProperty _simulateInEditor;
        private SerializedProperty _simulatedUserId;

        private void OnEnable()
        {
            _gameId = serializedObject.FindProperty(nameof(LeChuckSettings.gameId));
            _debug = serializedObject.FindProperty(nameof(LeChuckSettings.debug));
            _authUrl = serializedObject.FindProperty(nameof(LeChuckSettings.authUrl));
            _flushDelayMs = serializedObject.FindProperty(nameof(LeChuckSettings.flushDelayMs));
            _simulateInEditor = serializedObject.FindProperty(nameof(LeChuckSettings.simulateInEditor));
            _simulatedUserId = serializedObject.FindProperty(nameof(LeChuckSettings.simulatedUserId));
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Game", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_gameId);
            if (_gameId.intValue <= 0)
                EditorGUILayout.HelpBox("Set the public game id from the Minijuegos developer panel.", MessageType.Warning);
            EditorGUILayout.PropertyField(_debug);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Backend (optional)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_authUrl);
            string url = _authUrl.stringValue.Trim();
            if (url.Length > 0 && !url.StartsWith("https://"))
                EditorGUILayout.HelpBox("Use an https:// endpoint: the token is sent in the request body.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_flushDelayMs);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor testing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_simulateInEditor);
            using (new EditorGUI.DisabledScope(!_simulateInEditor.boolValue))
                EditorGUILayout.PropertyField(_simulatedUserId);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawRuntimeStatus();
        }

        private void DrawRuntimeStatus()
        {
            EditorGUILayout.LabelField("Runtime status", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test Init. Outside WebGL builds every SDK call is a no-op; " +
                                        "enable 'Simulate In Editor' to receive a fake OnReady.", MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(LeChuckSDK.IsInitialized))
            {
                if (GUILayout.Button("Test Init (Play Mode)"))
                    LeChuckSDK.Init((LeChuckSettings)target);
            }

            LeChuckUser? user = LeChuckSDK.GetUser();
            EditorGUILayout.LabelField("Initialized", LeChuckSDK.IsInitialized ? "Yes" : "No");
            EditorGUILayout.LabelField("Ready", LeChuckSDK.IsReady ? "Yes" : "No");
            EditorGUILayout.LabelField("User", user.HasValue ? user.Value.ToString() : "(none)");
        }
    }
}
