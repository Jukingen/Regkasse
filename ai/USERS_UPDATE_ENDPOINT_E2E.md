# PUT /api/UserManagement/{id} – UpdateUser Flow and E2E Verification

## 1. UpdateUser flow summary

| Step | Action |
|------|--------|
| 1 | **Auth** – `[Authorize(Policy = "UsersManage")]`; route param `id` (user to update). |
| 2 | **Validation** – `id` required; `request` required; `request.EmployeeNumber` required (explicit check + `[Required(AllowEmptyStrings = false)]` on DTO); `ModelState.IsValid`. |
| 3 | **Load user** – `_userManager.FindByIdAsync(id)`; if null → 404. |
| 4 | **Email uniqueness** – If `request.Email` set and different from current, `FindByEmailAsync(request.Email)`; if exists → 400 "Email already exists". |
| 5 | **Employee number uniqueness** – If `request.EmployeeNumber` changed, check other active users; if duplicate → 400 "Employee number already exists". |
| 6 | **Apply fields** – Email/NormalizedEmail (if changed), FirstName, LastName, EmployeeNumber.Trim(), TaxNumber, Notes, UpdatedAt. |
| 7 | **Persistence (user)** – `_userManager.UpdateAsync(user)`; on failure → 400 with errors. |
| 8 | **Role update** – If `request.Role` set and ≠ current: GetRolesAsync → RemoveFromRolesAsync(all) → AddToRoleAsync(request.Role); update `user.Role`. |
| 9 | **Persistence (context)** – `_context.SaveChangesAsync()` (e.g. any EF-tracked changes). |
| 10 | **Audit (best-effort)** – If actor present: `TryLogUserLifecycleAsync(USER_UPDATE, …)`; if role changed, also `TryLogUserLifecycleAsync(USER_ROLE_CHANGE, …)` and `InvalidateSessionsForUserAsync(id)`. |
| 11 | **Response** – `return Ok(new { message = "User updated successfully" });` → 200. |

**Payload (UpdateUserRequest):**  
`FirstName`, `LastName` (required), `Email` (optional), `EmployeeNumber` (required), `Role` (required), `TaxNumber`, `Notes` (optional).

---

## 2. Fail point

- **Where:** After user and role update and `SaveChangesAsync`, the code called `_auditLogService.LogUserLifecycleAsync(...)` (before error isolation). The service does `_context.AuditLogs.Add(auditLog)` and `await _context.SaveChangesAsync()`.
- **Why 500:** The `audit_logs` table schema did not match the AuditLog entity (e.g. column `"Amount"` missing or wrong case in PostgreSQL). Npgsql threw **42703: column "Amount" of relation "audit_logs" does not exist**. The exception bubbled to the controller’s `catch` → **500** and `{ message = "Internal server error" }`, even though the user update had already succeeded.

---

## 3. Fix

1. **Schema (root cause)**  
   - **AlignAuditLogsTableWithEntity** migration: `DROP TABLE IF EXISTS audit_logs CASCADE` then `CREATE TABLE audit_logs (...)` with quoted PascalCase columns and indexes so the table matches the entity/AppDbContext.  
   - AddAuditLogsTable and EnsureAuditLogsTable were turned into no-ops so only this migration defines the table.  
   - **Apply:** `dotnet ef database update` (from `backend`).

2. **Error isolation**  
   - All user-lifecycle audit calls in `UserManagementController` go through **TryLogUserLifecycleAsync**, which catches exceptions from `LogUserLifecycleAsync`, logs them, and does not rethrow.  
   - So if audit insert fails (e.g. DB still wrong or transient error), the endpoint still returns **200** and `{ message = "User updated successfully" }`.

3. **Payload and persistence**  
   - No change: `EmployeeNumber` remains required (controller + DTO); update and role flow unchanged.  
   - Success response remains **200** with body `{ message = "User updated successfully" }`.

---

## 4. Test scenarios

### Backend (unit)

| Scenario | Expected |
|----------|----------|
| **UpdateUser_WhenIdEmpty_ReturnsBadRequest** | 400, validation error. |
| **UpdateUser_WhenEmployeeNumberEmpty_ReturnsBadRequest** | 400, "Employee number" in response. |
| **UpdateUser_WhenUserNotFound_ReturnsNotFound** | 404. |
| **UpdateUser_WhenValidPayload_SameRole_ReturnsOkWithMessage** | 200, `message = "User updated successfully"`, audit USER_UPDATE called once. |
| **UpdateUser_WhenAuditLogThrows_StillReturnsOk** | 200, same message (audit failure does not fail the request). |

Run:  
`dotnet test --filter "FullyQualifiedName~UserManagementControllerUserLifecycleTests"`

### E2E / manual

1. **Apply migrations**  
   `cd backend && dotnet ef database update`

2. **PUT success**  
   - `PUT /api/UserManagement/{id}`  
   - Headers: `Authorization: Bearer <token>` (user with `UsersManage`).  
   - Body: `{ "firstName": "A", "lastName": "B", "employeeNumber": "E1", "role": "Admin" }` (and optionally email, taxNumber, notes).  
   - Expect: **200**, `{ "message": "User updated successfully" }`.

3. **PUT – missing EmployeeNumber**  
   - Body without `employeeNumber` or empty string.  
   - Expect: **400**, message indicating employee number is required.

4. **GET audit (optional)**  
   - `GET /api/AuditLog/user/{id}`  
   - Expect: **200** and list (e.g. USER_UPDATE entry after the PUT).

---

## 5. Success criteria

- PUT /api/UserManagement/{id} returns **200** and `{ "message": "User updated successfully" }` when the update and (if applicable) role change succeed.  
- Audit log insert no longer causes 500; if it fails, the endpoint still returns 200 and the failure is only logged.  
- Required fields (e.g. EmployeeNumber) are enforced; invalid or missing payload returns 400 with a clear validation message.
