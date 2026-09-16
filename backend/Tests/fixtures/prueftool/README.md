# BMF Prüftool DEP export fixtures

Deterministic test files for `scripts/rksv/verify-rksv-dep-export.ps1`.

| File | Purpose |
|------|---------|
| `dep-export.json` | BMF `Belege-Gruppe` export (3 chained RKSV §9 receipts) |
| `crypto-material.json` | BMF `cryptographicMaterialContainer` (AES key + signing cert) |
| `qr-code-rep.json` | BMF QR wire strings for `CheckSingleReceipt` (§9 machine code + JWS) |

**Not production secrets** — fixed dev-only key material for CI/local Prüftool runs.

A plain `dotnet test` run **does not rewrite these files**. The generator writes into a throwaway directory unless regeneration is requested. ES256 draws a new nonce per signature, so a fresh run cannot be byte-compared against this folder.

## Regenerate

Set `REGKASSE_UPDATE_BASELINE=1` (same switch as the country baseline suite) and run the regenerator. The dedicated script sets that switch for you:

```powershell
.\scripts\rksv\generate-dep-export-fixtures.ps1
```

Or:

```powershell
$env:REGKASSE_UPDATE_BASELINE = "1"
cd backend
dotnet test --filter "RksvDepPrueftoolFixtureTests"
```

Commit the three JSON files together when the rotation is intentional. CI reads the committed copies; it does not regenerate them.

## Verify (JDK 17+ required)

DEP export:

```powershell
.\scripts\rksv\verify-rksv-dep-export.ps1 -UseFixtures
```

Receipt QR (CheckSingleReceipt):

```powershell
.\scripts\rksv\verify-rksv-receipt-qr.ps1 -UseFixtures
```

Manual paths:

```powershell
.\scripts\rksv\verify-rksv-dep-export.ps1 `
  -DepExportPath "backend/Tests/fixtures/prueftool/dep-export.json" `
  -CryptoMaterialPath "backend/Tests/fixtures/prueftool/crypto-material.json"
```
