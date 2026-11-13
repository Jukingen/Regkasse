# Email configuration (SMTP and local dev capture)

> **Find this doc:** `docs/EMAIL_CONFIGURATION.md`  
> **Secrets / env vars:** [`backend/CONFIGURATION.md`](../backend/CONFIGURATION.md)  
> **Local forgot-username test:** `scripts/dev-mail-test.bat`

Technical reference for all outbound email in the Regkasse backend. Do **not** commit real SMTP passwords; use user secrets or environment variables in non-local environments.

---

## Quick summary

| Environment | Default behaviour | What you configure |
|-------------|-------------------|-------------------|
| **Development** | Emails captured to `backend/App_Data/dev-mail/*.txt` | Optional `Email:Smtp` if you also want a real inbox / Mailpit |
| **Staging / Production** | Real SMTP only (no file capture) | `Email:Smtp` (required for any email feature) |

**SMTP is optional for day-to-day user management.** New users get a one-time password in the API/UI response; invitation emails were removed. SMTP is needed when you want welcome mail, forgot-username/password, activity alerts, license reports, invoice resend, restore approval, etc.

---

## Configuration files

| File | Purpose |
|------|---------|
| `backend/appsettings.example.json` | Tracked template — `Email:Smtp`, `License:ReportEmail` |
| `backend/appsettings.Development.example.json` | Tracked template — adds `Email:DevCapture` |
| `backend/appsettings.json` | Local (gitignored) — copy from examples |
| `backend/appsettings.Development.json` | Local Development overlay (gitignored) |
| User secrets / env vars | Override any key without editing tracked files |

Copy templates once:

```bash
cd backend
cp appsettings.example.json appsettings.json
cp appsettings.Development.example.json appsettings.Development.json
```

Environment variable names use `__` instead of `:` (e.g. `Email__Smtp__Host`).

---

## `Email:Smtp` (shared SMTP client)

**Section:** `Email:Smtp`  
**Source:** `backend/Configuration/EmailSmtpOptions.cs`  
**Transport:** `System.Net.Mail.SmtpClient` (plain text bodies)

| Key | Env variable | Default | Required for send |
|-----|--------------|---------|-------------------|
| `Host` | `Email__Smtp__Host` | `""` | Yes — if empty, SMTP send is skipped |
| `Port` | `Email__Smtp__Port` | `587` | — |
| `EnableSsl` | `Email__Smtp__EnableSsl` | `true` | — |
| `User` | `Email__Smtp__User` | `""` | Only if server requires auth |
| `Password` | `Email__Smtp__Password` | `""` | Store in user secrets / env only |
| `From` | `Email__Smtp__From` | `""` | Yes — envelope-from |
| `SupportContact` | `Email__Smtp__SupportContact` | optional | Shown in user-facing transactional mail; falls back to `From` |
| `LicenseReminderRecipients` | `Email__Smtp__LicenseReminderRecipients` | `""` | Comma/semicolon list for license urgency mail |
| `LicenseReportRecipients` | `Email__Smtp__LicenseReportRecipients` | `""` | Scheduled license inventory mail; falls back to reminder list |

**“Configured” rule (most services):** `Host` and `From` are both non-empty.

Example (placeholders only — do not commit credentials):

```json
"Email": {
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "EnableSsl": true,
    "User": "smtp-user",
    "Password": "",
    "From": "noreply@regkasse.at",
    "SupportContact": "support@regkasse.at",
    "LicenseReminderRecipients": "ops@regkasse.at",
    "LicenseReportRecipients": "licensing@regkasse.at"
  }
}
```

Production secrets via user secrets:

```bash
cd backend
dotnet user-secrets set "Email:Smtp:Host" "smtp.example.com"
dotnet user-secrets set "Email:Smtp:From" "noreply@regkasse.at"
dotnet user-secrets set "Email:Smtp:User" "your-smtp-user"
dotnet user-secrets set "Email:Smtp:Password" "YOUR_SMTP_PASSWORD"
```

---

## `Email:DevCapture` (Development only)

**Section:** `Email:DevCapture`  
**Source:** `backend/Configuration/EmailDevCaptureOptions.cs`, `backend/Services/Email/DevEmailOutboxWriter.cs`

Active only when `ASPNETCORE_ENVIRONMENT=Development` **and** `Enabled=true`.

| Key | Env variable | Default |
|-----|--------------|---------|
| `Enabled` | `Email__DevCapture__Enabled` | `true` |
| `Directory` | `Email__DevCapture__Directory` | `App_Data/dev-mail` (relative to backend content root) |

Captured files look like:

