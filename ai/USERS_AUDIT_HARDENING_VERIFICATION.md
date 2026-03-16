# Users Audit Diff + Actor/IP Hardening – Verification

## Automated tests

- **Backend:** `UserAuditDiffHelperTests` – whitelist contains only safe fields; `CreateSafeSnapshot` excludes Notes, TaxNumber, EmployeeNumber, Password.
- **Frontend:** `auditDiffUtils.test.ts` – parseAuditDiff returns null on empty/invalid JSON (no throw); formatDiffValue placeholder/boolean/truncation.

## Manual verification checklist

1. **Audit diff whitelist**
   - [ ] Edit a user (change name, email, or role). Open Aktivitätsverlauf → last USER_UPDATE → "Änderungen ansehen". Only FirstName, LastName, Email, UserName, Role, IsActive, IsDemo appear. No Notes, TaxNumber, EmployeeNumber.

2. **Actor + IP**
   - [ ] Aktivitätsverlauf table shows "Durchgeführt von" (userId) and "IP" columns. Null/empty show "—".

3. **Diff modal stability**
   - [ ] Entry with invalid/malformed oldValues or newValues: "Änderungen ansehen" still opens modal; content shows "Keine Feldänderungen" (no crash).
   - [ ] Very long value in diff: cell wraps or truncates with "…"; layout does not break.

4. **Ansehen single entry**
   - [ ] Users list: only one "Ansehen" button per row (no duplicate "Aktivität" button). Click opens UserDetailDrawer with Aktivität + Details tabs.

5. **Destructive actions**
   - [ ] Deactivate: modal shows user name and confirm text; reason required.
   - [ ] Reset password: modal shows security note and validation; confirm semantics clear.

6. **Legacy / no-diff events**
   - [ ] FORCE_RESET_PASSWORD or USER_DEACTIVATE rows: no "Änderungen ansehen" link. Pagination and sorting unchanged.

## Limitations

- Actor column shows userId (opaque ID), not resolved display name; IP is as reported by backend (proxy headers already handled in `GetClientIpAddress`).
- Old audit records created before hardening may still contain Notes/TaxNumber/EmployeeNumber in OldValues/NewValues; they remain visible for legacy data. New records do not.
