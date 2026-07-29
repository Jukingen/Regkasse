# New Script Ecosystem Available!

We've added `.bat` files for all common tasks in the Regkasse project!

## What's New?

- **21 `.bat` files** for common tasks (13 root + 8 under `scripts\`)
- **Full documentation** at [`docs/SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md)
- **Quick start guide** at [`docs/GETTING_STARTED_SCRIPTS.md`](GETTING_STARTED_SCRIPTS.md)
- **Test checklist** at [`docs/SCRIPTS_TEST_CHECKLIST.md`](SCRIPTS_TEST_CHECKLIST.md)
- **Pocket card** at [`docs/SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md)

## Why?

- **No PowerShell required for daily use** — double-click the `.bat`
- **Consistent** — everyone uses the same commands
- **Safer** — confirmations on destructive actions + clearer errors
- **Documented** — purpose, URLs, and troubleshooting in one place

## Quick Examples

| What | Script |
|------|--------|
| Start everything | `start-dev.bat` |
| Start Docker | `docker-up.bat` |
| Stop Docker | `docker-down.bat` |
| Container status | `docker-status.bat` |
| Run tests | `test-all.bat` |
| Prod Compose deploy | `deploy.bat` (confirm carefully) |

## Where to Start

1. Read [`docs/GETTING_STARTED_SCRIPTS.md`](GETTING_STARTED_SCRIPTS.md)
2. Try `start-dev.bat`
3. Try `docker-up.bat` → `docker-status.bat` → `docker-down.bat`

## Questions?

- Check [`docs/SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md)
- Ask in the team chat
- Contact the DevOps / platform owners

---

**Happy coding!**

*Copy/paste this into Slack, Teams, or email as needed.*
