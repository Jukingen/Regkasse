# Local Redis (Windows portable)

Binaries under this folder are **not** committed. They are downloaded on demand by:

```powershell
.\scripts\start-redis-dev.ps1
```

That script fetches [tporadowski/redis](https://github.com/tporadowski/redis) 5.0.14.1 into `tools/redis/` and starts `redis-server` on `localhost:6379` (matches typical `Redis:ConnectionString` in backend Development config).

## Commands

```powershell
.\scripts\start-redis-dev.ps1          # download if needed + start
.\scripts\start-redis-dev.ps1 -PingOnly
.\scripts\start-redis-dev.ps1 -Stop
```

Prefer Docker Redis if you already use containers. See `backend/CONFIGURATION.md`.

Do not commit `dump.rdb`, `.exe`, `.pdb`, or zip artifacts — see `.gitignore` in this folder.
