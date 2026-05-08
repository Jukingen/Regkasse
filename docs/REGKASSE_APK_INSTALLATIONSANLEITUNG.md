# Regkasse POS – Installationsanleitung für Android-Tablets

> Dieses Dokument richtet sich an **Gastronomie- und Geschäftsbetreiber:innen**, die die Regkasse POS-App ohne Google Play Store auf einem Android-Tablet installieren möchten.

---

## 0. Voraussetzungen

- Android-Tablet mit **Android 8.0 oder höher**.
- Mindestens **1 GB freier Speicher**.
- Eine Internetverbindung (für den Login und die Synchronisation mit dem Kassen-Backend).
- Die **APK-Datei** der Regkasse POS-App. Diese erhalten Sie:
  - per **E-Mail-Anhang** (oder Download-Link in der E-Mail) oder
  - per **USB-Stick / USB-Kabel** vom IT-Verantwortlichen.

> **Hinweis zur Sicherheit:** Installieren Sie die APK ausschließlich aus offiziellen, von Ihrem Anbieter bestätigten Quellen. Öffnen Sie keine APK-Dateien aus unbekannten Nachrichten oder Webseiten.

---

## 1. Schritt 1 – „Aus unbekannten Quellen installieren" erlauben

Damit Android die APK-Datei akzeptiert, muss die Installation aus unbekannten Quellen einmalig freigegeben werden. Der genaue Menüpunkt unterscheidet sich je nach Hersteller (Samsung, Lenovo, Huawei, …).

### Android 8 oder neuer (empfohlen)

