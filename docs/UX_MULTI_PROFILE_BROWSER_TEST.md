# Multi-profile POS browser test

Manual browser pass for the shared POS UI (`frontend/`, Expo web). One app serves every vertical profile. After login, `GET /api/pos/vertical-profile` follows `company_settings.vertical_profile_id` and shows or hides tabs, panels, and onboarding slides. Profiles do not change the RKSV signature pipeline.

Related: [`VERTICAL_PROFILES.md`](VERTICAL_PROFILES.md), [`POS_PRODUCTION_ARCHITECTURE.md`](POS_PRODUCTION_ARCHITECTURE.md).

**Last updated:** 2026-10-11

## Scope

| Item | Count |
|------|------:|
| Profiles | 10 |
| Test cases (T1–T8) | 8 |
| Matrix cells | **80** (10 × 8) |

Run T1–T4 and T8 chrome on every profile. Run fiscal cases T5–T7 on the `dev-*` registers. Leave the shared `dev` register (`KASSE-001` on slug `dev`) out of payment and Tagesabschluss unless you accept closing that demo register for the Vienna day.

## Prerequisites

Start from the repository root. Development only.

```bash
npm run dev:backend
npm run dev:pos
npm run dev:admin
```

`npm run dev:all` starts the same three plus Sites. Sites is not part of this pass.

| Surface | URL |
|---------|-----|
| API | `http://localhost:5184` |
| POS web | `http://localhost:8081` |
| FA | `http://admin.regkasse.local:3000` |

Confirm `GET http://localhost:5184/api/health` returns success before opening POS.

### Tenant header

In Development the POS sends `X-Tenant-Id` from the dev tenant override (`dev_tenant_id` in browser storage) or from `EXPO_PUBLIC_DEV_TENANT_ID` (default `dev`). Log out, then set the slug to the row you are about to test. A header of `dev` plus a cashier from `dev-vet` fails tenant isolation (HTTP 404 on POS calls).

Set the slug in either way:

- Open `http://localhost:8081/tenant/{slug}` while signed out (T3), or
- After a previous session, use the header control **DevTenantSwitcher**, then log out and back in.

### Fixtures

Country is `AT`. Each new tenant has one default register `KASSE-001`, three demo products (`Demo Produkt 1`–`3`), and no license key. Cashier password change on next login is off.

| Tenant slug | Profile | Cashier | Password | Register |
|-------------|---------|---------|----------|----------|
| `dev` | `gastronomy` | `cashier-gastro` | `DevCashier1!` | `KASSE-001` |
| `dev-gastronomy-tables` | `gastronomy-tables` | `cashier-tables` | `DevCashier1!` | `KASSE-001` |
| `dev-vet` | `vet` | `cashier-vet` | `DevCashier1!` | `KASSE-001` |
| `dev-doctor` | `doctor` | `cashier-doctor` | `DevCashier1!` | `KASSE-001` |
| `dev-hair-salon` | `hair-salon` | `cashier-hair` | `DevCashier1!` | `KASSE-001` |
| `dev-mobile-services` | `mobile-services` | `cashier-mobile` | `DevCashier1!` | `KASSE-001` |
| `dev-handy-shop` | `handy-shop` | `cashier-handy` | `DevCashier1!` | `KASSE-001` |
| `dev-taxi` | `taxi` | `cashier-taxi` | `DevCashier1!` | `KASSE-001` |
| `dev-ticket-sales` | `ticket-sales` | `cashier-ticket` | `DevCashier1!` | `KASSE-001` |
| `dev-beherbergung` | `beherbergung` | `cashier-lodge` | `DevCashier1!` | `KASSE-001` |

`dev-taxi` company settings store a kilometre tariff of 2.50 EUR.

Each `dev-*` tenant also has a Mandanten-Admin `admin@{slug}.regkasse.at` with initial password `DevAdmin1!`. That account must change its password on first login. Use it only when the cashier hits **Startbeleg erforderlich**. Cashier sessions do not create a Startbeleg.

### Profile chrome

Footer tabs use German labels. `href: null` removes a tab from the footer. The cart control stays on every profile.

