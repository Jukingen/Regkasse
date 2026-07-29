# Scripts Quick Reference

> Pocket card for Windows double-click helpers.  
> Map: [`SCRIPTS_ECOSYSTEM.md`](SCRIPTS_ECOSYSTEM.md) · Full: [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) · Folder: [`../scripts/README.md`](../scripts/README.md)

---

## Everyday

| Icon | Script | What it does |
|------|--------|--------------|
| 🚀 | `start-dev.bat` | Start all services (API + Admin + POS + Sites) |
| ⚙️ | `start-backend.bat` | API only (`:5184`) |
| 🖥️ | `start-admin.bat` | Admin FA only (`:3000`) |
| 📱 | `start-pos.bat` | POS Expo only (`:8081`) |
| 🌐 | `start-sites.bat` | Tenant Sites only (`:3001`) |
| 🧪 | `test-all.bat` | Backend → Admin → POS tests (sequential) |
| 🧹 | `clean-all.bat` | Confirm + clean build artifacts |

## Docker

| Icon | Script | What it does |
|------|--------|--------------|
| 🐳 | `docker-up.bat` | Start Docker Compose (`up -d`) |
| 🐳 | `docker-down.bat` | Stop Docker Compose |
| 📊 | `docker-status.bat` | Show container status (Names / Status / Ports) |
| 💣 | `docker-clean.bat` | Remove volumes + prune (**data loss**) |

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

## Deploy / helpers

| Icon | Script | What it does |
|------|--------|--------------|
| 🚢 | `deploy.bat` | Prod Compose deploy (confirm + smoke + backup) |
| ⏪ | `rollback.bat` | `git reset --hard HEAD~1` + prod Compose rebuild (**destructive**) |
| 📝 | `scripts\run-with-log.bat` | Run any command with `logs\` capture |
| 🔍 | `scripts\validate-scripts.bat` | Pairing + docs validation (CI) |
| 📋 | `scripts\test-scripts.bat` | Dry-run structure tests |

---

## One-liners

```batch
start-dev.bat
docker-up.bat
scripts\smoke-test.bat
test-all.bat
docker-down.bat
```

```batch
npm run validate:scripts
npm run test:scripts
npm run verify:bat-ps1
```

**Tip:** Prefer `git revert` on shared branches; use `rollback.bat` only on disposable local commits.
