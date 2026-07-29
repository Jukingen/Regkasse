# Docker unter Windows — Fehlerbehebung

Leitfaden für Regkasse-Entwickler mit **Docker Desktop + WSL 2**.

| Sprache | Dokument |
|---------|----------|
| **Deutsch (diese Seite)** | [`DOCKER_WINDOWS_TROUBLESHOOTING.de.md`](DOCKER_WINDOWS_TROUBLESHOOTING.de.md) |
| **English** | [`DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md) |

**Zuerst einrichten:** [`DOCKER_WINDOWS_SETUP.de.md`](DOCKER_WINDOWS_SETUP.de.md)  
**Compose:** [`DOCKER.de.md`](DOCKER.de.md) · [`../DEVELOPMENT.md`](../DEVELOPMENT.md#docker-compose-full-stack)

```powershell
.\scripts\docker-diagnose.ps1
```

**Zuletzt aktualisiert:** 2026-07-29

---

## Schnellhilfe

| Problem | Lösung |
|---------|--------|
| WSL 2 fehlt | `wsl --install` (Admin-PowerShell), dann neu starten |
| Docker Desktop startet nicht | Virtual Machine Platform + WSL 2 aktivieren; `wsl --update`; BIOS-Virtualisierung prüfen |
| Port belegt (z. B. 5432) | `netstat -ano \| findstr :5432` → Prozess beenden oder Port in `.env` ändern |
| Volume- / Freigabefehler | Settings → Resources → File sharing — oder Repo unter `\\wsl$\…` |
| `docker` unbekannt | Docker Desktop starten; **neues** Terminal öffnen |
| Engine hängt bei „starting…“ | Desktop beenden → `wsl --shutdown` → Desktop neu starten |

---

## 1. Installationsprobleme

### WSL 2 fehlt oder unvollständig

```powershell
# PowerShell als Administrator
wsl --install
wsl --set-default-version 2
wsl --update
```

Features manuell:

```powershell
dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
```

Neu starten, Distro prüfen: `wsl -l -v` (VERSION = **2**). Kernel-Update: [aka.ms/wsl2kernel](https://aka.ms/wsl2kernel).

### Docker Desktop startet nicht / Virtualisierung

1. BIOS/UEFI: VT-x / AMD-V / SVM **ein**
2. Windows-Features: **Virtual Machine Platform** (Pflicht für WSL 2)
3. Task-Manager → Leistung → CPU → Virtualisierung: **Aktiviert**
4. `wsl --update`, Docker Desktop neu starten, **WSL 2 based engine** aktiv

### Distribution noch auf WSL 1

```powershell
wsl --set-version Ubuntu 2
```

---

## 2. Laufzeitprobleme

### Engine antwortet nicht

```powershell
docker info
wsl --shutdown
# Docker Desktop starten, warten bis „Running“
docker run --rm hello-world
```

### Port bereits belegt

Regkasse-Standards: **5184**, **5432**, **6379**, **3000**, **8081**, **3001**.

```powershell
netstat -ano | findstr "5184 5432 6379 3000 8081 3001"
```

Prozess beenden oder in `.env` z. B. `POSTGRES_PORT=5433` setzen.  
Hinweis: `.\scripts\start-redis-dev.ps1` und Compose-Redis konkurrieren um **6379**.

### Compose unhealthy / Backend beendet sich

```powershell
docker compose ps
docker compose logs backend --tail 100
```

| Prüfung | Maßnahme |
|---------|----------|
| JWT zu kurz | `JWT_SECRET_KEY` ≥ 32 Zeichen in `.env` |
| Soft-TSE in Prod | **Kein** `docker-compose.override.yml` mit `docker-compose.prod.yml` |
| Prod-TSE-Lock | Fiskaly-Secrets in `.env.production` |

### Volume- / Berechtigungsfehler

- Settings → **Resources → File sharing** (Laufwerk mit dem Repo freigeben)
- Oder Repo unter WSL: `\\wsl$\Ubuntu\home\<user>\Regkasse` (schneller, weniger Freigabeprobleme)
- Regkasse bind-mountet absichtlich **nicht** `./backend` auf `/app` (veröffentlichte DLLs)

```powershell
docker compose down          # Daten behalten
# docker compose down -v     # löscht Volumes — Datenverlust!
```

---

## 3. Leistungsprobleme

| Ursache | Fix |
|---------|-----|
| Zu wenig WSL-RAM | Resources ≥ 4 GB / 2 CPUs oder `.wslconfig` |
| Repo auf NTFS (`C:\`) | Für schwere Builds unter WSL-Dateisystem arbeiten |
| Defender-Scan | Freigaben nur mit IT-Freigabe |
| OOM beim Build | Speicher erhöhen; `docker compose build backend` einzeln |

Beispiel `%UserProfile%\.wslconfig`:

```ini
[wsl2]
memory=6GB
processors=4
swap=2GB
```

```powershell
wsl --shutdown
```

---

## 4. Netzwerkprobleme

| Symptom | Fix |
|---------|-----|
| Images lassen sich nicht pullen | Proxy unter Settings → Resources → Proxies; Firmen-CA; `docker login` |
| Browser erreicht API/Admin nicht | **`localhost`** nutzen (nicht `backend`); Admin nach `NEXT_PUBLIC_*`-Änderung neu bauen |
| VPN bricht WSL | Kurz ohne VPN testen |
| Firewall | Docker Desktop erlauben |

---

## 5. Diagnoseskript

```powershell
.\scripts\docker-diagnose.ps1
```

Prüft Docker-CLI, Compose, WSL, Engine und Regkasse-Ports. Exit-Code `0` = keine harten Fehler.

Manuell:

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

## 6. Immer noch blockiert?

1. `.\scripts\docker-diagnose.ps1` ausführen und den fehlgeschlagenen Schritt notieren
2. `docker compose ps` und `docker compose logs backend --tail 200` sichern
3. Checkliste in [`DOCKER_WINDOWS_SETUP.de.md`](DOCKER_WINDOWS_SETUP.de.md) erneut durchgehen
4. Letzter Ausweg: Desktop beenden → `wsl --shutdown` → Reboot → Desktop → `docker run --rm hello-world`

**Nicht** `docker compose down -v` auf produktnahen Daten ausführen, wenn der Datenverlust nicht akzeptiert ist.

Englische Vollfassung: [`DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md).
