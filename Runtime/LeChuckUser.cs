// MIT License — Copyright (c) 2026 LeChuck Bridge contributors
// See LICENSE.md in the package root for the full license text.

namespace LeChuck
{
    /// <summary>Player identity reported by the LeChuck SDK.</summary>
    public struct LeChuckUser
    {
        /// <summary>Minijuegos/Miniplay user id, or null/empty for a guest.</summary>
        public string Id;

        /// <summary>Session token to validate on your backend. Treat it as a credential: do not log it.</summary>
        public string Token;

        /// <summary>Display name (falls back to <c>Player_{Id}</c> when the portal provides none).</summary>
        public string Name;

        /// <summary>True when the portal reported a user id.</summary>
        public bool IsSignedIn => !string.IsNullOrEmpty(Id);

        /// <summary>Readable description without the token.</summary>
        public override string ToString() => IsSignedIn ? $"{Name} ({Id})" : "(guest)";
    }
}
