# Frontend Admin — User Feedback Loop

**Owner:** FA maintainers (Super Admin reviews)  
**Last updated:** 2026-07-21  
**Review cadence:** weekly (Monday triage)

Operators submit structured feedback from any protected FA page. Super Admins triage status so submitters see progress in the same widget.

For the **2026-08 sidebar IA / breadcrumb redesign**, use the dedicated checklist and interview form in [`MENU_IA_REDESIGN_QA.md`](./MENU_IA_REDESIGN_QA.md) (in-app feedback: category **Ease of use**, title prefix `Menu IA 2026-08:`).

---

## 1. How it works

```text
Operator (any authenticated FA user + tenant context)
  └─ Floating "Feedback" button → Drawer
       ├─ Submit: category + title + message (+ optional rating / page path)
       └─ My feedback: status + reviewer note (closes the loop)

Super Admin (system.critical)
  └─ /admin/feedback inbox → set Under Review / In Progress / Implemented / Declined / Duplicate
```

| Storage | PostgreSQL table `admin_user_feedback` (tenant-scoped) |
| API | `POST/GET /api/admin/feedback`, `GET …/mine`, `PATCH …/{id}/status` |
| External tools | Optional later (Canny / UserVoice) — not required; DB is source of truth |

---

## 2. Categories & statuses

| Category | Use |
| -------- | --- |
| `EaseOfUse` | UX / clarity (optional 1–5 rating) |
| `Performance` | Speed / jank (optional 1–5 rating) |
| `FeatureRequest` | New capability ideas |
| `Bug` | Defects |

| Status | Meaning (shown to submitter) |
| ------ | ---------------------------- |
| `UnderReview` | Default on submit |
| `InProgress` | Accepted into a sprint / backlog |
| `Implemented` | Shipped or otherwise done |
| `Declined` | Won't do (with short note) |
| `Duplicate` | Points to an existing item via note |

---

## 3. Weekly review process

**When:** every Monday (or first working day of the week)  
**Who:** Super Admin / FA product owner  
**Where:** `/admin/feedback` (filter default: Under Review)

### Checklist

1. Open inbox; filter **Under review**.
2. For each item: set **In progress**, **Implemented**, **Declined**, or **Duplicate**.
3. Add a short **reviewer note** (visible to the submitter in “My feedback”).
4. Optionally file a ticket / tech-debt entry for larger items.
5. Spot-check “My feedback” as a normal user after status changes.

Add a calendar invite: **“FA Feedback triage (weekly)”**.

---

## 4. Code map

| Layer | Path |
| ----- | ---- |
| Entity / migration | `backend/Models/AdminUserFeedback.cs`, `Migrations/20260721140000_AddAdminUserFeedback.cs` |
| API | `backend/Controllers/AdminFeedbackController.cs` |
| Service | `backend/Services/Feedback/AdminFeedbackService.cs` |
| FA client | `frontend-admin/src/api/manual/adminFeedback.ts` |
| Widget | `frontend-admin/src/components/feedback/FeedbackWidget.tsx` |
| Inbox | `frontend-admin/src/app/(protected)/admin/feedback/page.tsx` |
| i18n | `frontend-admin/src/i18n/locales/{de,en,tr}/feedback.json` |

Apply migration:

```bash
cd backend
dotnet ef database update
```

---

## 5. Closing the loop

Submitters do **not** need email for MVP: open the floating widget → **My feedback** → see status tags (`Implemented`, `In Progress`, …) and reviewer notes.

---

## 6. Privacy

- Page path is pathname only (query/hash stripped).
- No passwords or tokens in the form.
- Tenant isolation via EF filters; Super Admin list uses `IgnoreQueryFilters` with `system.critical`.
