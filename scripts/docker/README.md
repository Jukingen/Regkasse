# Docker scripts (`scripts/docker/`)

Two layers:

| Layer | Path | Role |
|-------|------|------|
| **PowerShell Compose** | `scripts/docker/*.ps1` (+ sibling `.bat`) | Canonical flags: `-Dev` / `-Prod`, profiles, build/push/deploy/diagnose |
| **Host / chooser** | [`host/`](host/) | Turkish UX + `C:\Scripts\logs` (mode chooser path) |

**Chooser:** [`../dev/start.bat`](../dev/start.bat) → option `[2]` → `host\up.bat`  
**Guide:** [`../../docs/DOCKER_VS_LEGACY.md`](../../docs/DOCKER_VS_LEGACY.md)

## Host bats (`host/`)

| Script | Log file | Purpose |
|--------|----------|---------|
| `up.bat` | `docker.log` | Full stack + POS + Sites profiles |
| `down.bat` | `docker_down.log` | Stop (keep volumes) |
| `status.bat` | `docker_status.log` | Status |
| `logs.bat` | `docker_logs.log` | Follow logs (`Ctrl+C`) |
| `clean.DANGER.bat` | `docker_clean.log` | DANGER: wipe volumes + prune |
| `up-backend.bat` | `docker_backend.log` | Infra + API |
| `up-admin.bat` | `docker_admin.log` | Infra + API + Admin |
| `up-pos.bat` | `docker_pos.log` | Infra + API + POS web |

## Prerequisites

Docker Desktop + WSL 2 must be installed on the host. Compose files under the repo are **not** enough by themselves.

```powershell
.\scripts\docker\ensure-docker-desktop.ps1
.\scripts\docker\docker-diagnose.ps1
```

If CLI is missing: Admin `wsl --install`, then `winget install --id Docker.DockerDesktop -e` — see [`docs/DOCKER_WINDOWS_SETUP.md`](../../docs/DOCKER_WINDOWS_SETUP.md).

Without Docker: [`scripts/dev/start.bat`](../dev/start.bat) → Legacy, or `scripts\dev\start-dev.bat`.

## PowerShell (examples)

```powershell
.\scripts\docker\docker-up.ps1 -Build
.\scripts\docker\docker-up.ps1 -Prod -Profile admin
.\scripts\docker\docker-down.ps1
.\scripts\docker\docker-deploy.ps1 -Profile admin
.\scripts\docker\docker-diagnose.ps1
```
