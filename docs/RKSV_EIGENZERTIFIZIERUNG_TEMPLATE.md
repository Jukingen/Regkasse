# RKSV Eigenzertifizierung – Regkasse POS

> **Vorlage zur Selbstdeklaration durch den Kassenbetreiber.**
> Diese Vorlage beschreibt die technische Implementierung der Registrierkassen-Software **Regkasse POS** (Repository-Stand zum Datum dieser Version) und stellt die Felder bereit, die der Kassenbetreiber im Rahmen seiner gesetzlichen Pflichten (RKSV) selbst dokumentieren und unterschreiben muss.
>
> **Wichtig (rechtlicher Hinweis):**
> Dieses Dokument ist **keine** automatische Konformitätserklärung des Softwareherstellers gegenüber dem Bundesministerium für Finanzen (BMF) und **kein** rechtsverbindlicher Nachweis im Sinne der RKSV oder einer Außenprüfung. Die rechtliche Verantwortung für die Vollständigkeit und Richtigkeit der hier eingetragenen Daten und der unterschriebenen Erklärung liegt ausschließlich beim Kassenbetreiber. Eine steuerlich-rechtliche Beratung wird empfohlen.

---

| Metadatum | Wert |
|---|---|
| Vorlagenname | RKSV Eigenzertifizierung – Regkasse POS |
| Vorlagenversion | 1.0 |
| Software | Regkasse POS (ASP.NET Core / .NET 10, EF Core / PostgreSQL) |
| Stand des Dokuments (Datum) | _______________ (vom Betreiber auszufüllen) |
| Erstellt am (UTC) | _______________ (vom Betreiber auszufüllen) |
| Gültig ab | _______________ (vom Betreiber auszufüllen) |

---

## 1. Unternehmen und Registriernummer

| Feld | Wert (vom Betreiber auszufüllen) |
|---|---|
| Firmenwortlaut | _______________________________________ |
| Anschrift (Straße, PLZ, Ort) | _______________________________________ |
| UID-Nummer (`ATU` + 8 Ziffern) | _______________________________________ |
| Steuernummer | _______________________________________ |
| Firmenbuchnummer (FN) bzw. Registriernummer | _______________________________________ |
| Zuständiges Finanzamt | _______________________________________ |
| Verantwortliche Person | _______________________________________ |
| Funktion / Vertretungsbefugnis | _______________________________________ |
| Kontakt (E-Mail / Telefon) | _______________________________________ |

> **Technischer Hinweis:** Die UID-Nummer wird im Backend pro Zahlung in der Spalte `payment_details.steuernummer` mit dem Format `ATU` + 8 Ziffern validiert (Regulärer Ausdruck `^ATU\d{8}$`). Sie erscheint auf jedem fiskalischen Beleg.

---

## 2. Kassen-ID (Kassenidentifikationsnummer)

### 2.1 Format

- **Spaltenname:** `cash_registers.register_number`
- **Wertebereich:** Frei wählbarer alphanumerischer Wert, **maximal 20 Zeichen**.
- **Eindeutigkeit:** Pro Mandant (Tenant) eindeutig.
- **Beispiel (Default in `appsettings.example.json`):** `KASSE-001`

### 2.2 Verwendung der Kassen-ID

Die Kassen-ID wird eingebettet in:

- jede **Belegnummer** (vgl. Abschnitt 3),
- jeden **RKSV-QR-Code** (vgl. Abschnitt 7),
- jeden **TSE-Signatur-Datensatz** (vgl. Abschnitt 4.3 / Tabelle `TseSignatures`),
- jede Eintragung in `signature_chain_state` (vgl. Abschnitt 5).

### 2.3 Konkrete Kassen dieser Installation

| Lfd. Nr. | Kassen-ID (`register_number`) | Standort | Inbetriebnahme | Status |
|---|---|---|---|---|
| 1 | _______________ | _______________ | _______________ | _______________ |
| 2 | _______________ | _______________ | _______________ | _______________ |
| 3 | _______________ | _______________ | _______________ | _______________ |

> **Hinweis:** Eine Kasse mit Status `Decommissioned` (siehe Schlussbeleg, Abschnitt 6) darf **keine** neuen Sitzungen, Zahlungen oder Belege mehr annehmen.

---

## 3. Belegnummer / Receipt Number (Sequenz)

### 3.1 Format

```
AT-{KassenId}-{yyyyMMdd}-{Sequenz}
```

