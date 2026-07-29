# Scripts Quick Reference

> Pocket card for Windows double-click helpers.  
> Modes: [`DOCKER_VS_LEGACY.md`](DOCKER_VS_LEGACY.md) · Map: [`SCRIPTS_ECOSYSTEM.md`](SCRIPTS_ECOSYSTEM.md) · Full: [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md)

---

## Start here

| Icon | Script | What it does |
|------|--------|--------------|
| 🔀 | `start.bat` | Choose **Legacy** or **Docker** mode |
| 🚀 | `start-dev.bat` | npm workspaces all-in-one (`npm run dev`) |

## Legacy Mode (`scripts\legacy\`)

| Icon | Script | What it does |
|------|--------|--------------|
| 🪟 | `scripts\legacy\start-all.bat` | Redis + Backend + POS + Admin (windows) |
| ⚙️ | `scripts\legacy\start-backend.bat` | Host `dotnet run` |
| 📱 | `scripts\legacy\start-frontend.bat` | Host Expo POS |
| 🖥️ | `scripts\legacy\start-frontend-admin.bat` | Host Admin |
| 🔴 | `scripts\legacy\start-redis.bat` | Portable Redis |
| 🛑 | `scripts\legacy\kill-ports.bat` | Free common ports |

Logs → `C:\Scripts\logs\`

## Everyday (npm)

| Icon | Script | What it does |
|------|--------|--------------|
| ⚙️ | `start-backend.bat` | API only (`:5184`) |
| 🖥️ | `start-admin.bat` | Admin FA only (`:3000`) |
| 📱 | `start-pos.bat` | POS Expo only (`:8081`) |
| 🌐 | `start-sites.bat` | Tenant Sites only (`:3001`) |
| 🧪 | `test-all.bat` | Backend → Admin → POS tests (sequential) |
| 🧹 | `clean-all.bat` | Confirm + clean build artifacts |

## Docker Mode (`scripts\docker\` / root wrappers)

| Icon | Script | What it does |
|------|--------|--------------|
| 🐳 | `docker-up.bat` | Start Compose (+ POS/Sites profiles) |
| 🐳 | `docker-down.bat` | Stop Compose (keep volumes) |
| 📊 | `docker-status.bat` | Show container status |
| 📜 | `docker-logs.bat` | Follow Compose logs |
| 💣 | `docker-clean.bat` | Remove volumes + prune (**data loss**) |

Logs → `C:\Scripts\logs\` · Rollback: use Legacy if Docker fails

## Maintenance (`scripts\`)

| Icon | Script | What it does |
|------|--------|--------------|
| 🧽 | `scripts\clean-backend.bat` | Clean backend `bin`/`obj` |
| 🗑️ | `scripts\dev-purge-tenant.bat` | Purge Dev tenant catalog |
| 📦 | `scripts\generate-dep-export.bat` | Generate DEP Prüftool fixtures |
| ☕ | `scripts\ensure-bmf-prueftool.bat` | Install BMF Prüftool JARs |
| 🎨 | `scripts\fix-antd.bat` | Fix Ant Design deprecations |
| ✉️ | `scripts\dev-mail.bat` | Dev mail config + test |
| ✅ | `scripts\smoke-test.bat` | Lightweight curl smoke (API/Admin/POS) |
| 🔬 | `scripts\run-comprehensive-smoke.bat` | Full HTTP / FA / RKSV smoke |
| 🧪 | `scripts\test-mode-scripts.bat` | Legacy/Docker/`start.bat` structural smoke |
| 📋 | `scripts\test-scripts.bat` | Dry-run structure tests |

---

## One-liners

```batch
start.bat
REM or:
scripts\legacy\start-all.bat
docker-up.bat
scripts\smoke-test.bat
test-all.bat
docker-down.bat
scripts\test-mode-scripts.bat
```

```batch
npm run validate:scripts
npm run test:scripts
npm run verify:bat-ps1
```

**Tip:** Prefer `git revert` on shared branches; use `rollback.bat` only on disposable local commits.  
**Modes:** [`DOCKER_VS_LEGACY.md`](DOCKER_VS_LEGACY.md)
