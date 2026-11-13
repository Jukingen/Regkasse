# Docker on Windows — troubleshooting

Focused guide for Regkasse developers using **Docker Desktop + WSL 2** on Windows.

| Language | Doc |
|----------|-----|
| **English (this page)** | [`DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md) |
| **Deutsch** | [`DOCKER_WINDOWS_TROUBLESHOOTING.de.md`](DOCKER_WINDOWS_TROUBLESHOOTING.de.md) |

**Setup first:** [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md)  
**Compose workflow:** [`DOCKER.md`](DOCKER.md) · [`../DEVELOPMENT.md`](../DEVELOPMENT.md#docker-compose-full-stack)  
**Diagnose quickly:**

```powershell
.\scripts\docker-diagnose.ps1
```

**Last updated:** 2026-07-29

---

## Quick fixes (most common)

| Issue | Solution |
|-------|----------|
| WSL2 not installed | `wsl --install` (Admin PowerShell), then reboot |
| Docker Desktop not starting | Enable **Virtual Machine Platform** + WSL 2; reboot; `wsl --update` |
| Port already in use (e.g. 5432) | `netstat -ano \| findstr :5432` → stop process or change port in `.env` |
| Volume / file sharing errors | Docker Desktop → **Settings → Resources → File sharing** (or use WSL filesystem) |
| `docker` not recognized | Start Docker Desktop; open a **new** terminal |
| Engine stuck “starting…” | Quit Docker Desktop → `wsl --shutdown` → start again |

---

## 1. Installation issues

### 1.1 WSL 2 not installed / incomplete

**Symptoms:** Docker Desktop asks for WSL 2; `wsl` fails; “WSL 2 installation is incomplete”.

```powershell
# Run PowerShell as Administrator
wsl --install
wsl --set-default-version 2
wsl --update
```

Legacy feature enable (if `wsl --install` is unavailable):

```powershell
dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
```

Then **reboot**, install a distro (`wsl --install -d Ubuntu`), and confirm:

```powershell
wsl -l -v
# VERSION column must be 2
```

Kernel package (older Windows 10): [WSL2 Linux kernel update](https://aka.ms/wsl2kernel).

### 1.2 Docker Desktop will not start / Hyper-V / virtualization

**Symptoms:** Whale icon stuck; “Hardware assisted virtualization is not enabled”; crash on launch.

1. **BIOS/UEFI:** enable Intel VT-x / AMD-V / SVM.
2. **Windows features** (optional on Home; Pro/Enterprise):
   - Virtual Machine Platform — **required** for WSL 2
   - Windows Hypervisor Platform — often needed
   - Hyper-V — not always required for WSL 2 backend; enable if Docker asks
3. Task Manager → Performance → CPU → **Virtualization: Enabled**.
4. Reboot, then:

```powershell
wsl --status
wsl --update
```

Restart Docker Desktop. Prefer the **WSL 2 based engine** (Settings → General).

### 1.3 Installer / PATH / permissions

| Symptom | Fix |
|---------|-----|
| Installer fails mid-way | Uninstall Docker Desktop → reboot → reinstall as current user or all-users per IT policy |
| `docker` not in PATH after install | Sign out/in or open a new terminal; confirm Desktop is running |
| Corporate policy blocks install | Request Docker Desktop license / exception; see [Docker Desktop license](https://docs.docker.com/subscription/desktop-license/) |
| Antivirus quarantines Docker | Allowlist Docker Desktop + `%LOCALAPPDATA%\Docker` / WSL VHDX paths (IT approval) |

### 1.4 Distro still on WSL 1

```powershell
wsl --set-version Ubuntu 2
wsl -l -v
```

---

## 2. Runtime issues

### 2.1 Engine not responding

```powershell
docker info
# error during connect / cannot find dockerDesktopLinuxEngine → engine down

# Reset WSL VMs (stops all Linux distros)
wsl --shutdown
# Start Docker Desktop from the Start menu, wait until “Running”
docker run --rm hello-world
```

### 2.2 Port already in use

Regkasse defaults: **5184** (API), **5432** (Postgres), **6379** (Redis), **3000** (Admin), **8081** (POS web), **3001** (Sites).

```powershell
netstat -ano | findstr ":5432"
netstat -ano | findstr "5184 5432 6379 3000 8081 3001"
```

Find and stop the owning process (replace `PID`):

```powershell
tasklist /FI "PID eq PID"
# Stop only if you know it is safe (e.g. local postgres.exe / old compose)
Stop-Process -Id PID -Force
```

Or change the host port in root `.env`:

```env
POSTGRES_PORT=5433
API_PORT=5185
ADMIN_PORT=3002
```

Then `docker compose up --build` again.

Local Redis via `.\scripts\start-redis-dev.ps1` conflicts with Compose Redis on **6379** — stop one of them.

### 2.3 Compose service unhealthy / backend exits

```powershell
docker compose ps
docker compose logs backend --tail 100
docker compose logs postgres --tail 50
```

| Check | Action |
|-------|--------|
| JWT too short | Set `JWT_SECRET_KEY` ≥ 32 chars in `.env` |
| Postgres not ready | Wait for healthy; `docker compose restart backend` |
| Soft TSE on prod file | Do **not** merge `docker-compose.override.yml` with `docker-compose.prod.yml` |
| Prod TSE lock | Fill Fiskaly secrets in `.env.production`; see `TseProductionOptionsValidator` |

### 2.4 Volume / permission / file sharing errors

**Symptoms:** “mount denied”; “path is not shared”; permission denied writing volumes; weird failures when bind-mounting `C:\…`.

**Docker Desktop (Hyper-V / older file sharing UI):**

1. Settings → **Resources → File sharing**
2. Share the drive that holds the repo (e.g. `C:`)
3. Apply & restart

**WSL 2 backend (current default):** Linux containers use the WSL VM. Prefer:

- Repo under WSL: `\\wsl$\Ubuntu\home\<user>\Regkasse` (faster, fewer share issues), **or**
- Keep repo on `C:\` and avoid bind-mounting Windows paths into Linux for heavy builds (Regkasse Compose does not bind-mount `./backend` onto `/app` for this reason)

Named volumes (`regkasse_pgdata`, etc.) do not need Windows file sharing.

```powershell
docker volume ls
docker compose down    # keep data
# nuclear (data loss):
docker compose down -v
```

### 2.5 Disk full / “no space left”

```powershell
docker system df
docker system prune -a    # removes unused images — careful
wsl --shutdown
# Optionally compact WSL VHD via Disk Cleanup / vendor docs
```

---

## 3. Performance issues

### 3.1 Slow builds / high memory

| Cause | Fix |
|-------|-----|
| WSL RAM too low | Docker Settings → Resources ≥ **4 GB** RAM / **2** CPUs; or `%UserProfile%\.wslconfig` |
| Repo on NTFS (`C:\`) with many file watches | Clone/work under WSL filesystem for Compose builds |
| Defender real-time scan | IT-approved exclusions for Docker/WSL data dirs |
| Parallel heavy tools | Close Android Studio / many browsers during first `compose build` |

Example `.wslconfig`:

```ini
[wsl2]
memory=6GB
processors=4
swap=2GB
```

```powershell
wsl --shutdown
# Restart Docker Desktop
```

### 3.2 Compose build OOM / killed

Reduce parallelism, raise memory, prune images, rebuild one service:

```powershell
docker compose build backend
docker compose up -d
```

### 3.3 Postgres / Redis slow on first start

Normal while volumes initialize. Wait for `healthy` in `docker compose ps`.

---

## 4. Network issues

### 4.1 Cannot pull images (`hello-world` / `postgres:16-alpine`)

| Cause | Fix |
|-------|-----|
| No internet / DNS | Fix network; try `docker pull postgres:16-alpine` |
| Corporate proxy | Docker Desktop → Settings → **Resources → Proxies**; set HTTP/HTTPS proxy |
| TLS inspection | Import corp CA into Docker/WSL trust store (IT) |
| Docker Hub rate limit | Authenticate (`docker login`) or use a mirror |

### 4.2 Browser cannot reach API / Admin

- Clients must use **`localhost`**, not the Compose service name `backend`.
- Rebuild Admin after changing `NEXT_PUBLIC_API_BASE_URL` (`docker compose build --no-cache frontend-admin`).
- Confirm published ports: `docker compose ps` / `netstat` for **5184** / **3000**.
- CORS: Dev Compose uses `ASPNETCORE_ENVIRONMENT=Development`.

### 4.3 Container → host / VPN

- VPN clients sometimes break WSL networking — disconnect VPN briefly to test.
- From a container, host is often `host.docker.internal` (Docker Desktop).
- Host → container: use published `localhost:PORT`.

### 4.4 Firewall

Allow Docker Desktop / `com.docker.backend` through Windows Defender Firewall when prompted. Corporate firewalls may need Hub + registry allowlists.

---

## 5. Diagnostic script

From the repository root:

```powershell
.\scripts\docker-diagnose.ps1
```

Checks Docker CLI, Compose, WSL, engine (`docker info`), and listeners on Regkasse ports. Exit code `0` = no hard failures; `1` = at least one check failed.

Manual equivalent:

```powershell
Write-Host "Checking Docker..."
docker --version
Write-Host "Checking WSL..."
wsl --list --verbose
Write-Host "Checking Docker Compose..."
docker compose version
Write-Host "Checking ports..."
netstat -ano | findstr "5184 5432 6379 3000 8081"
```

---

## 6. Still stuck?

1. Run `.\scripts\docker-diagnose.ps1` and note which step failed.
2. Collect: `docker compose ps`, `docker compose logs backend --tail 200`, Windows build (`winver`).
3. Re-read [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md) checklist.
4. Last resort: quit Docker Desktop → `wsl --shutdown` → reboot → start Docker → `docker run --rm hello-world`.

Do **not** run `docker compose down -v` on shared/prod-like data unless you accept wiping Postgres/Redis volumes.
