# POS Playwright and TSE

**Last updated:** 2026-11-14

POS Playwright talks to a Development API that is already running. The Playwright config starts only the Expo web app. It does not start the API and it does not change TSE settings.

## Which TSE to use

| Run | TSE | How |
|-----|-----|-----|
| E2E tests | Fake TSE (Soft TSE in Development) | `frontend/e2e/run-e2e.sh` or `frontend/e2e/run-e2e.ps1` |
| Manual testing | Fiskaly TEST | `npm run dev:backend` with the normal Development config |

For E2E tests, use Fake TSE. For manual testing, use Fiskaly.

Fake TSE produces the `_R1-AT1_` machine code the receipt QR test asserts. Fiskaly TEST stays the default for a normal API process. The scripts set environment variables on the API process they start. They do not edit `appsettings` files, so a later manual `npm run dev:backend` still uses Fiskaly.

## Environment variables

The scripts export these variables before `npm run dev:backend`:

| Variable | E2E value | Why |
|----------|-----------|-----|
| `Tse__TseMode` | `Demo` | Selects the Development soft path when no device is attached |
| `Tse__Mode` | `Fake` | Registers `FakeTseProvider` at startup |
| `Tse__Provider` | `fake` | Names the fake provider |
| `Tse__FallbackEnabled` | `true` | Required by `TseSoftFallbackPolicy` in Development |
| `Tse__SoftTseEnabled` | `true` | Required by the same policy |

`Tse__TseMode`, `Tse__Mode`, and `Tse__Provider` alone do not sign a receipt while Fiskaly credentials exist and the cash register is not initialized. `TseSoftFallbackPolicy` also requires `FallbackEnabled` and `SoftTseEnabled`, and only in `ASPNETCORE_ENVIRONMENT=Development`. The scripts set `ASPNETCORE_ENVIRONMENT` as well.

Do not set these variables on the Expo process. Playwright's web server environment does not configure the API.

## Commands

Stop any API already listening on port 5184. Reusing that process would keep Fiskaly for the run.

```bash
bash frontend/e2e/run-e2e.sh
```

Windows PowerShell:

```powershell
./frontend/e2e/run-e2e.ps1
```

Both scripts start the API, wait for `/api/health`, run `npx playwright test critical-pos-flows` from `frontend/`, and stop the API process they started. Extra arguments are passed to Playwright.

To run Playwright against an API you already started with the variables above:

```bash
cd frontend && npx playwright test critical-pos-flows
```

## What stays unchanged

Production and Staging reject Soft TSE flags. The scripts are for local Development E2E only. Manual Development sessions keep the Fiskaly settings in the Development configuration.
