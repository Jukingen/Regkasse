# Docker scripts (`scripts/docker/`)

Legacy-style Windows helpers (title/color, `C:\Scripts\logs`, Turkish messages).

**Chooser:** [`../../start.bat`](../../start.bat) → option `[2]`  
**Guide:** [`../../docs/DOCKER_VS_LEGACY.md`](../../docs/DOCKER_VS_LEGACY.md)

| Script | Log file | Purpose |
|--------|----------|---------|
| `docker-up.bat` | `docker.log` | Full stack + POS + Sites profiles |
| `docker-down.bat` | `docker_down.log` | Stop (keep volumes) |
| `docker-status.bat` | `docker_status.log` | Status |
| `docker-logs.bat` | `docker_logs.log` | Follow logs (`Ctrl+C`) |
| `docker-clean.bat` | `docker_clean.log` | Wipe volumes + prune (**destructive**) |
| `docker-up-backend.bat` | `docker_backend.log` | Infra + API |
| `docker-up-admin.bat` | `docker_admin.log` | Infra + API + Admin |
| `docker-up-pos.bat` | `docker_pos.log` | Infra + API + POS web |

Root `docker-*.bat` wrappers call these scripts.
