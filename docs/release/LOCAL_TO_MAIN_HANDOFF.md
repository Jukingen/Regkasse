# Local to main handoff

Branch: `feat/backup-extensions-tickets-retail-lodging`

This branch is 11 commits ahead of `579b17fa` (`test(pos): add Playwright coverage for profiles and folios`), which was the `main` tip when the working tree was split. Nothing has been pushed.

## Commits

`git log --oneline --since=2026-10-12` (the four commits at the bottom were already on `main`):

```text
4e0204e5 docs: update tickets, vertical profiles, tenant extensions, backup
497952d6 chore(backend): register new services and the cumulative EF model
b17e9972 chore(i18n): add strings for backup, outbox, tickets, retail, and lodging
cac12cc3 test: stabilize tenant wizard and admin tenants tests
2a070f97 feat(beherbergung): minibar auto-charge and room-move flows
cd5196d2 feat(retail): vertical profile seed, product variants, barcode scanning
20ab7eca feat(ticket-sales): v2 seat map, categories, hold ownership, companion pairing
5ed263f0 feat(extensions): vet prescription extension and payment.before-sign payload
523c6dee feat(extensions): add folio.opened hook and breakfast/late-checkout extensions
d56ff30f feat(finanzonline): retry or resolve an outbox row without changing SOAP
bb16d066 feat(backup): known-gap acceptance and weekly drill summary
579b17fa test(pos): add Playwright coverage for profiles and folios
93258f1a feat: add tenant extensions, doctor profile, and ticket events
76b311bf docs: add staging setup and align release runbooks
3ac6a61b feat(lodging): add folio detail, stay charges, and room checkout
```

New commits in this handoff: **11**.

## Test summary

Gates were run on this tree before the commits. The commits only split that tree; they did not change file contents.

| Gate | Result |
|------|--------|
| `dotnet test backend/KasseAPI_Final.sln` | Passed 5818, Failed 0, Skipped 61, Total 5879 |
| `frontend-admin` `npm run test` | 3 consecutive runs, each 606 files / 2619 tests, Failed 0 |
| `frontend-admin` `npm run typecheck` | Exit 0 |
| `frontend` `npm run test` | 155 suites, 828 tests, Failed 0 |
| `frontend` `npm run typecheck` | Exit 0 |
| `node scripts/verify-api-client.mjs` | OK. Generated client matches `backend/swagger.json` |
| `node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error` | Passed, 0 failures |
| `node scripts/git-hooks/scan-secrets.mjs --tracked` | OK |
| `frontend-admin` `npm run i18n:ci` | **Failed.** Dynamic unresolved templates: 99 > budget 90 |

The 61 skipped backend tests are the PostgreSQL integration tests. Docker was not running, so they were not executed. No other non-skipped suite was left unrun.

`i18n:ci` is the gate that is not green. `localization/i18n-ci-budgets.json` was not raised. Expansion rules were added for the two templates this work introduced (`backupDr.compliance.gate` colors and `admin.rooms.minibarStatusValue`). After that, the count is 99. The remaining templates are pre-existing dynamic `t()` calls. Missing locale pairs are 0.

## What landed

Backup operators can accept a known gap and get a weekly drill summary. FinanzOnline operators can retry a failed outbox row or close it without a new SOAP submit, with Mandanten-Admin access still limited to the ambient tenant. Lodging extensions can add included breakfast when a folio opens and one late-checkout fee when it closes, and the same options writer stores minibar settings. A veterinary sale can carry a prescription reference on the before-sign cart payload. Ticket sales can price seats by category, hold them for the seller, and pair a companion seat. Retail tenants can scan a barcode and sell a catalog variant, with stock adjustment on the shared payment path that also stamps the prescription flag; neither is stored on the RKSV receipt. An open folio can move to another available room and pick up minibar charges without a new folio, payment, or receipt. Tenant-list and create-wizard tests wait for the loaded row. Strings, host registration, the cumulative EF model, and the English guides are in the later commits.

## Known follow-ups

- Retail lot tracking
- Ticket season pass
- PITR/WAL UI
- `npm run i18n:ci` dynamic-template budget (99 unresolved, cap 90) until the pre-existing templates have expansion rules

## Push

```bash
git push origin feat/backup-extensions-tickets-retail-lodging
```
