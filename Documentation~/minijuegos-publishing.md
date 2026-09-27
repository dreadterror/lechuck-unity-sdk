# Publishing a Unity WebGL game on Minijuegos / Miniplay

Gotchas collected from production integrations. They apply to **any** game using
the LeChuck SDK, independent of this package.

## Query params are sacred

Minijuegos embeds your game in an iframe with auth params:

```
https://yourgame.example/?mp_api_user_id=X&mp_api_user_token=Y&...
```

The SDK reads them automatically. If you redirect `/` to another path, you MUST
preserve the query string or the SDK starts without a user:

```nginx
# ❌ WRONG — drops ?mp_api_user_id=...
location = / { return 301 /game/; }

# ✅ CORRECT
location = / { return 301 /game/$is_args$args; }
```

## COEP: use credentialless

If you need `Cross-Origin-Embedder-Policy` headers (e.g. for shared buffers),
use `credentialless`. `require-corp` blocks the external SDK script:

```nginx
add_header Cross-Origin-Embedder-Policy "credentialless";
add_header Cross-Origin-Opener-Policy "same-origin";
```

## User id in dev vs prod

Minijuegos only injects `mp_api_user_id` for **published** games. While your
build is unpublished, `LeChuckSDK.GetUser()` will be null even when everything
is wired correctly. Test user flows on a published (or panel-enabled) build.

## API credentials

- `game id` (public): per game, from the developer panel. Passed via the
  settings asset; never hardcode it in the package.
- `API key` (secret): server-to-server only (stats/achievements writes,
  token validation helpers). Never in client code or bundles.
- Dev credentials differ from prod credentials.

## Flushing data before restart

The SDK sends score/stats/achievements over async HTTP. If the game reloads
immediately after calling it, requests are cancelled and data is lost. Call
`LeChuckSDK.Flush(onDone)` and only then restart/transition. For critical data,
prefer sending it to your own backend (`LeChuckSDK.Authenticate` style) instead
of relying only on client-side SDK calls.

## Stats semantics

Stats are REPLACE: send accumulated totals (`totalRaces`), never deltas.
Achievements are boolean: `UnlockAchievement` sends the unlock **and** a
boolean stat (`uid = 1`), mirroring the pattern used by top integrations.