| Profile | Layout | First screen after onboarding | Footer shows | Footer hides |
|---------|--------|-------------------------------|--------------|--------------|
| `gastronomy` | `standard` | Kassa | Kassa, Küche | Bestellungen, Zimmer, Patientenakte, Termine |
| `gastronomy-tables` | `tables` | Bestellungen | Kassa, Bestellungen, Küche | Zimmer, Patientenakte, Termine |
| `vet` | `standard` | Kassa | Kassa, Tierakte | Bestellungen, Küche, Zimmer, Termine |
| `doctor` | `appointment` | Termine | Kassa, Patientenakte, Termine | Bestellungen, Küche, Zimmer |
| `hair-salon` | `appointment` | Termine | Kassa, Termine | Bestellungen, Küche, Zimmer, Patientenakte |
| `mobile-services` | `appointment` | Termine | Kassa, Bestellungen, Termine | Küche, Zimmer, Patientenakte |
| `handy-shop` | `standard` | Kassa | Kassa | Bestellungen, Küche, Zimmer, Patientenakte, Termine |
| `taxi` | `taxi` | Kassa | Kassa, Bestellungen | Küche, Zimmer, Patientenakte, Termine |
| `ticket-sales` | `ticket` | Kassa | Kassa | Bestellungen, Küche, Zimmer, Patientenakte, Termine |
| `beherbergung` | `rooms` | Zimmer | Kassa, Zimmer, Küche | Bestellungen, Patientenakte, Termine |

The cart control (Warenkorb total) is on every row. **Ticket prüfen** is a user-menu item, not a footer tab, and only when `ticketScan` is true (`ticket-sales`). **Küche** in the user menu follows `kitchenDisplay` (`gastronomy`, `gastronomy-tables`, `beherbergung`).

Bestellungen is visible only when the session can view orders and the profile has `tables` or `routeTracking` (`gastronomy-tables`, `mobile-services`, `taxi`).

## How to read a result

| Check | Where |
|-------|--------|
| Profile payload | Browser network: `GET /api/pos/vertical-profile`. Fields: `profileId`, `posLayout`, `posFeatures`. |
| Public slug (signed out) | `GET /api/public/tenants/{slug}` → `verticalProfileId`. |
| Login industry line | Login subtitle `Regkasse — {industry}` from `auth.brandIndustry`. |
| Hidden control | The German label is absent in the footer or user menu. Opening the route directly shows **Diese Funktion ist für das aktuelle POS-Profil nicht verfügbar.** |
| Payment | `POST /api/pos/payment` → `Zahlung erfolgreich`. |
| Tagesabschluss | `POST /api/pos/shift/daily-closing`. |
| RKSV signature | Response `tseSignature` is a compact JWS: exactly three segments separated by `.`. The receipt view shows the QR (`ReceiptSignatureQr`) and the same signature text. |

POS UI copy in the steps below is German and must match the screen. Do not treat an English label as a pass.

## T1 — Login shows the profile UI

**Goal:** The cashier for a slug lands on that profile’s first screen, and the footer matches the chrome table.

### Steps

1. Sign out.
2. Set the dev tenant slug to the row under test (deep link or switcher).
3. Open `http://localhost:8081`.
4. If the onboarding deck appears, press **Loslegen** or **Überspringen** (see T4). This device flag is shared across profiles.
5. On **ANMELDEN**, enter the cashier username and `DevCashier1!`. Press **Anmelden**.
6. If **Kasse wählen** appears, choose `KASSE-001`.

### Expected result

- Login subtitle is `Regkasse —` plus the industry name for that profile (for example `Regkasse — Tierarztpraxis` on `dev-vet`).
- After onboarding, the first screen matches the chrome table.
- Footer labels match **Footer shows**. The cart control is present.
- User menu shows the cashier name and register `KASSE-001`.

### Verify

- Network: `GET /api/pos/vertical-profile` returns `profileId` equal to the fixture profile.
- UI: footer labels from the chrome table. First route is `/(tabs)/cash-register`, `/(tabs)/orders`, `/(tabs)/appointments`, or `/(tabs)/rooms` as listed.

## T2 — Other profiles’ menus stay hidden

**Goal:** A session shows only its own profile chrome. There is no separate “wrong password” case. Log in as the correct cashier and confirm foreign menus are absent.

### Steps

1. Complete T1 for the profile.
2. Read the footer left to right.
3. Open the user menu (avatar).
4. For one hidden feature, paste the screen URL while the session is still that profile:
   - Küche: `http://localhost:8081/kitchen-display`
   - Patientenakte: `http://localhost:8081/patient-record`
   - Termine: `http://localhost:8081/appointments`
   - Zimmer: `http://localhost:8081/rooms`

