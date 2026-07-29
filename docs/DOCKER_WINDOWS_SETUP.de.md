# Docker Desktop unter Windows einrichten (WSL 2)

Schritt-für-Schritt-Anleitung für **Docker Desktop** mit **WSL-2-Backend**, damit Regkasse lokal mit Compose, Postgres, Redis und Testcontainers laufen kann.

| Sprache | Dokument |
|---------|----------|
| **Deutsch (diese Seite)** | [`DOCKER_WINDOWS_SETUP.de.md`](DOCKER_WINDOWS_SETUP.de.md) |
| **English** | [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md) |

**Verwandt:** Hub [`DOCKER.de.md`](DOCKER.de.md) · Probleme [`DOCKER_WINDOWS_TROUBLESHOOTING.de.md`](DOCKER_WINDOWS_TROUBLESHOOTING.de.md) · Diagnose `.\scripts\docker-diagnose.ps1`

**Zuletzt aktualisiert:** 2026-07-29

---

## Voraussetzungen

| Anforderung | Hinweise |
|-------------|----------|
| **OS** | Windows 10 64-Bit (22H2+) oder Windows 11 64-Bit |
| **CPU** | 64-Bit mit SLAT; Virtualisierung (Intel VT-x / AMD-V) im BIOS/UEFI **aktiv** |
| **RAM** | **8 GB** System-RAM empfohlen (Docker Desktop / WSL 2) |
| **Festplatte** | ≥ **20 GB** frei |
| **Rechte** | Administrator für Windows-Features und Neustart |

```powershell
systeminfo | findstr /B /C:"OS Name" /C:"OS Version"
wsl --status
```

---

## Ablauf

```text
1. WSL + Virtual Machine Platform aktivieren
2. Standard-WSL-Version auf 2 setzen
3. Docker Desktop installieren (WSL-2-Backend)
4. Windows neu starten
5. WSL-Integration + Ressourcen konfigurieren
6. Prüfen: docker / compose / hello-world
7. (Optional) Regkasse-Compose starten
```

---

## 1. WSL 2 aktivieren

**PowerShell als Administrator:**

```powershell
dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
wsl --set-default-version 2
```

Oder (Windows 11 / aktuelle Windows 10):

```powershell
wsl --install
wsl --set-default-version 2
wsl --update
```

Distribution (z. B. Ubuntu):

```powershell
wsl --install -d Ubuntu
wsl -l -v
```

Die Spalte **VERSION** muss **2** zeigen. Sonst: `wsl --set-version Ubuntu 2`.

Danach **Windows neu starten**.

---

## 2. Docker Desktop installieren

1. Download: [https://www.docker.com/products/docker-desktop/](https://www.docker.com/products/docker-desktop/)
2. Installer ausführen — **Use WSL 2 instead of Hyper-V** aktiv lassen
3. Bei Aufforderung **neu starten**
4. Docker Desktop starten und Onboarding abschließen

Im Infobereich muss der Status **Running** sein.

---

## 3. Installation prüfen

Neues Terminal öffnen:

```powershell
docker --version
docker compose version
docker run hello-world
```

Erwartet: Docker-Version, Compose **v2**, Erfolgsmeldung von `hello-world`.

---

## 4. Docker Desktop konfigurieren

### WSL-Integration

**Settings → Resources → WSL Integration**

- Integration mit der Standard-Distribution aktivieren
- Gewünschte Distros (z. B. Ubuntu) aktivieren → **Apply & restart**

**Settings → General:** **Use the WSL 2 based engine** muss aktiv sein.

### Ressourcen

**Settings → Resources** (mindestens):

| Ressource | Minimum für Regkasse | Komfortabel |
|-----------|----------------------|-------------|
| **Memory** | **4 GB** | 6–8 GB |
| **CPUs** | **2** | 4 |

Optional `%UserProfile%\.wslconfig`:

```ini
[wsl2]
memory=6GB
processors=4
swap=2GB
```

Danach: `wsl --shutdown` und Docker Desktop neu starten.

---

## 5. Regkasse-Smoke-Test

```powershell
copy .env.example .env
# JWT_SECRET_KEY ≥ 32 Zeichen setzen

docker compose up --build
# Optional POS / Sites:
docker compose --profile pos --profile sites up --build
```

Oder nur Datenbanken:

```powershell
docker compose -f docker-compose.dev.yml up -d
npm run dev
```

Gesundheitscheck:

```powershell
curl -fsS http://localhost:5184/api/health/live
```

---

## Checkliste

- [ ] WSL + Virtual Machine Platform; Standardversion **2**
- [ ] Mindestens eine WSL-2-Distribution (`wsl -l -v`)
- [ ] Docker Desktop mit WSL-2-Backend; PC neu gestartet
- [ ] WSL-Integration aktiv
- [ ] Ressourcen ≥ 4 GB RAM / 2 CPUs
- [ ] `docker --version`, `docker compose version`, `hello-world` OK
- [ ] (Optional) `docker compose up --build` funktioniert

---

## Weiterführend

- Probleme: [`DOCKER_WINDOWS_TROUBLESHOOTING.de.md`](DOCKER_WINDOWS_TROUBLESHOOTING.de.md)
- Hub: [`DOCKER.de.md`](DOCKER.de.md)
- Englische Detailfassung: [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md)
- Dev-Workflow (EN): [`../DEVELOPMENT.md`](../DEVELOPMENT.md#docker-compose-full-stack)
