# Production cutover

Ordered checklist for the first real production deployment. Piece-by-piece guides stay in the linked docs. This file is the sequence. It is not a legal RKSV or FinanzOnline compliance certificate.

**Last updated:** 2026-10-27

| Related | Link |
|---------|------|
| Host deploy | [`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) · [`../DEPLOYMENT.md`](../DEPLOYMENT.md) |
| Environments | [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) |
| Pipeline | [`CI_CD.md`](CI_CD.md) · [`GITHUB_ACTIONS.md`](GITHUB_ACTIONS.md) |
| RKSV / TSE | [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) |
| FinanzOnline | [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) |
| Monitoring | [`MONITORING.md`](MONITORING.md) · [`ALERTING.md`](ALERTING.md) |
| Backup | [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md) |
| Marketing | [`../frontend-marketing/README.md`](../frontend-marketing/README.md) |
| Pre-flight script | [`../scripts/pre-flight-cutover.mjs`](../scripts/pre-flight-cutover.mjs) |

Hosts: `https://regkasse.at`, `https://www.regkasse.at`, `https://api.regkasse.at`, `https://admin.regkasse.at`, `https://pos.regkasse.at`.

Do not write secrets into this file. Do not run a real sale on a real mandant during D-0.

## Pre-flight (D-7)

- [ ] `Sentry__Dsn` is set for the API. Health and metrics routes stay out of Sentry ([`MONITORING.md`](MONITORING.md)).
- [ ] `SLACK_WEBHOOK_URL` is set and a test alert arrives ([`ALERTING.md`](ALERTING.md)).
- [ ] A deployment license key is issued. Offline verify uses `License__OfflineVerificationPublicKeyPem` or `LicenseSettings__PublicKeyPath`.
- [ ] The trial / demo path is confirmed. The first real mandant will be created with `seedDemo=false` (wizard default).
- [ ] Backup target exists (`PgDump`, staging and archive dirs). Execution mode is not `Fake` ([`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)).
- [ ] RKSV/TSE key material for a real tenant is in the secret store, not in git ([`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md)).
- [ ] FinanzOnline credentials are in the secret store ([`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md)).
- [ ] NTP is configured. Online fiscal payments stay blocked when sync fails or the offset is over `MaxAllowedOffsetSeconds`.
- [ ] Marketing URLs are decided: site `https://regkasse.at`, API `https://api.regkasse.at` (`NEXT_PUBLIC_SITE_URL`, `NEXT_PUBLIC_API_BASE_URL` at build time).
- [ ] Legal pages render: `/impressum`, `/datenschutz`, `/agb` (German copy on the marketing site).

Manual dry run of the checker (expect FAIL until the host env is exported):

```bash
node scripts/pre-flight-cutover.mjs --help
node scripts/pre-flight-cutover.mjs
```

On the target host, after secrets are in the shell:

```bash
node scripts/pre-flight-cutover.mjs \
  --api-base https://api.regkasse.at \
  --admin-base https://admin.regkasse.at \
  --pos-base https://pos.regkasse.at
```

There is no CI job for this script. `npm run validate:scripts` only checks that the filename stays documented.

## DNS and TLS (D-5)

- [ ] `regkasse.at` resolves to the marketing host.
- [ ] `www.regkasse.at` resolves (alias or redirect to the apex).
- [ ] `api.regkasse.at` resolves to the API load balancer or VM.
- [ ] `admin.regkasse.at` resolves to the FA host.
- [ ] `pos.regkasse.at` resolves if POS web is hosted; otherwise the DNS decision is written down.
- [ ] TLS certificates for those names are valid and not inside the renewal window used by the edge.

## Database (D-3)

- [ ] Production PostgreSQL is a new instance. It is not a restore of Staging.
- [ ] Schema is migrated to the latest revision through the approved migrate job (`backend-production-migrations`). No ad-hoc `Down`.
- [ ] `dotnet ef migrations list` on that connection reports `pendingCount=0` (the pre-flight script).
- [ ] No Staging rows were copied. The first real tenant is created later with `seedDemo=false`.

## Fiscal keys (D-2)

- [ ] TSE mode is the real device path (`TseMode=Device` / vendor Real). Demo, Off, and Fake are rejected ([`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md)).
- [ ] FinanzOnline `UseSimulation=false` on the production host. `RksvSubmission` client kind is Real.
- [ ] No simulation flag remains in the Production environment file.
- [ ] Staging, if it shares the lock, keeps `Tse:EnforceProductionLockInStaging=true` (Staging example default). That flag does not turn Production simulation on.

## Deploy (D-1)

- [ ] API image is promoted with `deploy.yml` / `deploy-production.yml` into GitHub Environment `backend-production` (reviewers required). Auto-rollback stays off.
- [ ] Migrations were approved on `backend-production-migrations` before the app deploy.
- [ ] `frontend-admin` is deployed with build-time `NEXT_PUBLIC_API_BASE_URL=https://api.regkasse.at` and `NEXT_PUBLIC_RKSV_ENVIRONMENT=PROD`.
- [ ] `frontend-marketing` is deployed to `regkasse.at` (Vercel or nginx) with `NEXT_PUBLIC_SITE_URL` and `NEXT_PUBLIC_API_BASE_URL` set before the build.
- [ ] `frontend-sites` is deployed for tenant storefronts.
- [ ] POS build, if hosted, used `EXPO_PUBLIC_API_BASE_URL=https://api.regkasse.at/api`.
- [ ] Pre-flight script exits 0 against the new hosts.

## Cutover (D-0)

- [ ] Super Admin first login completes TOTP 2FA. Development bypass codes do not work.
- [ ] One demo tenant is created with the demo button (`seedDemo=true`, Super Admin only).
- [ ] One test sale is completed per vertical on that demo tenant only. No test sale on a real mandant.
- [ ] One system backup succeeds (`PgDump`) and one restore drill is recorded ([`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)).
- [ ] Monitoring health check: `/api/health/live` and `/api/health/ready` are 200, and `/admin/monitoring` shows the API, Sentry, and Slack path.

## Post-cutover (D+1)

- [ ] 24 hours of monitoring with no unacked critical alert (Sentry, uptime, Slack).
- [ ] First real tenant is onboarded in the wizard with `seedDemo=false`.
- [ ] First license is activated for that tenant (`POST /api/license/activate`).
- [ ] First FinanzOnline submission follows the written policy (TEST webservice user first, then PROD). Do not skip the TEST gate in [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md).

## Rollback

App binaries can move back. Fiscal history cannot.

| Surface | How to roll back | Safe to reverse |
|---------|------------------|-----------------|
| Backend | Previous image via the rollback webhook or `scripts/rollback-production.sh` with `REGKASSE_ROLLBACK_CONFIRM=YES` ([`DEPLOYMENT.md`](../DEPLOYMENT.md)) | The published API build |
| Admin | Redeploy the previous FA image. `NEXT_PUBLIC_*` is already baked into that image | The FA build |
| Marketing | Vercel rollback, or the previous nginx static build | The marketing build |
| Database schema | Not automatic. `Down` is a separate high-risk change | Do not roll back applied migrations as part of an app rollback |
| Fiscal rows | Do not delete or rewrite | Payments, receipts, TSE signatures, and FinanzOnline outbox rows stay |

Production smoke failure does not auto-roll the app (unlike Staging). Schema that already migrated stays. Signed receipts and the TSE chain are not rewritten to an earlier time.