**Beispiel:** `AT-KASSE-001-20260115-42`
(42. Beleg auf der Kasse `KASSE-001` am 15. Januar 2026 UTC.)

### 3.2 Sequenzregeln

| Regel | Implementierung |
|---|---|
| **Start des Zählers** | Der Zähler beginnt **bei `1`** für jede Kombination `(cash_register_id, sequence_date)`. |
| **Reset-Frequenz** | **Tagesreset (täglich):** Beim Wechsel des UTC-Datums beginnt eine neue Sequenz. **Kein** monatlicher und **kein** jährlicher Reset. |
| **Lückenlosigkeit** | Allokation transaktional in Tabelle `receipt_sequences` über PostgreSQL `INSERT … ON CONFLICT (cash_register_id, sequence_date) DO UPDATE … RETURNING (next_sequence - 1)`. |
| **Sequentielles Hochzählen** | Innerhalb desselben Tages wird der Zähler um genau `1` erhöht; parallele Erstellungen werden durch die Datenbank serialisiert. |
| **Eindeutigkeit der Belegnummer** | Eindeutig pro Mandant + Kasse + Tag + Sequenz. |
| **Verwendung für Sonderbelege** | RKSV-Sonderbelege (Abschnitt 6) verwenden **dieselbe** Sequenz; es existiert keine separate Sonderbeleg-Nummerierung. |

### 3.3 Diagnose

