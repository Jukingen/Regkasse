# Operational SOP — FE-Admin Users (Benutzerverwaltung)

Standard Operating Procedure for restaurant managers and authorised staff using the FE-Admin Users module. Aligns with RKSV/BAO traceability and internal audit.

**Audience:** Restaurant / branch managers, HR or designated administrators.  
**System:** FE-Admin → Users (Benutzerverwaltung).

---

## 1. New cashier onboarding

### Who can do it

- **Administrator** (SuperAdmin) or **BranchManager** (if enabled for your tenant).
- Cashiers and Kellner cannot create users; they have no access to the Users module.

### Steps

1. Log in to FE-Admin with an Administrator (or BranchManager) account.
2. Open **Users** (Benutzerverwaltung).
3. Click **Benutzer anlegen** (Create user).
4. Fill in **required fields:** Benutzername, Passwort (min. 6 characters), Vorname, Nachname, Rolle (e.g. Cashier or Kellner), and optionally E-Mail, Mitarbeiternummer, Steuernummer, Notizen.
5. Click **Speichern** (Save).

### Required reason text

- **Not applicable** for create. No reason field is shown.
- **Recommended:** Use **Notizen** to record onboarding purpose (e.g. “Einstieg 01.04.2025, Standort Wien”).

### Expected system logs

- **Audit log:** One entry with action **USER_CREATE** (or equivalent).
- **Fields recorded:** Actor (who created), target user id, timestamp, source (e.g. web-admin).
- **Where to check:** Users → select the new user → **Aktivität** (Activity) tab, or global Audit Log filtered by action USER_CREATE.

### Rollback / escalation path

- **Undo create:** There is no “delete user” in normal flow. If the account was created by mistake, **deactivate** the user immediately and add a reason (see section 3). Optionally contact IT to avoid reusing the same username if you need to recreate later.
- **Wrong role or data:** Use **Bearbeiten** (Edit) to correct role, name, or other fields; audit will show USER_UPDATE.
- **Escalation:** If you cannot create users (e.g. missing role or 403), contact your system administrator or IT support. Do not share your admin credentials.

---

## 2. Temporary suspension

### Who can do it

- **Administrator** or **BranchManager** (same as deactivation).
- Purpose: Block access without treating the person as “left the company” (e.g. unpaid leave, investigation).

### Steps

1. Open **Users** and find the user (use filters: Status = Aktiv).
2. Click **Deaktivieren** (Deactivate).
3. Enter a **mandatory reason** in the field “Grund (für Audit erforderlich)”.
4. Confirm with **Deaktivieren**. The user’s sessions are invalidated; they can no longer log in.

### Required reason text

- **Mandatory.** The system will not allow deactivation without a reason.
- **Recommended format:** Short, factual, and traceable, e.g.  
  - “Temporäre Suspendierung – unbezahlter Urlaub bis 15.05.2025”  
  - “Interne Prüfung – Zugang bis auf Weiteres gesperrt”

### Expected system logs

- **Audit log:** Entry with action **USER_DEACTIVATE**.
- **Content:** Actor, target user id, timestamp, **reason** (stored in Notes/RequestData), IP, User-Agent, source (web-admin).
- **ApplicationUser:** DeactivatedAt, DeactivatedBy, DeactivationReason are set and kept for reporting.

### Rollback / escalation path

- **Rollback:** Use **Reaktivieren** (Reactivate) when the suspension ends (see section 4). Add an optional reason (e.g. “Suspendierung aufgehoben ab 16.05.2025”).
- **Escalation:** If you need to suspend someone but your role cannot deactivate, escalate to an Administrator. For disputes or HR issues, follow internal HR and compliance procedures.

---

## 3. Employee leaving company (deactivation)

### Who can do it

- **Administrator** or **BranchManager** (for their scope).
- You **cannot** deactivate your own account; another admin must do it.

### Steps

1. Open **Users** → find the user (e.g. filter by name or role).
2. Click **Deaktivieren** (Deactivate).
3. In **“Grund (für Audit erforderlich)”** enter the **mandatory** leave reason.
4. Confirm. The user is set to **Inaktiv** and all their sessions are invalidated. Past receipts and payments remain linked to their user id (CashierId); nothing is deleted.

### Required reason text

- **Mandatory.** Empty reason is rejected by the system.
- **Recommended format:**  
  - “Ausscheiden zum [DD.MM.YYYY]”  
  - “Kündigung zum 31.03.2025 – einvernehmlich”  
  - “Vertragsende – nicht verlängert zum 30.04.2025”

### Expected system logs

- **Audit log:** **USER_DEACTIVATE** with actor, target user id, timestamp, **reason**, IP, source.
- **User record:** DeactivatedAt, DeactivatedBy, DeactivationReason stored; IsActive = false. Receipts and payments are **not** modified.

### Rollback / escalation path

- **Rollback:** If the person returns (e.g. rehired), use **Reaktivieren** (section 4). Do **not** create a second user for the same person; reactivate the existing record to preserve receipt history.
- **Escalation:** If you cannot deactivate (e.g. 403, or you are trying to deactivate yourself), ask another Administrator. For legal or HR disputes about access or data, follow your internal HR and compliance process.

---

## 4. Reactivation

### Who can do it

- **Administrator** or **BranchManager** (same as deactivation).

### Steps

1. Open **Users** and set filter **Status = Inaktiv** (or show all).
2. Find the user and click **Reaktivieren** (Reactivate).
3. Optionally enter a reason in the reactivation dialog (e.g. “Wiedereintritt ab 01.06.2025”).
4. Confirm. The user is set back to **Aktiv**; DeactivatedAt, DeactivatedBy, and DeactivationReason are cleared. The user can log in again (they may need a password reset; see section 5).

### Required reason text

