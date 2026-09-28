# Changelog

## 0.1.0 (2026-09-27)

- Initial public version.
- Runtime script injection of the LeChuck SDK (no template edits).
- Ready/user detection, score, stats (REPLACE), achievements with double-unlock guard scoped per game and user, marked only after the SDK call (a pre-ready call can no longer poison the cache).
- Session flush helper with configurable delay (best-effort: the LeChuck SDK exposes no delivery callback).
- Portal detection (miniplay / minijuegos / unknown) by strict parsed hostname match.
- Init re-entry guard: the vendor SDK script is never injected twice.
- Optional backend authentication hook (developer-provided endpoint).
- Editor-safe no-ops with optional simulated user.
