# Legacy host scripts (`scripts/legacy/`)

Windows helpers migrated from `C:\Scripts\`. Run without Docker; logs stay in **`C:\Scripts\logs`**.

**Chooser:** [`../../start.bat`](../../start.bat) → option `[1]`  
**Guide:** [`../../docs/DOCKER_VS_LEGACY.md`](../../docs/DOCKER_VS_LEGACY.md)

| Script | Purpose |
|--------|---------|
| `start-all.bat` | Redis + Backend + POS + Admin (separate windows) |
| `start-backend.bat` | Host `dotnet run` |
| `start-frontend.bat` | Host Expo POS |
| `start-frontend-admin.bat` | Host Next.js Admin |
| `start-redis.bat` | Portable Redis (`tools/redis`) |
| `kill-ports.bat` | Free common ports |
| `open-tabs.bat` | Explorer tabs |
| `GameMode.ps1` / `WorkMode.ps1` | Optional display/power presets |

`C:\Scripts\*.bat` shortcuts redirect here for backward compatibility.