### Expected result

- Footer matches **Footer hides** for that row.
- User menu contains **Küche** only for `gastronomy`, `gastronomy-tables`, and `beherbergung`.
- User menu contains **Ticket prüfen** only for `ticket-sales`.
- A direct URL for a feature this profile does not have shows **Diese Funktion ist für das aktuelle POS-Profil nicht verfügbar.**

### Verify

- `posFeatures` on `GET /api/pos/vertical-profile` has `false` for the hidden capability (`tables`, `kitchenDisplay`, `patientRecord`, `appointment`, `ticketScan`, `routeTracking`, `roomTracking`).
- The unavailable copy is the `verticalProfiles.screens.unavailable` string.

## T3 — Deep link `/tenant/{slug}`

**Goal:** A signed-out visit to `/tenant/{slug}` stores that mandant and opens login for it.

Routes: web `http://localhost:8081/tenant/{slug}`. Native schemes `cashregister://tenant/{slug}` and `regkasse://tenant/{slug}` hit the same screen. This pass uses the web path.

### Steps

1. Sign out. Clear the session so `isAuthenticated` is false.
2. Open `http://localhost:8081/tenant/dev-vet` (repeat with each fixture slug).
3. Wait for the loader to finish.
4. Read the login screen. Do not sign in yet.
5. Sign in with that slug’s cashier.
6. Repeat once while already signed in as a different cashier: open `/tenant/dev-taxi` without logging out.

### Expected result

- Signed out: loader, then **ANMELDEN** with **Mandant: {slug}** and the industry subtitle for that slug’s profile.
- Signed out: the next POS calls use that slug.
- Already signed in: the bridge does not switch mandant. It opens the current session’s Kassa. Switching requires logout, then the deep link, then the matching cashier.

### Verify

- Signed out: `GET /api/public/tenants/{slug}` returns `verticalProfileId` for that fixture.
- Browser storage: dev tenant override equals the slug (`dev_tenant_id`).
- After login: `GET /api/pos/vertical-profile` matches the deep-linked slug, not the previous session.
- Signed-in case: network does not call `bootstrap` for a new slug; the visible register stays the current session.

## T4 — Onboarding slides

**Goal:** The first authenticated visit on a device shows that profile’s deck. The flag `pos_onboarding_seen_v1` is stored once per browser (web: `localStorage`), not once per tenant.

### Steps

1. Sign out.
2. In the browser devtools, delete `pos_onboarding_seen_v1` from `localStorage`.
3. Set the tenant slug, then sign in as that profile’s cashier.
4. Read the step counter `{current} / {total}`, the title, and the preview label.
5. Press **Weiter** until the last slide, then **Loslegen**. On a multi-slide deck, **Überspringen** on an earlier slide is a second pass: it must finish the deck and set the flag.
6. Sign out, delete the flag, and repeat for the next profile.

### Expected result

| Profile | Slides (titles in order) |
|---------|--------------------------|
| `gastronomy` | Ihre Gastronomie → Küchenmonitor |
| `gastronomy-tables` | Ihre Gastronomie → Tische |
| `vet` | Willkommen in Ihrer Tierarztpraxis → Patientenakte → Verkauf mit Patientenbezug |
| `hair-salon` | Ihr Friseursalon → Terminkalender → Dienstleistungen |
| `taxi` | Ihr Taxi-Betrieb → Fahrt → Tarif |
| `handy-shop` | Ihr Handyshop → IMEI |
| `mobile-services` | Ihr mobiler Dienst → Einsatzort |
| `ticket-sales` | Ihr Ticketverkauf → Ticketprüfung |
| `beherbergung` | Ihre Beherbergung → Zimmer |

- Step text matches `1 / {total}` and advances.
- **Loslegen** or **Überspringen** lands on the T1 first screen.
- A second login on the same browser, without deleting the flag, skips the deck.

### Verify

- UI titles match the table (German).
- After finish, `localStorage` key `pos_onboarding_seen_v1` is `1`.
- Network still shows the same `GET /api/pos/vertical-profile` as T1. Slides are chosen from `profileId` in the client (`buildOnboardingSlides`).

## T5 — Payment

**Goal:** A cash sale completes on every profile. The pay button and the fiscal receipt path are the same. Profile panels may add fields; they do not replace **Barzahlung**.

Run this on `dev-*` registers. Skip slug `dev` unless you accept a real demo-register receipt.

