# Scripts Quick Reference

> Pocket card for Windows double-click helpers.  
> Modes: [`DOCKER_VS_LEGACY.md`](DOCKER_VS_LEGACY.md) · Map: [`SCRIPTS_ECOSYSTEM.md`](SCRIPTS_ECOSYSTEM.md) · Full: [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md)

All entry points are under `scripts/<category>/` (no root `.bat`).

---

## Start here

| Icon | Script | What it does |
|------|--------|--------------|
| 🔀 | `scripts\dev\start.bat` | Choose **Legacy** or **Docker** mode |
| 🚀 | `scripts\dev\start-dev.bat` | npm workspaces all-in-one (`npm run dev`) |

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

## Everyday (npm) — `scripts\dev\`

| Icon | Script | What it does |
|------|--------|--------------|
| ⚙️ | `scripts\dev\start-backend.bat` | API only (`:5184`) |
| 🖥️ | `scripts\dev\start-admin.bat` | Admin FA only (`:3000`) |
| 📱 | `scripts\dev\start-pos.bat` | POS Expo only (`:8081`) |
| 🌐 | `scripts\dev\start-sites.bat` | Tenant Sites only (`:3001`) |
| 🧪 | `scripts\test\test-all.bat` | Backend → Admin → POS tests |
| 🧹 | `scripts\dev\clean-all.DANGER.bat` | Confirm + clean build artifacts |

## Docker Mode (`scripts\docker\host\`)

| Icon | Script | What it does |
|------|--------|--------------|
| 🐳 | `scripts\docker\host\up.bat` | Start Compose (+ POS/Sites profiles) |
| 🐳 | `scripts\docker\host\down.bat` | Stop Compose (keep volumes) |
| 📊 | `scripts\docker\host\status.bat` | Show container status |
| 📜 | `scripts\docker\host\logs.bat` | Follow Compose logs |
| 💣 | `scripts\docker\host\clean.DANGER.bat` | Remove volumes + prune (**data loss**) |

PowerShell: `scripts\docker\docker-up.ps1` · Logs → `C:\Scripts\logs\`

## Maintenance

| Icon | Script | What it does |
|------|--------|--------------|
| 🧽 | `scripts\dev\clean-backend.bat` | Clean backend `bin`/`obj` |
| 🗑️ | `scripts\dev\dev-purge-tenant.DANGER.bat` | Purge Dev tenant catalog |
| 📦 | `scripts\rksv\generate-dep-export.bat` | Generate DEP Prüftool fixtures |
| ☕ | `scripts\rksv\ensure-bmf-prueftool.bat` | Install BMF Prüftool JARs |
| 🎨 | `scripts\dev\fix-antd.bat` | Fix Ant Design deprecations |
| ✉️ | `scripts\dev\dev-mail.bat` | Dev mail config + test |
| 💨 | `scripts\test\smoke-test.bat` | Lightweight curl smoke |

## Deploy / ops

| Icon | Script | What it does |
|------|--------|--------------|
| 🚢 | `scripts\ops\deploy.DANGER.bat` | Prod compose + smoke + backup gate |
| ⏪ | `scripts\ops\rollback.DANGER.bat` | Destructive last-commit undo |

## Gates

```batch
npm run verify:bat-ps1
npm run validate:scripts
npm run test:scripts
```