Lückenprüfungen über alle Belege einer Kasse pro Tag werden vom Diagnose-Endpunkt `GET /api/admin/rksv/compliance-report` (Prüfschritt 3 „Receipt number sequence gaps") bereitgestellt.

---

## 4. TSE-Signatureinheit

### 4.1 Konfigurationsmodi (`appsettings.json`, Abschnitt `Tse`)

#### 4.1.1 Signaturmodus (`Tse.Mode`)

| Wert | Implementierung | Verwendung |
|---|---|---|
| `Real` | `RealTseProvider` + `SignaturePipeline`; reale TSE-Anbindung mit echtem Schlüsselmaterial; RKSV §6 Compact JWS. | **Produktivbetrieb.** |
| `Fake` | `FakeTseProvider`; deterministische Pseudo-Signaturen ohne kryptografische Wirkung. | **Entwicklung / Demo / Tests.** Nicht für den Echtbetrieb. |

> Die Auswahl erfolgt zur Laufzeit über `TseOptions.IsFakeSigningMode` (`backend/ApplicationHost.cs`).

#### 4.1.2 Gerätemodus (`Tse.TseMode`)

| Wert | Wirkung |
|---|---|
| `Device` | Erfordert reale TSE-Geräteverfügbarkeit; periodischer Health-Check (`HealthCheckIntervalSeconds`, Default 30 s); Offline-/Degraded-Erkennung über `OfflineAfterConsecutiveFailures` und `DegradedAfterConsecutiveFailures`. |
| `Demo` | Erlaubt Soft-TSE-Nutzung ohne Hardwaregerät; ausschließlich für Test- und Demonstrationszwecke. |
| `Off` | TSE deaktiviert; Signaturen werden nicht erzeugt. **Nicht für den Echtbetrieb.** |

### 4.2 Eingesetzte TSE für diese Installation

| Feld | Wert (vom Betreiber auszufüllen) |
|---|---|
| TSE-Hersteller / Bauart | _______________ (z. B. Epson-TSE, fiskaly, andere) |
| Modell-/Produktbezeichnung | _______________ |
| Seriennummer / Geräte-ID | _______________ |
| Zertifikatsseriennummer (TSE-Zertifikat) | _______________ |
| Zertifikatsgültigkeit (von / bis) | _______________ / _______________ |
| Inbetriebnahme der TSE | _______________ |
| `Tse.Mode` (in `appsettings.json`) | `Real` ☐ &nbsp; `Fake` ☐ |
| `Tse.TseMode` (in `appsettings.json`) | `Device` ☐ &nbsp; `Demo` ☐ &nbsp; `Off` ☐ |
| Bemerkungen | _______________ |

### 4.3 Signaturalgorithmus

- **Format:** RKSV §6 Compact JWS (`compact JWS = base64url(header) + "." + base64url(payload) + "." + base64url(signature)`).
- **Persistenz pro Beleg:** `payment_details.tse_signature` und `receipts.signature_value` enthalten den vollständigen Compact-JWS-Wert; ergänzend werden zerlegte Bestandteile (`jws_header`, `jws_payload`, `jws_signature`) sowie `signature_format`, `provider`, `correlation_id` gespeichert.
- **Vorgängersignatur:** `payment_details.prev_signature_value_used` und `receipts.prev_signature_value` halten die zur Verkettung verwendete Vorgängersignatur fest (vgl. Abschnitt 5).
- **TSE-Log:** Tabelle `TseSignatures` führt einen separaten Signaturlog (Geräte-ID, Zertifikatsnummer, Validierungsstatus, Zeitstempel).

---

## 5. Signaturkette (Verkettung der Belege)

### 5.1 Zustandsführung

Pro Kasse existiert genau eine Zeile in der Tabelle `signature_chain_state`:

| Spalte | Bedeutung |
|---|---|
| `cash_register_id` | Eindeutige Zuordnung pro Kasse. |
| `last_signature` | Die zuletzt für diese Kasse erzeugte TSE-Signatur. |
| `last_counter` | Monoton steigender Zähler über alle Belege dieser Kasse. |
| `updated_at` | Zeitpunkt der letzten Aktualisierung. |

### 5.2 Verkettung pro Beleg

Bei der Erstellung eines neuen Belegs (regulärer Verkauf, Refund, Storno **oder** Sonderbeleg gemäß Abschnitt 6) führt die Software folgende Schritte atomar in einer einzigen Datenbanktransaktion aus:

1. Belegnummer wird aus `receipt_sequences` allokiert (siehe Abschnitt 3.2).
2. Die Zeile in `signature_chain_state` wird mit `SELECT … FOR UPDATE` gesperrt; parallele Belege auf derselben Kasse werden serialisiert.
3. `prev_signature_value` wird aus dem aktuellen `last_signature` der Kasse gelesen.
4. Der TSE-Signaturpayload wird gebildet (Belegdaten + `prev_signature_value`).
5. Die TSE-Signatur (Compact JWS) wird vom konfigurierten `ITseProvider` erzeugt.
6. Die neuen Werte werden in `payment_details`, `receipts`, ggf. `invoices` und `signature_chain_state` gespeichert.
7. Die Transaktion wird festgeschrieben (Commit). Bei Fehlern in einem der Schritte wird die gesamte Transaktion zurückgerollt; teilbestätigte Signaturen sind ausgeschlossen.

### 5.3 Verifikation der Verkettung

Die Korrektheit der Kette kann nachträglich geprüft werden, indem für die Belege einer Kasse, geordnet nach `issued_at` und `receipt_number`, die Bedingung

```
r_n.prev_signature_value == r_{n-1}.signature_value
```

verifiziert wird. Diese Prüfung wird intern als Diagnose-Endpunkt `GET /api/admin/rksv/compliance-report` (Prüfschritt 2 „Signature chain continuity") bereitgestellt.

---

## 6. RKSV-Sonderbelege

Alle Sonderbelege sind **null-Euro Belege** mit gültiger TSE-Signatur und einer Belegnummer aus der laufenden Sequenz (Abschnitt 3). Die Erzeugung erfolgt über dedizierte API-Endpunkte:

| Sonderbeleg | API-Endpunkt | Auslöser im Betrieb | Eindeutigkeit / Sperre | Zusätzliche Statuswirkung |
|---|---|---|---|---|
| **Startbeleg** | `POST /api/rksv/special-receipts/startbeleg` | Inbetriebnahme der Kasse; POS-Sitzungsbeginn ist gesperrt, solange `Startbeleg` fehlt (`StartbelegRequired`). | Genau einer pro Kasse. | Erlaubt anschließend reguläre Verkäufe. |
| **Monatsbeleg** | `POST /api/rksv/special-receipts/monatsbeleg` | Admin-Panel oder POS-Erinnerung (`RksvReminderService`). | Genau einer pro Kasse + Wiener Kalendermonat. | – |
| **Jahresbeleg** | `POST /api/rksv/special-receipts/jahresbeleg` | Admin-Panel; **alternativ:** Dezember-Monatsbeleg kann den Jahresbeleg ersetzen, wenn `CompanySettings.UseDecemberMonatsbelegAsJahresbeleg = true` (Default). | Genau einer pro Kasse + Wiener Kalenderjahr. | Manuelle BMF-Belegcheck-Verifikation gemäß `docs/RKSV_BMF_BELEGCHECK_WORKFLOW.md`. |
| **Schlussbeleg / Endbeleg** | `POST /api/rksv/special-receipts/schlussbeleg` | Admin-Panel (Manager). Voraussetzung: Kasse geschlossen, keine offene Schicht. | Genau einer pro Kasse. | Setzt den Kassenstatus atomar auf `Decommissioned`; danach **keine** weiteren Sitzungen, Zahlungen oder Belege. |
| **Nullbeleg** | `POST /api/rksv/special-receipts/nullbeleg` | Admin-Panel oder Kontroll-Workflow. | Pro Kasse + Vienna-Kalendermonat eindeutig. | Beeinflusst keine Umsätze. |

### 6.1 Erinnerungen und Statusüberwachung

`RksvReminderService` veröffentlicht den konsolidierten Status (Startbeleg fehlt, Monatsbeleg fällig / überfällig, Jahresbeleg-Erwartung) sowohl an die POS-Anwendung als auch an das Admin-Panel, damit fällige Sonderbelege rechtzeitig erstellt werden.

### 6.2 Wichtige Hinweise

> **Schlussbeleg ist endgültig:** Der Schlussbeleg / Endbeleg ist ausschließlich für die **dauerhafte Außerbetriebnahme** einer Kasse vorgesehen — **nicht** für Pausen, Urlaub, saisonale Schließungen oder Filial-Umstellungen.

> **Frühzeitiger Jahresbeleg:** Wird der Jahresbeleg vorzeitig erstellt und der Betrieb fortgeführt, wird der laufende Verkauf vom Backend **nicht** technisch blockiert (Duplikat-Schutz für den Jahresbeleg selbst greift jedoch). Die operative Verantwortung verbleibt beim Kassenbetreiber.

---

## 7. QR-Code-Inhalt (RKSV-Beleg-QR)

Jeder fiskalische Beleg trägt einen QR-Code im internen RKSV-konformen Compact-Format:

```
_R1-AT1_{KassenId}_{Belegnummer}_{Zeitstempel}_{Bruttobetrag}_{Zweitbetrag}_{Zertifikatsseriennummer}_{Compact-JWS}
```

### 7.1 Segmente

| Segment | Inhalt | Quelle / Beispiel |
|---|---|---|
| **Präfix** | `_R1-AT1_` (RKSV-Maschinenpräfix, Version 1, AT, Layout 1) | konstant `_R1-AT1_` |
| **KassenId** | Identifikator der Kasse | `cash_registers.register_number`, z. B. `KASSE-001` |
| **Belegnummer** | Vollständige Belegnummer | `AT-KASSE-001-20260115-42` |
| **Zeitstempel** | ISO 8601 in UTC, Format `yyyy-MM-ddTHH:mm:ss` (ohne Zeitzonen-Suffix) | `2026-01-15T14:23:55` |
| **Bruttobetrag** | Gesamtbruttobetrag des Belegs; Dezimaltrennung `.` oder `,`, max. 2 Nachkommastellen | `12.40` |
| **Zweitbetrag** | Zweiter Betrag; in der aktuellen internen Variante **fix `0.00`** | `0.00` |
| **Zertifikatsseriennummer** | TSE-Zertifikatsseriennummer | `1A2B3C4D` |
| **Compact-JWS** | RKSV §6 Compact-JWS-Signatur über die Belegdaten + `prev_signature_value` | `eyJhbGc…` |

### 7.2 Format-Validator

Die Software stellt einen rein strukturellen Validator bereit:
`IRksvReceiptQrPayloadFormatValidator` / `RksvReceiptQrPayloadFormatValidator`.
Dieser Validator prüft Präfix, Anzahl der Segmente, Belegnummer-Muster, Zeitstempel-Format und Dezimal-Format der Beträge. **Eine kryptografische Verifikation der Signatur gegen einen externen Schlüssel erfolgt durch diesen Validator nicht.** Die kryptografische Belegprüfung erfolgt extern über die offizielle BMF Belegcheck App.

---

## 8. FinanzOnline-Anbindung

### 8.1 Aktueller Implementierungsstand

| Komponente | Status (Repository-Stand) |
|---|---|
| FinanzOnline Session-Webservice (SOAP) | Implementiert (`SoapFinanzOnlineSessionTransport`); aktivierbar mit konfigurierbarer `BaseUrl` und Zugangsdaten. Es existiert auch ein simulierter Client (`SimulatedFinanzOnlineSessionClient`) für Tests. |
| RKSV-Einreichung (Startbeleg / Jahresbeleg) | **Gehärtetes Skelett** (`FinanzOnline:RksvSubmission`). Default-Konfiguration: `ClientKind: "Fake"`, `Enabled: false`, `AllowOutboundNetworkCalls: false`. **In dieser Repository-Version werden keine produktiven ausgehenden SOAP-Aufrufe für die RKSV-Einreichung vorgenommen.** |
| Outbox / Reconciliation | Implementiert (Tabellen `finanz_online_outbox_messages`, `rksv_special_receipt_finanz_online_submissions`); Statusverfolgung über das Admin-Panel sichtbar; Retry-Job (`FinanzOnlineRetryJob`) integriert. |
| Manuelle BMF Belegcheck-App-Verifikation | **Erforderlicher operativer Workflow.** Siehe `docs/RKSV_BMF_BELEGCHECK_WORKFLOW.md`. |

### 8.2 Konfiguration für diese Installation

| Feld | Wert (vom Betreiber auszufüllen) |
|---|---|
| Teilnehmer-Identifikation (FinanzOnline) | _______________ |
| Telematik-ID (`FinanzOnlineTelematikId`) | _______________ |
| Hersteller-ID (`FinanzOnlineHerstellerId`) | _______________ |
| Endpunkt-URL (`FinanzOnlineApiUrl`) | _______________ |
| `RksvSubmission.ClientKind` | `Fake` ☐ &nbsp; `Real` ☐ |
| `RksvSubmission.Enabled` | `false` ☐ &nbsp; `true` ☐ |
| `RksvSubmission.AllowOutboundNetworkCalls` | `false` ☐ &nbsp; `true` ☐ |
| `FinanzOnlineEnabled` (CompanySettings) | `false` ☐ &nbsp; `true` ☐ |
| `FinanzOnlineAutoSubmit` | `false` ☐ &nbsp; `true` ☐ |
| Datum der Anmeldung der Kasse bei FinanzOnline | _______________ |
| Datum der Anmeldung der TSE bei FinanzOnline | _______________ |

### 8.3 Wichtiger Hinweis

> Der Softwarehersteller erklärt **nicht** automatisch die produktive Erreichbarkeit der FinanzOnline-Webservices oder die Akzeptanz seitens BMF. Die endgültige Anmeldung der Kasse, der TSE-Einheit und die operative Verifikation der Belege liegt in der Verantwortung des Kassenbetreibers (RKSV §7, RKSV §10).

---

## 9. Sonstige technische Eigenschaften

### 9.1 Zeitabgleich (NTP)

- Hintergrund-Synchronisation mit drei NTP-Servern (Default: `pool.ntp.org`, `at.pool.ntp.org`, `time.google.com`).
- **Online-Fiskalzahlungen werden abgewiesen**, wenn die letzte Synchronisation fehlgeschlagen ist, kein Offset bekannt ist oder `|offset| > MaxAllowedOffsetSeconds` (Default 5 s) gilt.
- Operatorseitige Warnschwelle: `CriticalOffsetSeconds` (Default 60 s). Statusabfrage: `GET /api/system/time/status`.

### 9.2 Offline-Verhalten

- Nicht-fiskale Zahlungen können in der POS-Offline-Warteschlange (lokal) zwischengespeichert und nach Wiederverbindung über `POST /api/offline-transactions/replay` wiedergegeben werden.
- **Gutschein-Zahlungen sind explizit von der Offline-Warteschlange ausgeschlossen** (Backend lehnt entsprechende Anfragen ab).
- TSE-Verfügbarkeit wird über Health-Check-Intervall und Failure-Schwellen überwacht (Abschnitt 4.1.2).

### 9.3 Datenaufbewahrung

- Mindestaufbewahrung **7 Jahre** (`AuditRetention.RetentionYears = 7`).
- Belegrelevante Tabellen: `payment_details`, `invoices`, `receipts`, `receipt_items`, `receipt_tax_lines`, `signature_chain_state`, `TseSignatures`, `audit_logs`, `offline_transactions`, `finanz_online_outbox_messages`, `rksv_special_receipt_finanz_online_submissions`.

### 9.4 Schutz sensibler Daten

- **Gutscheincodes** werden ausschließlich als Hash plus maskierte Anzeigeform gespeichert (`vouchers.code_hash`, `masked_code`). Klartextcodes werden weder geloggt noch in der Offline-Warteschlange gespeichert.
- **Kreditkartendaten / sonstige sensible Zahlungsdaten** werden in Logs und Audit-Trails maskiert.

### 9.5 Audit-Spuren

Alle fiskalisch relevanten Aktionen (erfolgreiche Zahlungen, abgelehnte Zahlungen, Sonderbeleg-Erzeugung, Decommissioning, Offline-Replay, FinanzOnline-Übermittlung) werden in `audit_logs` strukturiert protokolliert. Technische Logmeldungen sind in englischer Sprache; benutzerseitige Texte im POS sind in deutscher Sprache.

---

## 10. Anlagen / Verweise

| Dokument / Endpunkt | Zweck |
|---|---|
| `docs/RKSV_BMF_BELEGCHECK_WORKFLOW.md` | Manuelle BMF-Verifikation der Belege (operativer Workflow). |
| `docs/RKSV_CASH_REGISTER_OPERATIONS.md` | Operative Handhabung der Kasse. |
| `docs/RKSV_RECEIPT_INVOICE_REQUIREMENTS.md` | Detaillierte Belegfeld-Pflichten und Implementierungsstand. |
| `GET /api/admin/rksv/compliance-report` | Interner Diagnose-Bericht (5 Prüfungen: Sonderbelege, Signaturkette, Sequenzlücken, TSE-Signatur-Vorhandensein, QR-Format). |
| `POST /api/admin/rksv/evidence-bundle` | Internes RKSV-Beweismittel-Bündel (ZIP) für Prüfer / interne Compliance — kein Ersatz für den amtlichen DEP-Export. |
| `GET /api/admin/fiscal-export` | DEP-ähnlicher Fiscal-Export (intern / diagnostisch; nicht rechtsverbindlich). |

---

## 11. Erklärung des Kassenbetreibers

Hiermit erkläre ich als Verantwortliche/-r bzw. vertretungsbefugtes Organ des oben genannten Unternehmens:

1. dass die in diesem Dokument beschriebene Registrierkassen-Konfiguration die in der RKSV vorgesehenen technischen Sicherheitsmerkmale (Belegnummern-Sequenz, Signaturkette, TSE-Signatureinheit, RKSV-Sonderbelege, RKSV-konformer QR-Code) im Sinne der hier dargestellten Software-Implementierung verwendet;
2. dass die Kasse gemäß den in Abschnitt 1, 2, 4 und 8 angegebenen Daten konfiguriert und betrieben wird;
3. dass die unternehmensbezogenen Pflichten (Anmeldung der Kasse und der TSE bei FinanzOnline, regelmäßige manuelle BMF-Belegcheck-Verifikation, Aufbewahrung der Belegdaten gemäß Abschnitt 9.3) eingehalten werden;
4. dass die in dieser Eigenzertifizierung angegebenen unternehmensbezogenen Daten der Wahrheit entsprechen.

| | |
|---|---|
| Ort, Datum | _______________________________________ |
| Verantwortliche Person (Name in Druckbuchstaben) | _______________________________________ |
| Funktion | _______________________________________ |
| Unterschrift | _______________________________________ |
| Firmenstempel (sofern vorhanden) | _______________________________________ |

---

### Rechtlicher Hinweis (Wiederholung)

Diese Vorlage beschreibt den **technischen Implementierungsstand** der Software „Regkasse POS" (Repository-Stand zum Datum dieser Version). Sie ist:

- **keine** automatisierte Konformitätserklärung des Softwareherstellers gegenüber dem BMF,
- **kein** rechtsverbindlicher Nachweis im Sinne der RKSV oder einer abgabenrechtlichen Außenprüfung,
- **kein Ersatz** für eine steuerlich-rechtliche Beratung oder eine offizielle Prüfung durch das Finanzamt,
- **kein Ersatz** für den amtlichen DEP-Export (eigenes Verfahren).

Die juristische Verantwortung für Vollständigkeit und Richtigkeit der hier abgegebenen Eigenzertifizierung liegt ausschließlich beim Kassenbetreiber. Aktuelle gesetzliche Anforderungen sind dem Bundesgesetzblatt (insb. RKSV idgF) sowie den jeweils geltenden BMF-Erlässen zu entnehmen.
