# Backend authentication (optional)

The plugin can POST the LeChuck credentials to an endpoint you own so the player
gets a session in **your** backend. The endpoint validates the token against the
Minijuegos API server-side. **Your API key never reaches the client.**

## Contract

The plugin sends:

```http
POST {your authUrl}
Content-Type: application/json

{ "miniplay_id": "1234567", "token": "user_token_from_sdk" }
```

(with `credentials: 'include'`). Your endpoint responds with any JSON body; the
plugin forwards the raw body to your `Authenticate` callback.

## Reference implementation (Node/Express + Postgres)

### DB migration

```sql
ALTER TABLE users ADD COLUMN miniplay_id VARCHAR(64) UNIQUE;
CREATE INDEX idx_users_miniplay_id ON users(miniplay_id);
```

### Endpoint

```javascript
router.post('/auth/miniplay', async (req, res) => {
  const { miniplay_id, token } = req.body;
  if (!miniplay_id || !token) return res.status(400).json({ error: 'missing fields' });

  // 1. Validate the token against Minijuegos (MINIPLAY_API_ID is public)
  const url = `https://api.minijuegos.com/lechuck/client-js/user/${miniplay_id}/authenticate/`
    + `?api_id=${process.env.MINIPLAY_API_ID}&user_token=${token}&mobile=0&locale=es_ES`;
  let mp;
  try { mp = await (await fetch(url)).json(); }
  catch { return res.status(502).json({ error: 'minijuegos unreachable' }); }
  if (!mp?.status?.success) return res.status(403).json({ error: 'invalid token' });

  // 2. Upsert the user (synthetic email when email is NOT NULL in your schema)
  const email = `miniplay_${miniplay_id}@miniplay.local`;
  const username = mp.user?.name || `Player_${miniplay_id}`;
  const user = await db.query(
    `INSERT INTO users (miniplay_id, email, username) VALUES ($1,$2,$3)
     ON CONFLICT (miniplay_id) DO UPDATE SET username = EXCLUDED.username, updated_at = NOW()
     RETURNING *`, [miniplay_id, email, username]);

  // 3. Session: HttpOnly cookie is safer than a body token
  const jwt = require('jsonwebtoken');
  res.cookie('session', jwt.sign({ userId: user.rows[0].id, miniplay_id },
    process.env.JWT_SECRET, { expiresIn: '7d' }),
    { httpOnly: true, secure: true, sameSite: 'Lax', maxAge: 7 * 24 * 3600 * 1000 });
  res.json({ user: { id: user.rows[0].id, name: username } });
});
```

## Server-side writes (stats / achievements)

Write operations that require your `MINIPLAY_API_KEY` must run on the server
after a session ends. The plugin never touches the API key: implement a
server-side session (like above) if your game needs server-side writes, and
drive them from your own trusted data.

## Secrets checklist

- Game id → public, goes in the settings asset.
- API key → server-side env var only, never in the Unity project.
- JWT/session secret → server-side env var only.
