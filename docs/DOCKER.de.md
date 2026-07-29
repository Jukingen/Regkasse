# Docker — Regkasse (Deutsch)

Zentrale Übersicht für die Docker-Nutzung (lokal und produktionsnah) in diesem Monorepo.

| Sprache | Dokument |
|---------|----------|
| **Deutsch (diese Seite)** | [`DOCKER.de.md`](DOCKER.de.md) |
| **English** | [`DOCKER.md`](DOCKER.md) |

**Zuletzt aktualisiert:** 2026-07-29

---

## Schnellübersicht

| Ziel | Befehl / Datei |
|------|----------------|
| **Einrichtung & Migrationsplan** | [`DOCKER_SETUP.de.md`](DOCKER_SETUP.de.md) ([EN](DOCKER_SETUP.md)) |
| Docker Desktop unter Windows installieren | [`DOCKER_WINDOWS_SETUP.de.md`](DOCKER_WINDOWS_SETUP.de.md) |
| Docker-Probleme unter Windows lösen | [`DOCKER_WINDOWS_TROUBLESHOOTING.de.md`](DOCKER_WINDOWS_TROUBLESHOOTING.de.md) · `.\scripts\docker-diagnose.ps1` |
| Images bauen | `.\scripts\docker-build.ps1` |
| Dev starten / stoppen | `.\scripts\docker-up.ps1` · `.\scripts\docker-down.ps1` |
| Prod-nah deployen | `.\scripts\docker-deploy.ps1` |
| Vollständiger Dev-Stack (Soft-TSE) | `docker compose up --build` |
| Nur Infrastruktur | `docker compose -f docker-compose.dev.yml up -d` |
| Produktionsnah | `docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build` |

---

## Compose-Dateien

| Datei | Zweck |
|-------|--------|
| [`docker-compose.yml`](../docker-compose.yml) | Postgres, Redis, API, Admin; optionale Profile `pos` / `sites` |
| [`docker-compose.override.yml`](../docker-compose.override.yml) | Wird bei `docker compose up` automatisch geladen — Soft-TSE (`Demo`/`Fake`) + FON-Simulation |
| [`docker-compose.dev.yml`](../docker-compose.dev.yml) | Nur Postgres + Redis (Hot-Reload auf dem Host mit `npm run dev`) |
| [`docker-compose.prod.yml`](../docker-compose.prod.yml) | Produktionshost — Device/Real-TSE; **Override nicht** mitladen |

### Dockerfiles

| Dienst | Pfad | Hinweise |
|--------|------|----------|
| API | [`backend/Dockerfile`](../backend/Dockerfile) | Build-Kontext = **Repository-Root** |
| Admin | [`frontend-admin/Dockerfile`](../frontend-admin/Dockerfile) | `NEXT_PUBLIC_*` zur **Build-Zeit** |
| POS-Web | [`frontend/Dockerfile`](../frontend/Dockerfile) | Expo-Export → nginx; Profil `pos` |
| Sites | [`frontend-sites/Dockerfile`](../frontend-sites/Dockerfile) | Profil `sites` |

---

## Entwicklungsmodi

### A — Infrastruktur in Docker, Apps auf dem Host (empfohlen zum Codieren)

```bash
docker compose -f docker-compose.dev.yml up -d
npm run dev
```

API auf `localhost:5432`, Redis auf `localhost:6379` konfigurieren.

### B — Kompletter Stack in Docker (Soft-TSE)

```bash
copy .env.example .env
# JWT_SECRET_KEY ≥ 32 Zeichen setzen
docker compose up --build
```

Die Override-Datei setzt Development-Fiskaldefaults (kein echtes Device-TSE). Details (EN): [`DEVELOPMENT.md`](../DEVELOPMENT.md#docker-development-workflow).

### C — Produktionsnah (kein Soft-TSE)

```bash
copy .env.production.example .env.production
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
```

Start schlägt fehl, wenn Soft-TSE / FON-Simulation in Production landet. Siehe [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md).

**Wichtig (Österreich / RKSV):** Soft-TSE und FON-Simulation sind nur für **Development** erlaubt. Produktivbetrieb erfordert Device/Real-TSE und abgeschlossene Cutover-Checklisten.

---

## URLs (Standard)

| URL | Dienst |
|-----|--------|
| http://localhost:5184 | API |
| http://localhost:3000 | Admin (FA) |
| http://localhost:8081 | POS-Web (`--profile pos`) |
| http://localhost:3001 | Sites (`--profile sites`) |
| localhost:5432 / 6379 | Postgres / Redis |

Browser müssen **`localhost`** aufrufen, nicht den Docker-DNS-Namen `backend`.

---

## Make / Just

| Ziel | Aktion |
|------|--------|
| `just docker-up` / `make docker-up` | Dev-Stack (+ Override) |
| `just docker-up-dev` | Nur Infrastruktur |
| `just docker-up-prod` | Benötigt `.env.production` |
| `just docker-up-pos` | Dev-Stack + POS-Profil |
| `just docker-down` | Stacks stoppen |

---

## Weiterführend

- Root-Übersicht (EN): [`../README.md`](../README.md#docker-compose)
- Windows Setup EN: [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md)
- Windows Troubleshooting EN: [`DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md)
