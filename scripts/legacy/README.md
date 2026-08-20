# Legacy host scripts (`scripts/legacy/`)

Windows helpers migrated from `C:\Scripts\`. Run without Docker; logs stay in **`C:\Scripts\logs`**.

**Chooser:** [`../dev/start.bat`](../dev/start.bat) → option `[1]`  
**Guide:** [`../../docs/DOCKER_VS_LEGACY.md`](../../docs/DOCKER_VS_LEGACY.md)

| Script | Purpose |
|--------|---------|
| `start-all.bat` | Redis + Backend + Admin + POS |
| `start-backend.bat` | Host `dotnet run` |
| `start-frontend.bat` | Host Expo POS (`npm run dev`, `--max-workers=2`) |
| `start-frontend-admin.bat` | Host Next.js Admin (`next dev --webpack`, worker cap in config) |
| `start-redis.bat` | Portable Redis (`tools/redis`; may call `scripts\dev\start-redis-dev.ps1`) |
| `kill-ports.bat` / `.ps1` | Free common ports |
| `open-tabs.bat` / `.ps1` | Explorer tabs |
| `GameMode.ps1` / `WorkMode.ps1` | Optional display/power presets |

`C:\Scripts\*.bat` shortcuts redirect here for backward compatibility.