```text
To: user@example.com
Subject: Regkasse Admin – Benutzername
CapturedUtc: 2026-06-21T12:34:56.7890000Z

(body)

--- Dev debug ---
Matched account: role=Manager, current username=manager1, userId=...
```

**Forgot-username** and **forgot-password** use wrapper services in Development (`DevCapturingForgotUsernameEmailService`, `DevCapturingForgotPasswordEmailService`): they write to disk first, then optionally send via SMTP if `Email:Smtp` is also configured.

Log line when capture succeeds:

```text
Dev email captured for ab***@example.com. File: ...\backend\App_Data\dev-mail\20260621-....txt
```

---

## What sends email (feature matrix)

All rows below use `Email:Smtp` unless noted. If SMTP is not configured, the feature is skipped (usually with a warning log); API endpoints that anti-enumerate still return success.

| Feature | Service | Trigger | Recipients |
|---------|---------|---------|------------|
| Tenant onboarding welcome | `WelcomeEmailService` | Super Admin onboarding wizard | New tenant admin email |
| Username change notice | `UsernameChangeEmailService` | Admin username change | User email |
| Forgot username (Admin) | `ForgotUsernameEmailService` / dev wrapper | `POST /api/Auth/forgot-username` (`clientApp=admin`) | Request email — **current username only** |
| Forgot password (Admin) | `ForgotPasswordEmailService` / dev wrapper | `POST /api/Auth/forgot-password` (`clientApp=admin`) | Request email + Identity reset token |
| Invoice PDF resend | `InvoiceEmailService` | Admin invoice resend API | Customer or override recipient |
| Scheduled audit report | `AuditReportEmailService` | `AuditReportSchedulerHostedService` | Configured report recipients (scheduler) |
| License urgency reminders | `LicenseReminderEmailSender` | License reminder hosted job | Tenant contact or `LicenseReminderRecipients` |
| License weekly / expiry reports | `LicenseReminderEmailSender` | Scheduled when `License:ReportEmail` flags enabled | `LicenseReportRecipients` or reminder list |
| Activity / notification events | `ActivityEventEmailNotifier` | Critical/warning activity events | Tenant notification settings, then fallbacks |
| Manual restore approval | `ManualRestoreApprovalEmailService` | `POST /api/admin/restore/request` | Other Super Admin emails or `ManualRestoreApproval:FallbackApproverEmails` |
| Payment reversal approval | `PaymentReversalApprovalEmailService` | High-risk cancel/refund workflow | Manager approvers or `PaymentReversalApproval:FallbackApproverEmails` |

**Not sent by email (by design):**

- Tenant/platform **user creation** — one-time password in HTTP response / FA modal
- Tenant user **admin password reset** — returned in API/UI, not mailed
- **User invitations** — removed

Related operator docs: [`USER_MANAGEMENT.md`](USER_MANAGEMENT.md), [`CUSTOMER_ONBOARDING.md`](CUSTOMER_ONBOARDING.md).

---

## Related configuration sections

### `License:ReportEmail`

**Source:** `backend/Configuration/LicenseReportEmailOptions.cs`  
**Uses:** `Email:Smtp` + `LicenseReportRecipients` / `LicenseReminderRecipients`

| Key | Purpose |
|-----|---------|
| `EnableWeeklySummary` | Weekly issued-license summary |
| `EnableIssuedExpiryAlerts` | Alerts at 30 / 15 / 7 days before expiry |
| `WeeklySummaryDayOfWeekUtc` | 0 = Sunday … 6 = Saturday |
| `RunHourUtc` / `RunMinuteUtc` | Schedule (UTC) |

### `ActivityNotifications`

**Source:** `backend/Configuration/ActivityNotificationOptions.cs`  
**Uses:** `Email:Smtp` + per-tenant settings in the database

| Key | Purpose |
|-----|---------|
| `EmailEnabled` | Master switch for activity email channel |
| `FallbackEmailRecipients` | Used when tenant has no `EmailRecipients` in notification config |
| `MinimumOutboundSeverity` | Default `Warning` |

Tenant recipients are configured in **Frontend Admin → Settings → Notifications** (stored in `TenantNotificationConfig` / `NotificationConfig.EmailRecipients`).

Fallback chain in `ActivityEventEmailNotifier`:

1. Tenant `EmailRecipients`
2. `ActivityNotifications:FallbackEmailRecipients`
3. `Email:Smtp:LicenseReminderRecipients`

### `ManualRestoreApproval`

**Source:** `backend/Configuration/ManualRestoreApprovalOptions.cs`  
Requires SMTP for approval-token emails.

| Key | Notes |
|-----|-------|
| `Enabled` | When `false`, restore approval API returns 503 |
| `FallbackApproverEmails` | Static inboxes if no other Super Admin emails exist |
| `ApprovalTokenTtlMinutes` | Default 15 |

