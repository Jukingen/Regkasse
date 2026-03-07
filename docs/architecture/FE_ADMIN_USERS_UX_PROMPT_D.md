# FE-Admin Users Module UX — Prompt D Deliverable

## 1. File-by-file patch plan

| File | Change |
|------|--------|
| `frontend-admin/src/features/users/constants/copy.ts` | **New.** i18n-ready keys for users module (title, table headers, filters, actions, modals, empty/error messages). |
| `frontend-admin/src/features/users/api/usersApi.ts` | Add `query?: string` to `UsersListParams`; when `query` is set call `GET /api/UserManagement/search?query=`, else existing GET with role/isActive. |
| `frontend-admin/src/features/users/hooks/useUsersList.ts` | No signature change; hook already passes params to `getUsersList` (query included). |
| `frontend-admin/src/features/users/components/UserDetailDrawer.tsx` | **New.** Drawer with Tabs: Activity (UserActivityTimeline), Details (Descriptions). Used for View / Activity. |
| `frontend-admin/src/features/users/components/UserFormDrawer.tsx` | **New.** Drawer for Create/Edit user form; same fields as before; uses `usersCopy`; footer Save/Cancel. |
| `frontend-admin/src/features/users/components/UserActivityTimeline.tsx` | Use `usersCopy` for column titles and text; add error state (Alert + Retry); add empty state (Empty). |
| `frontend-admin/src/app/(protected)/users/page.tsx` | Table columns: name, email, role, branch (placeholder "—"), status, last login, actions. Filters: search (Input.Search), role, status. Loading via Table loading; empty via locale.emptyText (Empty); error via Alert + Retry. Permission: `useAuth().user?.role === 'Administrator'` to show Create, Edit, View, Deactivate, Reactivate. Create/Edit via UserFormDrawer; View/Activity via UserDetailDrawer; Deactivate/Reactivate keep Modal with reason required. All labels from `usersCopy`. |
| `docs/architecture/FE_ADMIN_USERS_UX_PROMPT_D.md` | **This file.** Patch plan, code summary, QA checklist. |

## 2. Code summary

- **Copy:** `usersCopy` in `features/users/constants/copy.ts` — all UI strings in one place for future i18n.
- **Search:** `getUsersList({ query })` calls backend `GET /api/UserManagement/search?query=...` when query is non-empty; otherwise GET with role/isActive.
- **Drawers:** UserDetailDrawer (Details + Activity tabs); UserFormDrawer (create/edit).
- **States:** Table loading; Empty for no data; Alert + Retry on list error; Activity timeline has empty and error + Retry.
- **Permissions:** Only Administrator sees Create, Edit, View, Deactivate, Reactivate; Activity button visible to all (opens same drawer).

## 3. Manual QA checklist

### List & filters
- [ ] Users table shows columns: Name (with avatar), Email, Role, Branch (placeholder "—"), Status, Last login, Actions.
- [ ] Filter by Role: dropdown filters list; "Alle" when cleared.
- [ ] Filter by Status: Active / Inaktiv; list updates.
- [ ] Search: typing and Enter (or search button) calls search API; results update; clear search restores list (role/status still applied when no query).
- [ ] Loading: table shows loading state while list or search is in progress.
- [ ] Empty: when no users match, table shows empty state (no data message).
- [ ] Error: when list/search fails (e.g. network off), error Alert with Retry appears; Retry refetches.

### Create / Edit (Administrator only)
- [ ] "Benutzer anlegen" visible only when logged in as Administrator.
- [ ] Create: opens Drawer; all fields (userName, password, email, firstName, lastName, employeeNumber, role, taxNumber, notes); required validation; Submit creates user and closes drawer; list refreshes; success message.
- [ ] Edit: "Bearbeiten" opens Drawer with user data; changing and Save updates user; list refreshes; success message.
- [ ] Non-admin: Create button and Edit/Deactivate/Reactivate/View buttons not shown (only Activity visible).

### Deactivate / Reactivate (Administrator only)
- [ ] Deactivate: reason field required; submit calls API; success message; user status becomes Inaktiv; list refreshes.
- [ ] Reactivate: confirm modal; submit reactivates; list refreshes.
- [ ] Deactivate and Reactivate buttons only for Administrator.

### Activity timeline
- [ ] "Aktivität" (or View) opens UserDetailDrawer with tabs: Activity, Details.
- [ ] Activity tab: table of audit entries (time, action, description, status); pagination works.
- [ ] Empty activity: shows empty state.
- [ ] Error loading activity: shows Alert + Retry.
- [ ] Details tab: shows user fields (email, userName, role, status, employeeNumber, lastLogin, notes).

### i18n readiness
- [ ] All user-facing text in users module comes from `usersCopy` (no hardcoded German in page/drawers/timeline).

### Regression
- [ ] Existing UserManagement API (GET, POST, PUT, deactivate, reactivate) still used; no breaking changes.
- [ ] Audit log still populated for create/update/deactivate/reactivate (backend).

---

**Note:** Branch column shows "—" until backend provides branch data. Search and list require Administrator role on backend; non-admins may get 403 on load if backend restricts GET.
