# LeChuck SDK Bridge (Minijuegos / Miniplay) for Unity

Unity WebGL bridge for the [Minijuegos.com / Miniplay.com](https://www.minijuegos.com) LeChuck JS SDK.

Generic and self-contained: bring your own game id from the Minijuegos developer panel — **no ids, keys or secrets are bundled**.

## Features

- SDK script injection at runtime (no template edits required)
- Ready/user detection (`user.getId()`, token, display name)
- Score (`set_score`, once per session), stats (`stat.put`, REPLACE semantics), achievements (`unlockAchievement` + boolean stat, with double-unlock guard)
- Session flush helper (best-effort delay guard before game restart, see [gotchas](Documentation~/minijuegos-publishing.md))
- Portal detection (miniplay / minijuegos / unknown)
- Optional auth hook: POST `{miniplay_id, token}` to *your* backend endpoint (token validated server-side against the Minijuegos API — see [backend reference](Documentation~/backend.md))
- Editor-friendly: everything is a safe no-op outside WebGL (optional simulated user for testing)

## Requirements

- Unity 2022.3+ (WebGL build target for the SDK features)
- A Minijuegos developer panel account and a registered game id

## Install

Copy (or add as a git package / Asset Store package) into your project:

```
Packages/com.lechuck.sdk/
```

## Usage

1. Create a settings asset: `Assets > Create > LeChuck > Settings`, set your **game id**.
2. Init from any script:

```csharp
using LeChuck;

[SerializeField] private LeChuckSettings settings;

void Awake() {
    LeChuckSDK.Init(settings);
    LeChuckSDK.OnReady += user => Debug.Log($"LeChuck user: {user.Name}");
}

void OnGameOver() {
    LeChuckSDK.SetScore(finalScore);
    LeChuckSDK.SetStat("races", totalRaces);          // totals, not deltas
    LeChuckSDK.UnlockAchievement("your_achievement_uid");
    LeChuckSDK.Flush(() => { ShowGameOverScreen(); }); // let the SDK send data first
}
```

See `Samples~/RaceExample` for a complete example.

## Publishing notes (Minijuegos)

Important details and common pitfalls are in [Documentation~/minijuegos-publishing.md](Documentation~/minijuegos-publishing.md):

- The SDK reads `mp_api_user_id` / `mp_api_user_token` from the URL; **preserve query params** on any redirect.
- Unpublished games do not receive a user id: `GetUser()` returns null in dev.
- Use `Cross-Origin-Embedder-Policy: credentialless` (not `require-corp`) if you need COE headers.

## Backend authentication (optional)

To sign players into your own backend, expose one endpoint that validates the LeChuck token server-side and returns a session (see [Documentation~/backend.md](Documentation~/backend.md)). Set its URL in the settings asset and call:

```csharp
LeChuckSDK.Authenticate(response => { /* your session handling */ });
```

The plugin never stores or ships your secrets: token validation always happens on your server.

## License

MIT
