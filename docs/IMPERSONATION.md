# Impersonation für Super Admin

## Was ist Impersonation?

Super Admin kann sich als ein Mandant (Tenant) ausgeben, um Support zu leisten. Die Verwaltungsoberfläche (Frontend Admin) läuft dann im Kontext des gewählten Mandanten — mit denselben Daten und Menüs wie ein Mandanten-Administrator, ohne das Passwort des Kunden zu kennen.

**Voraussetzung:** Rolle `SuperAdmin` und Zugang zum Plattform-Host (`admin.regkasse.at` bzw. Entwicklung: `localhost` / `admin.*.local`).

## Wie funktioniert es?

1. Super Admin öffnet **Mandantenverwaltung**: `/admin/tenants`
2. Klickt bei einem aktiven Mandanten auf **„Als Mandant anmelden“** (Tabellenmenü oder Mandantendetail)
3. Das System stellt ein mandantenspezifisches JWT aus (`tenant_id` + `tenant_impersonation=true`) und übergibt die Session:
   - **Zielarchitektur (Single Admin UI):** Session bleibt auf `https://admin.regkasse.at` mit Mandanten-JWT (siehe [`POS_PRODUCTION_ARCHITECTURE.md`](POS_PRODUCTION_ARCHITECTURE.md)).
   - **Legacy FA-Code:** kann noch nach `https://{slug}.regkasse.at/impersonate-callback#…` umleiten (Token im URL-Fragment) — technischer Debt; POS bleibt immer `https://pos.regkasse.at`.
   - **Entwicklung:** Token auf demselben Host, Mandant per `dev_tenant_id` / Reload
4. Nach erfolgreicher Übergabe landet man im Dashboard des Mandanten und kann operativ arbeiten (Stammdaten, Kassen, Benutzer usw., je nach Berechtigung des Super-Admin-Kontos)

Während der Session zeigt ein **blauer Banner** oben den aktiven Mandanten und den Button **„Impersonation beenden“**. In der Kopfzeile erscheint zusätzlich ein Mandanten-Badge (Support-Impersonation).

## Sicherheit

- **Audit-Log:** Alle relevanten Aktionen werden im Audit-Log festgehalten. Unter Impersonation werden zusätzlich gesetzt:
  - `impersonated_by` — ID des Super-Admin-Benutzers
  - `impersonated_tenant` — ID des betroffenen Mandanten  
  Beim Start der Session wird ein Eintrag `TENANT_IMPERSONATION_STARTED` geschrieben.
- **Zeitlich begrenztes Token:** Das Impersonation-JWT nutzt dieselbe Access-Token-Laufzeit wie der normale Admin-Login (Standard **15 Minuten**, konfigurierbar über `AuthOptions:AccessTokenLifetimeMinutes`). Es ist **kein** 24-Stunden-Token. Vor Ablauf erscheint ab **unter 5 Minuten** Restlaufzeit eine Warnung (*„Impersonation läuft in X Minuten ab.“*). Bei Ablauf: erneut von `/admin/tenants` impersonieren.
- **Nachvollziehbarkeit:** Der originale Super Admin bleibt über JWT (`sub` / `user_id`) und die Audit-Felder `impersonated_by` / `impersonated_tenant` zuordenbar.
- **Einschränkungen:** Gesperrte, inaktive oder gelöschte Mandanten können nicht impersoniert werden. Nur `SuperAdmin` darf `POST /api/admin/tenants/{tenantId}/impersonate` aufrufen.

## Impersonation beenden

Klicken Sie im oberen Banner auf **„Impersonation beenden“**. Die Impersonation-Session wird beendet; in der Produktion erfolgt die Weiterleitung zurück zu `https://admin.regkasse.at/admin/tenants`.

## Technische Details

Entwickler- und Integrationsdokumentation (API, Fragment-Handoff, Code-Pfade):

- [IMPERSONATION_FLOW.md](./IMPERSONATION_FLOW.md) — technischer Ablauf (EN)
- [MULTI_TENANT.md](./MULTI_TENANT.md) — Mandantenmodell und Isolation