See [`backend/CONFIGURATION.md`](../backend/CONFIGURATION.md) (Manual restore approval).

### `PaymentReversalApproval`

**Source:** `backend/Configuration/PaymentReversalApprovalOptions.cs`  
High-risk payment cancel/refund manager approval.

| Key | Notes |
|-----|-------|
| `FallbackApproverEmails` | When no manager emails resolved |
| `HighRiskAmountThresholdEur` | Default €100 |

---

## Local development workflow

### 1. Start backend

```bash
cd backend
dotnet run
```

Ensure `ASPNETCORE_ENVIRONMENT=Development` and `Email:DevCapture:Enabled=true` (default in `appsettings.Development.example.json`).

### 2. Test forgot-username (recommended)

```bat
scripts\dev-mail-test.bat
```

Or PowerShell:

```powershell
.\scripts\test-forgot-username-email.ps1 -Email you@example.com
```

Optional: `scripts/dev-mail.local.env` with `BASE_URL=http://localhost:5184` if the API port differs (see `scripts/dev-mail.local.env.example`).

### 3. Verify capture (not real inbox)

| Check | Success signal |
|-------|----------------|
| New file under `backend/App_Data/dev-mail/` | Mail body was built and written |
| Backend log | `Dev email captured for ...` |
| Script **SONUC** box | Username shown when user exists |
| FA `/login/forgot-username` | Always shows success (anti-enumeration) — **not** proof of delivery |

**Important:** On localhost, Gmail/Outlook will **not** receive mail unless you also configure `Email:Smtp` to a real or test SMTP server.

### 4. Optional real SMTP in Development

You can run capture **and** SMTP together (e.g. Mailpit on port 1025):

```json
"Email": {
  "DevCapture": { "Enabled": true },
  "Smtp": {
    "Host": "localhost",
    "Port": 1025,
    "EnableSsl": false,
    "From": "noreply@regkasse.local"
  }
}
```

---

## Production / staging checklist

1. Set `Email:Smtp:Host` and `Email:Smtp:From` (minimum).
2. Set `User` / `Password` if the provider requires authentication.
3. Confirm `EnableSsl` and `Port` match your provider (587 STARTTLS vs 465, etc.).
4. Set operational recipient lists (`LicenseReminderRecipients`, `LicenseReportRecipients`, fallbacks for restore/reversal/activity as needed).
5. Configure tenant notification emails in FA for each mandant that should receive activity alerts.
6. Enable `License:ReportEmail` flags only when recipients are defined.
7. Send a test via forgot-username or a controlled welcome/onboarding run; check provider logs and recipient inbox.
8. Monitor backend logs for `SMTP not configured or send failed` warnings.

---

## Troubleshooting

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| FA says “sent” but no inbox mail (localhost) | Expected — dev capture only | Check `App_Data/dev-mail/` or run `dev-mail-test.bat` |
| No new `.txt` file after forgot-username | Email not registered or user inactive | FA still returns 200; verify user in Admin → Users |
| `Forgot-username: SMTP not configured or send failed` | Production without SMTP, or capture disabled in Dev | Configure `Email:Smtp` or enable DevCapture |
| Welcome email skipped after onboarding | `Host` or `From` empty | Configure SMTP; UI still shows credentials once |
| Activity emails never arrive | `ActivityNotifications:EmailEnabled=false`, no tenant recipients, no fallbacks | Configure tenant notification emails + SMTP |
| Restore approval mail missing | No Super Admin emails + empty `FallbackApproverEmails` | Add fallback approver inboxes + SMTP |

After code changes to email services, **restart the backend** (`dotnet run`); a stale process may still run old templates.

---

## Security notes

- Never commit `Email:Smtp:Password` in tracked JSON.
- Backend logs mask recipient addresses in dev capture (`ab***@example.com`).
- Forgot-username/password endpoints always return generic success to prevent account enumeration.
- Audit logs record email-related security events; passwords and reset tokens must not appear in logs.

---

## Key source files

| Area | Path |
|------|------|
| SMTP options | `backend/Configuration/EmailSmtpOptions.cs` |
| Dev capture options | `backend/Configuration/EmailDevCaptureOptions.cs` |
| Disk writer | `backend/Services/Email/DevEmailOutboxWriter.cs` |
| DI registration | `backend/ApplicationHost.cs` (Development vs non-Development wrappers) |
| Auth endpoints | `backend/Controllers/AuthController.cs` (`forgot-username`, `forgot-password`) |
| Local test scripts | `scripts/dev-mail-test.bat`, `scripts/test-forgot-username-email.ps1` |
