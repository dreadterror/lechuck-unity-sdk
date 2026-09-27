// MIT License — Copyright (c) 2026 LeChuck Bridge contributors
// See LICENSE.md in the package root for the full license text.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeChuck.Samples
{
    /// <summary>
    /// Minimal integration example: initializes the SDK from a settings asset and submits the results of a
    /// finished race (score, stat totals, achievements) before restarting.
    /// </summary>
    public sealed class LeChuckRaceExample : MonoBehaviour
    {
        [SerializeField, Tooltip("Settings asset created with Assets > Create > LeChuck > Settings.")]
        private LeChuckSettings settings;

        private void Awake()
        {
            LeChuckSDK.Init(settings);
            LeChuckSDK.OnReady += HandleReady;
        }

        private void OnDestroy()
        {
            LeChuckSDK.OnReady -= HandleReady;
        }

        private static void HandleReady(LeChuckUser user)
        {
            Debug.Log($"[LeChuckRaceExample] SDK ready. User: {user}, portal: {LeChuckSDK.DetectPortal()}");
        }

        /// <summary>
        /// Sends the final score, the accumulated stat totals and any unlocked achievements, then waits for the
        /// flush window before invoking <paramref name="beforeRestart"/>.
        /// </summary>
        /// <param name="score">Final score of the session.</param>
        /// <param name="stats">Stat uid → accumulated total (REPLACE semantics, never deltas). May be null.</param>
        /// <param name="achievements">Achievement uids unlocked in this session. May be null.</param>
        /// <param name="beforeRestart">Invoked once the data had time to be sent; restart or change scene here.</param>
        public void SubmitGameOver(double score, IReadOnlyDictionary<string, double> stats,
                                   IEnumerable<string> achievements, Action beforeRestart)
        {
            LeChuckSDK.SetScore(score);

            if (stats != null)
                foreach (KeyValuePair<string, double> stat in stats)
                    LeChuckSDK.SetStat(stat.Key, stat.Value);

            if (achievements != null)
                foreach (string uid in achievements)
                    LeChuckSDK.UnlockAchievement(uid);

            LeChuckSDK.Flush(beforeRestart);
        }
    }
}
