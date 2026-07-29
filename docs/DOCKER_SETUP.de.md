# Docker-Einrichtung & Migrationsplan — Regkasse

Vollständige Anleitung: vorhandene Artefakte, Entwicklungs- vs. produktionsnahe Stacks, PowerShell-Skripte.

| Sprache | Dokument |
|---------|----------|
| **Deutsch (diese Seite)** | [`DOCKER_SETUP.de.md`](DOCKER_SETUP.de.md) |
| **English** | [`DOCKER_SETUP.md`](DOCKER_SETUP.md) |

**Hub:** [`DOCKER.de.md`](DOCKER.de.md) · Windows: [`DOCKER_WINDOWS_SETUP.de.md`](DOCKER_WINDOWS_SETUP.de.md) · Fehlerbehebung: [`DOCKER_WINDOWS_TROUBLESHOOTING.de.md`](DOCKER_WINDOWS_TROUBLESHOOTING.de.md)

**Zuletzt aktualisiert:** 2026-07-29

---

## 1. Migrationsstatus

Dockerfiles, Compose-Dateien, `.dockerignore`, Env-Vorlagen und Skripte sind **bereits vorhanden** — siehe Tabelle in der [englischen Fassung](DOCKER_SETUP.md#1-migration-status-checklist). Neu anlegen ist nicht nötig; Skripte nutzen.

---

## 2. Drei Betriebsarten

| Modus | Compose | Fiskal |
|-------|---------|--------|
| **A. Nur Infrastruktur** | `docker-compose.dev.yml` | Soft-TSE auf dem Host OK |
| **B. Voller Dev-Stack** | `docker-compose.yml` + **Override** | Soft-TSE / FON-Simulation |
| **C. Produktionsnah** | `docker-compose.prod.yml` + `.env.production` | Device/Real — fail-closed |

`docker-compose.override.yml` **niemals** mit der Prod-Datei mischen (RKSV).

---

## 3. Schnellstart (Development)

```powershell
copy .env.example .env
# JWT_SECRET_KEY ≥ 32 Zeichen

.\scripts\docker-build.ps1 -Dev
.\scripts\docker-up.ps1 -Build

curl -fsS http://localhost:5184/api/health/live
.\scripts\docker-down.ps1
```

### Empfohlen zum Codieren (Hot-Reload)

```powershell
docker compose -f docker-compose.dev.yml up -d
npm run dev
```

---

## 4. Produktionsnah

```powershell
copy .env.production.example .env.production
# Secrets ausfüllen (Postgres, JWT, Fiskaly, ADMIN_API_URL)

.\scripts\docker-deploy.ps1 -Profile admin
.\scripts\docker-down.ps1 -Prod
```

Details (EN): [`../DEPLOYMENT.md`](../DEPLOYMENT.md#docker-compose-production-oriented) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md).

---

## 5. Skripte

| Skript | Zweck |
|--------|--------|
| `docker-build.ps1` | Images bauen (Dev und/oder Prod) |
| `docker-up.ps1` | Stack starten (`-d`) |
| `docker-down.ps1` | Stack stoppen (`-All`, `-Volumes`) |
| `docker-deploy.ps1` | Prod bauen + starten (Bestätigung) |
| `docker-diagnose.ps1` | Windows/Docker/WSL/Ports prüfen |

```powershell
.\scripts\docker-build.ps1
.\scripts\docker-up.ps1
.\scripts\docker-deploy.ps1 -Profile admin
.\scripts\docker-diagnose.ps1
```

---

## 6. Wichtige Hinweise

- Browser: immer **`localhost`**, nicht Docker-DNS `backend`
- Admin/POS: `NEXT_PUBLIC_*` / `EXPO_PUBLIC_*` zur **Build-Zeit** setzen
- Backend-Build-Kontext = **Repository-Root** (`-f backend/Dockerfile`)
- Soft-TSE nur Development — Produktion fail-closed

Vollständige Checkliste und Architektur: [`DOCKER_SETUP.md`](DOCKER_SETUP.md).
