# FinanzOnline pilot — GO / NO-GO gate

**Evidence-only:** Each **Met** box requires a **concrete pointer** to filled YAML in `docs/finanzonline-pilot-evidence-pack-template.md` (or attached pack) + `pass_fail: PASS`.  
If evidence is missing → treat as **Unmet** → **NO-GO** for that level.

**Blocker dependency:** If `docs/finanzonline-pilot-blocker-review-template.md` §2 has any **S1** or **S2** row open → **NO-GO** for pilot customer until resolved or waived in writing.

---

## Gate 1 — Ready for internal staging?

| Met | Unmet | Blocking evidence gap | Owner |
|-----|-------|------------------------|-------|
| [ ] `FinanzOnline.TransportStartup` log captured; flags match **intended** environment (sim vs real TEST) | | No G3 log | DEV |
| [ ] API starts clean after config; no crash loop | | | DEV |
| [ ] DB migrated; `finanz_online_outbox_messages` exists | | | DEV |
| [ ] At least one **SUCCESS** evidence record attempted OR waived with ticket | | No SUCCESS YAML | OP+DEV |

**Verdict:** `GO staging` / `NO-GO staging` — **Date:** ______ **Signed:** ______

---

## Gate 2 — Ready for BMF TEST validation?

Requires **Gate 1 GO** unless explicitly risk-accepted.

| Met | Unmet | Blocking evidence gap | Owner |
|-----|-------|------------------------|-------|
| [ ] Config matches runbook §3 for **live BMF TEST** (three `UseSimulation=false`, `EnableRealTestSubmission=true`, `EnableRealTestQuery=true` when protocol path needed) | | Redacted config summary missing | DEV |
| [ ] **SUCCESS** evidence: `pass_fail: PASS` **and** runbook §5 primary evidence (logs + DB outbox terminal + admin API) | | Outbox not `ProtocolSuccess` (or agreed terminal) | OP+DEV |
| [ ] **FAILURE** evidence: `pass_fail: PASS` (failure observable; payment not wrongly rolled back) | | | OP+DEV |
| [ ] **RETRY** evidence: `pass_fail: PASS` | | | OP+DEV |
| [ ] Blocker review §3.4 **PASS** (no O3/O4 contradiction) | | Paste row # from blocker doc | DEV |
| [ ] Blocker review §2 empty or only S3/S4 | | Open S1/S2 | DEV lead |

**Verdict:** `GO BMF TEST` / `NO-GO BMF TEST` — **Date:** ______ **Signed:** ______

---

## Gate 3 — Ready for pilot customer?

Requires **Gate 2 GO** + product/ops sign-off outside this doc.

| Met | Unmet | Blocking evidence gap | Owner |
|-----|-------|------------------------|-------|
| [ ] All Gate 2 checkboxes | | | |
| [ ] **A1–A3** PASS in blocker review §3.5 (operator can use outbox; understands status vs submission) | | | OP lead |
| [ ] Runbook §7 acknowledged: no reliance on status tile / test-connection alone | | | OP |
| [ ] PROD/cutover: if customer is PROD, `FinanzOnline:CutoverGuard` and credentials tracked separately | | Not applicable / ticket # | Product+DEV |

**Verdict:** `GO pilot customer` / `NO-GO pilot customer` — **Date:** ______ **Signed:** ______

---

## Not ready because? (fill when NO-GO)

```text
Gate level:
Unmet rows:
Blocking evidence gaps (exact):
Open blockers (ids):
Recommended next actions:
Target date:
```

---

## Evidence pack index (required for Gate 2+)

Paste when complete:

```yaml
evidence_pack_index:
  environment:
  api_git_sha:
  completed_utc:
  success_artifact:   # path or ticket attachment id
  failure_artifact:
  retry_artifact:
  blocker_review_artifact:  # path to filled finanzonline-pilot-blocker-review-template.md
```

---

## Summary table

| Level | Condition for GO |
|-------|------------------|
| Internal staging | Infra + G-steps; optional SUCCESS attempt |
| BMF TEST validation | SUCCESS+FAILURE+RETRY PASS + §5 evidence + no S1/S2 blockers |
| Pilot customer | Gate 2 GO + operator clarity + non-technical sign-off |