1. Öffnen Sie **Einstellungen**.
2. Wählen Sie **Apps** (oder „Apps & Benachrichtigungen").
3. Tippen Sie auf **Spezieller Zugriff** ▸ **Unbekannte Apps installieren**.
4. Suchen Sie die App, mit der Sie die APK-Datei öffnen werden:
   - **Dateien** / **Eigene Dateien** (für USB-Stick oder lokale Kopie),
   - **Chrome** oder Ihr Browser (für Download-Link),
   - **Gmail** / **Outlook** (für E-Mail-Anhang).
5. Setzen Sie den Schalter **„Aus dieser Quelle zulassen"** auf **EIN**.

### Ältere Android-Versionen (7 oder älter, falls noch im Einsatz)

1. **Einstellungen** ▸ **Sicherheit**.
2. Aktivieren Sie **Unbekannte Quellen**.
3. Bestätigen Sie den Sicherheitshinweis mit **OK**.

> Sie können die Berechtigung später wieder deaktivieren – die einmal installierte App bleibt dabei erhalten.

---

## 2. Schritt 2 – APK-Datei auf das Tablet bringen

Wählen Sie eine der folgenden Varianten:

### Variante A – Per E-Mail

1. Öffnen Sie auf dem Tablet die E-Mail mit der APK (oder dem Download-Link), die Sie von Ihrem Anbieter erhalten haben.
2. **Bei E-Mail-Anhang:** Tippen Sie auf den Anhang `Regkasse-1.0.0-release.apk` (oder ähnlich). Wählen Sie **Herunterladen**.
3. **Bei Download-Link:** Tippen Sie auf den Link. Der Browser lädt die APK-Datei in den Ordner **Downloads**.

### Variante B – Per USB-Stick / USB-Kabel

1. Schließen Sie den USB-Stick (oder das Tablet per USB-Kabel an den PC) an.
2. Kopieren Sie die Datei `Regkasse-…release.apk` in den Ordner **Downloads** auf dem Tablet.
3. Trennen Sie den USB-Stick / das USB-Kabel sicher.

> **Tipp:** Notieren Sie sich den genauen Dateinamen und den Speicherort. Sie benötigen ihn für Schritt 3.

---

## 3. Schritt 3 – APK öffnen und installieren

1. Öffnen Sie die App **Dateien** (oder **Eigene Dateien**).
2. Navigieren Sie in den Ordner **Downloads**.
3. Tippen Sie auf die Datei `Regkasse-…release.apk`.
4. Android zeigt einen Sicherheitsdialog mit den Berechtigungen, die die App anfordert.  
   Bestätigen Sie mit **Installieren**.
5. Warten Sie, bis die Meldung **App installiert** erscheint.
6. Tippen Sie auf **Öffnen**, um Regkasse POS zu starten – oder auf **Fertig**, um später zu starten.

> Sollte Android die Installation blockieren, wiederholen Sie Schritt 1 und prüfen Sie, ob die richtige Quell-App freigegeben wurde.

---

## 4. Schritt 4 – Erster Start und Berechtigungen

Beim ersten Start fragt Regkasse POS nach den Berechtigungen, die für den Kassenbetrieb erforderlich sind. Bitte erteilen Sie alle angefragten Zugriffe.

### 4.1 Übliche Anfragen

| Berechtigung | Zweck | Empfehlung |
|---|---|---|
| **Speicher / Dateien und Medien** | Belege als PDF speichern, Backups exportieren, Logos verwenden | **Zulassen** |
| **Mitteilungen / Benachrichtigungen** | Hinweise zu Monatsbeleg, Lizenzablauf, TSE-Status | **Zulassen** |
| **Standort (optional)** | Nur falls Ihr Anbieter eine standortabhängige Funktion eingerichtet hat | Nach Absprache |

### 4.2 Falls ein Bluetooth-Bonprinter verwendet wird

Wenn Sie einen **Bluetooth-Bondrucker** angebunden haben:

1. Aktivieren Sie auf dem Tablet zuerst **Bluetooth** in den Schnelleinstellungen.
2. Verbinden Sie den Drucker einmalig in den Tablet-Einstellungen unter **Bluetooth** ▸ **Neues Gerät koppeln**.
3. Erlauben Sie Regkasse POS bei der ersten Druckanforderung den Zugriff auf **Bluetooth-Geräte in der Nähe** bzw. **Geräte in der Nähe**.

> **Hinweis:** Wenn Ihr Drucker per **WLAN** oder **USB** angebunden ist, ist eine Bluetooth-Berechtigung nicht erforderlich. Den Druck steuert in vielen Konfigurationen das Android-System über den **Druckdienst** des Druckerherstellers (z. B. Epson, Star, Brother).

### 4.3 Anmeldung

Nach den Berechtigungsdialogen erscheint der **Anmeldebildschirm**. Bitte melden Sie sich mit den Zugangsdaten an, die Sie von Ihrem Anbieter erhalten haben.

---

## 5. Manuelle Aktualisierung (neue APK-Version)

Da Regkasse POS **nicht** über den Google Play Store verteilt wird, müssen Updates manuell eingespielt werden, sobald Ihr Anbieter eine neue APK bereitstellt.

### 5.1 Vorbereitung

- Schließen Sie den **Tagesabschluss** ab, falls noch nicht erfolgt.
- Stellen Sie sicher, dass **kein offener Zahlungsvorgang** läuft.
- Optional: Erstellen Sie ein **Backup**, falls Ihr Anbieter dies vorsieht.

### 5.2 Schritte

1. Laden Sie die neue APK-Datei auf das Tablet (siehe **Schritt 2**).
2. Öffnen Sie die neue APK-Datei (siehe **Schritt 3**).
3. Android erkennt eine vorhandene Installation und zeigt **„App aktualisieren?"**.  
   Bestätigen Sie mit **Aktualisieren**.
4. Bestehende Daten und Anmeldungen bleiben erhalten, sofern der **Paketname** identisch ist (`com.registrierkasse.cashregister`).
5. Tippen Sie nach Abschluss auf **Öffnen**, um die neue Version zu starten.

> **Wichtig:** Installieren Sie eine neue Version **niemals** mit einem anderen Paketnamen oder Signaturschlüssel — Android lehnt das Update sonst ab und Ihre lokalen Daten könnten verloren gehen.

### 5.3 Versionsprüfung

Sie können die installierte Version jederzeit prüfen unter:

- **Einstellungen** ▸ **Apps** ▸ **Cash Register / Regkasse POS** ▸ **Version**.

Innerhalb der App finden Sie die Version normalerweise unter **Einstellungen** ▸ **Über die App**.

---

## 6. Häufige Fragen / Problembehebung

**Die Installation startet nicht – Android zeigt „App nicht installiert".**  
Prüfen Sie, ob:
- die Berechtigung „Aus dieser Quelle zulassen" für Ihre Datei-App aktiv ist (Schritt 1),
- genug freier Speicher vorhanden ist,
- die APK-Datei vollständig heruntergeladen wurde (kein Abbruch der E-Mail/Download).

**Beim Update erscheint „Paket steht im Konflikt mit einem bereits installierten Paket".**  
Das bedeutet, die neue APK wurde mit einem **anderen Signaturschlüssel** signiert oder hat einen anderen Paketnamen. Bitte kontaktieren Sie den Support — eine Deinstallation würde lokale Daten löschen.

**Belege drucken nicht.**  
- Bluetooth-Drucker: Tablet ▸ Bluetooth ▸ Drucker erneut koppeln.
- WLAN-/USB-Drucker: Druckerdienst des Herstellers im Tablet aktivieren  
  (**Einstellungen** ▸ **Verbindungen** / **Allgemein** ▸ **Drucken**).

**Die App fragt erneut nach Berechtigungen.**  
Das ist normal nach Updates oder größeren Android-Versionssprüngen. Bitte erneut zulassen.

**Mein Tablet zeigt „Sicherheitsbedenken".**  
Aktivieren Sie die Quell-App nur kurzzeitig (Schritt 1) und deaktivieren Sie die Berechtigung anschließend wieder. Die installierte Regkasse POS-App bleibt auch dann betriebsbereit.

---

## 7. Support

Bei Fragen oder Problemen wenden Sie sich bitte an Ihren Regkasse-Anbieter:

| Kontakt | Information |
|---|---|
| Ansprechpartner / Firma | _______________________ |
| Telefon | _______________________ |
| E-Mail | _______________________ |
| Geschäftszeiten | _______________________ |

---

> **Rechtlicher Hinweis:** Diese Anleitung beschreibt ausschließlich die technische Installation der Regkasse POS-App auf einem Android-Tablet. Sie ersetzt keine RKSV-/Steuerberatung und keine offiziellen Dokumente Ihres Anbieters. Die Verantwortung für den ordnungsgemäßen Kassenbetrieb verbleibt beim Betreiber.