- **Optional** for reactivation. The system allows reactivation without a reason.
- **Recommended:** Use the optional reason field for audit clarity, e.g. “Wiedereintritt”, “Suspendierung aufgehoben”.

### Expected system logs

- **Audit log:** Entry **USER_REACTIVATE** with actor, target user id, timestamp, optional reason, source.

### Rollback / escalation path

- **Rollback:** If reactivation was done by mistake, **deactivate** again and enter a reason (e.g. “Reaktivierung versehentlich – erneut deaktiviert”).
- **Escalation:** If the user still cannot log in after reactivation, follow the **Password reset emergency flow** (section 5) or contact IT (e.g. account lockout, MFA).

---

## 5. Password reset emergency flow

### Who can do it

- **Administrator** or **BranchManager** (force reset).
- The **affected user** can use “Forgot password” on the login page if that feature is enabled; this SOP covers **admin-initiated** force reset.

### Steps

1. Open **Users** and find the user.
2. Use the **Force password reset** (or equivalent) action for that user.
3. Enter a **new temporary password** (min. 6 characters) and confirm. The system invalidates all existing sessions for that user; they must log in with the new password.
4. **Communicate the temporary password to the user through a secure channel** (e.g. in person, or per company policy). Advise them to change it at next login if the system supports it.

### Required reason text

- **Not mandatory** in the UI for the reset itself. No reason field is required by the system.
- **Recommended:** Record in **Notizen** on the user or in your internal ticket, e.g. “Notfall-Passwort-Reset am [date] – Anforderung durch [name/Abteilung]”.

### Expected system logs

- **Audit log:** Entry **USER_PASSWORD_RESET** with actor (admin who performed reset), target user id, timestamp, source (web-admin). Session invalidation is triggered for the target user.

### Rollback / escalation path

- **Rollback:** There is no “undo” for password reset. If the new password is compromised or wrong, perform another force reset and set a new temporary password; communicate securely again.
- **Escalation:** If the user still cannot log in (e.g. account locked, wrong credentials), escalate to IT. If you do not have permission to force reset (403), contact an Administrator.

---

## 6. Monthly audit review

### Who can do it

- **Administrator**, **BranchManager**, or **Auditor** (read-only). Auditors can view users and audit data but cannot create, edit, deactivate, or reset.

### Steps

1. **User list review:** Open **Users**. Filter by **Status = Aktiv** and review who currently has access. Confirm that only current employees and authorised roles are active. Note any accounts that should be deactivated (e.g. leavers not yet processed).
2. **Activity timeline:** For a sample of users (e.g. new joiners, leavers, or high-privilege roles), open each user and check the **Aktivität** (Activity) tab. Confirm that create, deactivate, reactivate, and password reset events are present and reasons are recorded where required.
3. **Export (if available):** Use **Export user activity report** (or Audit Log export) for the past month. Store the export according to your retention and compliance policy (e.g. 7 years for RKSV/BAO).
4. **Document:** In your internal log or checklist, record: date of review, reviewer name, scope (e.g. “All active users”), and any follow-up actions (e.g. “User X deactivated – left 31.03.2025”).

### Required reason text

- **N/A** for the review itself. The review ensures that **past** actions (create, deactivate, reactivate, reset) had appropriate reasons where the system requires them (especially deactivation).

### Expected system logs

- **No new user-lifecycle event** is generated by “viewing” or “exporting”. Export and read access may be logged in general audit or access logs depending on your setup; user lifecycle events (USER_*) are created only when you create, update, deactivate, reactivate, or reset.

### Rollback / escalation path

- **Findings:** If you discover active users who should be deactivated (e.g. leavers), deactivate them and add the required reason (section 3). If you find missing or unclear reasons in past entries, document the gap and follow internal compliance/HR procedures.
- **Escalation:** If you cannot access the Users module or Activity/Export (e.g. 403), contact your Administrator. For suspected misuse or security incidents, follow your incident response and compliance escalation path.

---

## Quick reference

| Action | Who | Reason required? | Main audit action |
|--------|-----|-------------------|-------------------|
| New cashier onboarding | Administrator, BranchManager | No (Notizen recommended) | USER_CREATE |
| Temporary suspension | Administrator, BranchManager | **Yes** | USER_DEACTIVATE |
| Employee leaving (deactivation) | Administrator, BranchManager | **Yes** | USER_DEACTIVATE |
| Reactivation | Administrator, BranchManager | Optional | USER_REACTIVATE |
| Password reset (emergency) | Administrator, BranchManager | No (internal note recommended) | USER_PASSWORD_RESET |
| Monthly audit review | Administrator, BranchManager, Auditor (read) | N/A | Read/export only |

---

## Reason codes (examples for audit trail)

For **deactivation**, the system requires a non-empty reason. Recommended short texts for Austrian fiscal traceability (RKSV/BAO):

| Use case | Example reason (DE) |
|----------|----------------------|
| Employee leaving | `Ausscheiden zum 31.03.2025` |
| End of contract | `Vertragsende – nicht verlängert zum 30.04.2025` |
| Temporary suspension | `Temporäre Suspendierung – unbezahlter Urlaub bis 15.05.2025` |
| Internal review | `Interne Prüfung – Zugang bis auf Weiteres gesperrt` |
| Mutual agreement | `Kündigung zum 31.03.2025 – einvernehmlich` |

**Permission matrix and backend policy names:** See `docs/architecture/USERS_MODULE_PERMISSION_MATRIX.md`. Backend policies in use: `UsersView` (Administrator, SuperAdmin, BranchManager, Auditor), `UsersManage` (Administrator, SuperAdmin, BranchManager).

---

*This SOP is for operational use with FE-Admin Users. Final HR and legal procedures remain the responsibility of your organisation. Keep this document updated when roles or system behaviour change.*