### Steps

1. Complete T1 for a `dev-*` cashier. Confirm the user menu shows register `KASSE-001`.
2. Open **Einstellungen**. Under **Schicht**, the status is **Schicht: ✅ Aktiv**. If it shows **Schicht: ⏳ Geschlossen**, press **Schicht starten** and confirm **Schicht gestartet**.
3. If a banner or modal says **Startbeleg erforderlich**, stop the cashier session. Sign in as `admin@{slug}.regkasse.at` (initial password `DevAdmin1!`, then the forced password change). Create the Startbeleg from **Fiskaly / Sonderbelege**. Sign out, sign back in as the cashier, and continue.
4. On **Kassa**, add **Demo Produkt 1**.
5. Press the cart control (accessibility label starts with **Warenkorb**).
6. In the payment dialog, choose **Barzahlung**. Leave the received amount empty (exact amount).
7. Press the pay control (**Zahlen** plus the amount).

### Expected result

- Success title **Zahlung erfolgreich**.
- Actions **Beleg-PDF** and **Fertig** are available.
- The cart total returns to zero after **Fertig**.
- The same steps succeed on every `dev-*` profile. Profile-only fields (Rezept, route, IMEI) stay optional for this generic product.

### Verify

- Network: `POST /api/pos/payment` returns success and a payment id.
- `GET /api/pos/receipts/recent` lists a new receipt for this register.
- UI: **Zahlung erfolgreich**. A blocked **Startbeleg erforderlich** is a setup gap, not a profile failure, once the Startbeleg exists.

## T6 — Tagesabschluss

**Goal:** Daily closing works on each new register. Run it after T5, once per register, on the Vienna calendar day of that sale.

Do not run this twice on the same register the same day. A second close is expected to stay disabled. Do not run it on slug `dev`.

### Steps

1. Finish T5 on that register so the shift has a sale.
2. Open the user menu → **Einstellungen**.
3. In **Schicht**, press **Tagesabschluss**.
4. In the dialog titled **Tagesabschluss**, enter the cash count. Notes are optional.
5. Press **Abschluss durchführen**.

### Expected result

- Success title **Tagesabschluss abgeschlossen**.
- The button does not offer a second close for that register on the same Vienna day.
- Closing does not change `vertical_profile_id`.

### Verify

- Network: `GET /api/pos/shift/daily-closing/status` had `canClose: true` before the post, and `POST /api/pos/shift/daily-closing` succeeds.
- UI: **Tagesabschluss abgeschlossen**.
- After success, `GET /api/pos/vertical-profile` is unchanged.

## T7 — RKSV receipt signature

**Goal:** Every profile’s sale receipt uses the same signature shape. Profile data is not a second signature and is not written into the RKSV chain.

Use the receipt from T5. Do not expect identical signature bytes across profiles or across two sales on one register. The chain advances. The shape stays a compact JWS.

### Steps

1. From **Zahlung erfolgreich**, open **Beleg-PDF**, or open the user menu → **Belegliste** and open the new Beleg.
2. Find the QR and the signature text on the receipt.
3. In the network log, open the `POST /api/pos/payment` response (or the receipt detail) and copy `tseSignature`.
4. Repeat the shape check on one receipt per `dev-*` profile.

### Expected result

- The receipt shows a QR and a signature string.
- `tseSignature` has exactly three `.`-separated segments (compact JWS).
- Vet **Rezept**, taxi route fields, and IMEI values do not replace that signature. The vet payment hint states that a prescription is stored on the sale and stays out of the RKSV signature chain.
- Ticket QR copy, when present, is labeled as a ticket code, not the RKSV receipt QR.

### Verify

- UI: QR plus signature text on the Beleg.
- Response field `tseSignature` matches `/^[^.]+\.[^.]+\.[^.]+$/`.
- `GET /api/pos/vertical-profile` before and after the sale returns the same `profileId`.

## T8 — Profile-specific features

**Goal:** Each profile’s own surface is reachable and the foreign surfaces from T2 stay hidden. Catalog rows for IMEI, tickets, duration services, and lodging rooms are not in the three demo products. Chrome checks pass without that data. Saving a real IMEI, ticket, appointment service, or room is a manual extra and needs FA catalog data first.

### Steps and expected result

