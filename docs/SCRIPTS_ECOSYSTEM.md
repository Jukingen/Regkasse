# Scripts ecosystem map

> **Last updated:** 2026-07-29  
> Visual guide: which Windows `.bat` to use for which task.  
> Full detail: [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) · Pocket card: [`SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md) · Test plan: [`SCRIPTS_TEST_PLAN.md`](SCRIPTS_TEST_PLAN.md)

---

## Decision flowchart

```mermaid
flowchart TD
    A[You want to...] --> B{What do you want to do?}

    B -->|Develop| C[Development Scripts]
    B -->|Use Docker| D[Docker Scripts]
    B -->|Maintain| E[Maintenance Scripts]
    B -->|Deploy / validate stack| F[Deployment Scripts]
    B -->|Debug / CI gates| G[Helpers]

    C --> C1[start-dev.bat]
    C --> C2[start-backend.bat]
    C --> C3[start-admin.bat]
    C --> C4[start-pos.bat]
    C --> C5[start-sites.bat]
    C --> C6[test-all.bat]
    C --> C7[clean-all.bat]

    D --> D1[docker-up.bat]
    D --> D2[docker-down.bat]
    D --> D3[docker-clean.bat]
    D --> D4[docker-status.bat]

    E --> E1[clean-backend.bat]
    E --> E2[dev-purge-tenant.bat]
    E --> E3[generate-dep-export.bat]
    E --> E4[ensure-bmf-prueftool.bat]
    E --> E5[fix-antd.bat]
    E --> E6[dev-mail.bat]
    E --> E7[smoke-test.bat]

    F --> F1[deploy.bat]
    F --> F2[rollback.bat]

    G --> G1[run-with-log.bat]
    G --> G2[validate-scripts.bat]
    G --> G3[test-scripts.bat]
    G --> G4[create-bat-wrappers.bat]

    C1 --> N1["npm run dev"]
    D1 --> N2["docker compose up -d"]
    F1 --> N3["prod compose + smoke + backup gate"]
    F2 --> N4["git reset --hard HEAD~1 + prod rebuild"]
```

> **Note:** `deploy.bat` uses `docker-compose.prod.yml` with confirmations (operator checklist on the deploy host). It is **not** a substitute for GitHub Actions cloud CD alone. Prefer `git revert` over `rollback.bat` on shared branches. `scripts\smoke-test.bat` is lightweight curl; full suite is `run-comprehensive-smoke.bat`.

---

## Category → script → when

| Category | Script | When |
|----------|--------|------|
| Development | [`start-dev.bat`](../start-dev.bat) | Daily full stack |
| Development | [`start-backend.bat`](../start-backend.bat) | API only |
| Development | [`start-admin.bat`](../start-admin.bat) | FA only |
| Development | [`start-pos.bat`](../start-pos.bat) | POS only |
| Development | [`start-sites.bat`](../start-sites.bat) | Tenant Sites only |
| Development | [`test-all.bat`](../test-all.bat) | Before commit |
| Development | [`clean-all.bat`](../clean-all.bat) | Stale build artifacts |
| Docker | [`docker-up.bat`](../docker-up.bat) | Start Compose |
| Docker | [`docker-down.bat`](../docker-down.bat) | Stop Compose |
| Docker | [`docker-status.bat`](../docker-status.bat) | Is it up? |
| Docker | [`docker-clean.bat`](../docker-clean.bat) | Wipe volumes (**data loss**) |
| Maintenance | [`scripts/clean-backend.bat`](../scripts/clean-backend.bat) | Corrupted `bin`/`obj` |
| Maintenance | [`scripts/dev-purge-tenant.bat`](../scripts/dev-purge-tenant.bat) | Dev catalog reset |
| Maintenance | [`scripts/generate-dep-export.bat`](../scripts/generate-dep-export.bat) | DEP fixtures |
| Maintenance | [`scripts/ensure-bmf-prueftool.bat`](../scripts/ensure-bmf-prueftool.bat) | Prüftool JARs |
| Maintenance | [`scripts/fix-antd.bat`](../scripts/fix-antd.bat) | Ant Design 6 fixes |
| Maintenance | [`scripts/dev-mail.bat`](../scripts/dev-mail.bat) | Local mail capture |
| Maintenance | [`scripts/smoke-test.bat`](../scripts/smoke-test.bat) | Lightweight curl smoke (stack up) |
| Deployment | [`deploy.bat`](../deploy.bat) | Prod Compose + smoke + backup gate |
| Deployment | [`rollback.bat`](../rollback.bat) | Discard last commit + rebuild |
| Helpers | [`scripts/run-with-log.bat`](../scripts/run-with-log.bat) | Log any command |
| Helpers | [`scripts/validate-scripts.bat`](../scripts/validate-scripts.bat) | Pairing + docs CI gate |
| Helpers | [`scripts/test-scripts.bat`](../scripts/test-scripts.bat) | Dry-run bat structure |
| Helpers | [`scripts/create-bat-wrappers.bat`](../scripts/create-bat-wrappers.bat) | Generate missing `.bat` |

Anchors for deep links in GitHub / VS Code preview: see headings under [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) (e.g. `#start-devbat`, `#docker-upbat`, `#deploybat`).

---

## Typical day

```mermaid
sequenceDiagram
    participant Dev as Developer
    participant Bat as Root .bat
    participant Stack as API/FA/POS

    Dev->>Bat: start-dev.bat
    Bat->>Stack: npm run dev
    Dev->>Dev: Code changes
    Dev->>Bat: test-all.bat
    Dev->>Bat: scripts\smoke-test.bat
    Note over Dev,Stack: Optional Docker path: docker-up → status → docker-down
```

---

## Related

| Doc | Purpose |
|-----|---------|
| [`SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md) | One-screen icon card |
| [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) | Full reference |
| [`SCRIPTS_TEST_PLAN.md`](SCRIPTS_TEST_PLAN.md) | Automated + manual tests |
| [`BATCH_FILES.md`](BATCH_FILES.md) | Short inventory |
| [`../scripts/README.md`](../scripts/README.md) | Folder conventions |