| Profile | Steps | Expected | Verify |
|---------|-------|----------|--------|
| `gastronomy` | On Kassa, add **Demo Produkt 1**. Press **An die Küche senden**. Open footer **Küche**. | Toast **Bestellung an die Küche gesendet.** Küchenanzeige lists the order or shows **Keine offenen Bestellungen.** after it is cleared. | `POST` kitchen order succeeds. Footer **Küche** visible. `posFeatures.kitchenDisplay` is true. |
| `gastronomy-tables` | Land on **Bestellungen**. On Kassa, choose a table number (1–10), add a product, send to kitchen. | Table selection sticks. **Küche** and **Bestellungen** stay in the footer. | `posLayout` is `tables`. `posFeatures.tables` and `kitchenDisplay` are true. |
| `vet` | Open **Patientenakte**. Leave the form empty and press **Patientenakte speichern**. Then fill Kundenname and Tiername and save. | Empty save shows **Kundenname und Tiername sind erforderlich.** A filled save shows **Kunde und Tierdaten wurden gespeichert.** | `posFeatures.patientRecord` is true. Save calls the patient/customer API for this tenant only. |
| `hair-salon` | First screen is **Terminkalender**. Open **Neuer Termin**. | Screen title **Terminkalender**. Saving without Kunde, Datum, Uhrzeit, and Dienstleistung shows **Kunde, Datum, Uhrzeit und Dienstleistung sind erforderlich.** | `posLayout` is `appointment`. `posFeatures.appointment` is true. A real booking needs a service product with duration (not in the demo catalog). |
| `mobile-services` | First screen is **Terminkalender**. On Kassa, find **Adresse und Einsatzort**. | Both **Termine** and **Bestellungen** are in the footer. Address panel title is **Adresse und Einsatzort**. | `posFeatures.appointment` and `routeTracking` are true. |
| `handy-shop` | Stay on Kassa. Add **Demo Produkt 1** (not IMEI-tracked). | No IMEI dialog for the demo product. Footer is Kassa plus the cart control. | `posFeatures.imeiTracking` is true. An IMEI dialog (**IMEI wählen**) appears only for a product flagged for IMEI. That product is not in the seed. |
| `taxi` | On Kassa, read **Taxi-Fahrt**. Enter Abholort, Zielort, and Kilometer. | Panel shows **Tarif 2,50 / km** (or the formatted 2.50 EUR tariff). **Fahrt starten** is available. | `posLayout` is `taxi`. `posFeatures.routeTracking` is true. Tariff comes from company settings, not from the receipt signature. |
| `ticket-sales` | On Kassa, read **Veranstaltung**. Open the user menu → **Ticket prüfen**. | The sale screen lists events and a seat grid when events exist. **Ticket prüfen** title is **Ticket prüfen**. Helper text says the ticket QR is not an RKSV receipt code. **Prüfen** with an empty code does not create a fiscal receipt. | `posFeatures.ticketScan` is true. Seat **Bezahlen** calls `POST /api/pos/ticket-events/{id}/seats/sell` and does not call `POST /api/pos/payment`. Camera **QR scannen** needs a device camera. |
| `beherbergung` | First screen is **Zimmer**. | Title **Zimmer** or **Zimmer wählen**. With no rooms, the empty copy is **Keine Zimmer vorhanden.** **Küche** remains in the footer. | `posLayout` is `rooms`. `posFeatures.roomTracking` and `kitchenDisplay` are true. `GET /api/pos/rooms` returns an empty list until rooms exist in FA. |

## What stays manual

These checks need a person in the browser. This document does not add an automated runner.

| Case | Why it stays manual |
|------|---------------------|
| T4 slide reading | Titles must be read on screen, and the seen-flag has to be deleted between profiles. |
| T5 Startbeleg, when the banner appears | One-time fiscal receipt. The cashier cannot create it. The Mandanten-Admin password change is a human step. |
| T6 Tagesabschluss | Once per register per Vienna day. A retry is a different assertion (button stays disabled). |
| T7 signature shape | A person compares the three-segment JWS and the QR on the Beleg. Byte-identical signatures across profiles are not the pass condition. |
| T8 camera **QR scannen** | Needs a camera and a real ticket QR. |
| T8 IMEI, duration service, lodging room, issued ticket | The seed has generic demo products and no rooms. Creating those rows is an FA catalog step, then a second browser pass. |
| T5–T7 on slug `dev` | That register is a shared demo register with an existing license. Closing or selling on it is a deliberate choice, not part of the default matrix. |

T1, T2, and the signed-out half of T3 are still browser checks. They can be scripted later. They are not scripted in this repository by this plan.

## Checklist

Copy the date and tester at the top of the pass. Mark a cell only after the expected result and the verify step both passed.

**Date:** _______________ **Tester:** _______________ **POS URL:** `http://localhost:8081`

### Setup

- [ ] API health on port 5184
- [ ] POS web on port 8081
- [ ] FA on `admin.regkasse.local:3000`
- [ ] Signed out before the first deep link
- [ ] Fiscal cases limited to `dev-*` registers

### T1 Login UI

- [ ] `dev` / gastronomy
- [ ] `dev-gastronomy-tables`
- [ ] `dev-vet`
- [ ] `dev-hair-salon`
- [ ] `dev-mobile-services`
- [ ] `dev-handy-shop`
- [ ] `dev-taxi`
- [ ] `dev-ticket-sales`
- [ ] `dev-beherbergung`

### T2 Hidden menus

- [ ] `dev` / gastronomy
- [ ] `dev-gastronomy-tables`
- [ ] `dev-vet`
- [ ] `dev-hair-salon`
- [ ] `dev-mobile-services`
- [ ] `dev-handy-shop`
- [ ] `dev-taxi`
- [ ] `dev-ticket-sales`
- [ ] `dev-beherbergung`

### T3 Deep link `/tenant/{slug}`

- [ ] `dev`
- [ ] `dev-gastronomy-tables`
- [ ] `dev-vet`
- [ ] `dev-hair-salon`
- [ ] `dev-mobile-services`
- [ ] `dev-handy-shop`
- [ ] `dev-taxi`
- [ ] `dev-ticket-sales`
- [ ] `dev-beherbergung`
- [ ] Signed-in visit does not switch mandant

### T4 Onboarding

- [ ] `dev` / gastronomy — Ihre Gastronomie, Küchenmonitor
- [ ] `dev-gastronomy-tables` — Ihre Gastronomie, Tische
- [ ] `dev-vet` — Tierarztpraxis, Patientenakte, Verkauf mit Patientenbezug
- [ ] `dev-hair-salon` — Friseursalon, Terminkalender, Dienstleistungen
- [ ] `dev-mobile-services` — Mobiler Dienst, Einsatzort
- [ ] `dev-handy-shop` — Handyshop, IMEI
- [ ] `dev-taxi` — Taxi-Betrieb, Fahrt, Tarif
- [ ] `dev-ticket-sales` — Ticketverkauf, Ticketprüfung
- [ ] `dev-beherbergung` — Beherbergung, Zimmer

### T5 Payment (`dev-*` only)

- [ ] `dev-gastronomy-tables`
- [ ] `dev-vet`
- [ ] `dev-hair-salon`
- [ ] `dev-mobile-services`
- [ ] `dev-handy-shop`
- [ ] `dev-taxi`
- [ ] `dev-ticket-sales`
- [ ] `dev-beherbergung`

### T6 Tagesabschluss (`dev-*` only, once each)

- [ ] `dev-gastronomy-tables`
- [ ] `dev-vet`
- [ ] `dev-hair-salon`
- [ ] `dev-mobile-services`
- [ ] `dev-handy-shop`
- [ ] `dev-taxi`
- [ ] `dev-ticket-sales`
- [ ] `dev-beherbergung`

### T7 RKSV signature shape (`dev-*` only)

- [ ] `dev-gastronomy-tables`
- [ ] `dev-vet`
- [ ] `dev-hair-salon`
- [ ] `dev-mobile-services`
- [ ] `dev-handy-shop`
- [ ] `dev-taxi`
- [ ] `dev-ticket-sales`
- [ ] `dev-beherbergung`

### T8 Profile feature

- [ ] `dev` — Küche send
- [ ] `dev-gastronomy-tables` — table plus Küche
- [ ] `dev-vet` — Patientenakte validation and save
- [ ] `dev-hair-salon` — Terminkalender
- [ ] `dev-mobile-services` — Termine plus Einsatzort
- [ ] `dev-handy-shop` — no IMEI dialog on demo product; `imeiTracking` true
- [ ] `dev-taxi` — Taxi-Fahrt and Tarif 2,50 / km
- [ ] `dev-ticket-sales` — Ticket prüfen, no fiscal receipt
- [ ] `dev-beherbergung` — Zimmer empty state
