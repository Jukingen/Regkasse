# GET /api/UserManagement/{id} — response and userToFormValues mapping

## 1. Backend response (actual shape)

Backend `UserInfo` DTO (C#): `Id`, `UserName`, `FirstName`, `LastName`, `Email`, `EmployeeNumber`, `Role`, `TaxNumber`, `Notes`, `IsActive`, `CreatedAt`, `LastLoginAt`.

If `Program.cs` sets `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, the HTTP body is **camelCase**:

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "userName": "jdoe",
  "firstName": "John",
  "lastName": "Doe",
  "email": "jdoe@example.com",
  "employeeNumber": "E001",
  "role": "Admin",
  "taxNumber": "ATU12345678",
  "notes": "Muster-Notizen",
  "isActive": true,
  "createdAt": "2025-01-15T10:00:00Z",
  "lastLoginAt": "2025-03-08T14:30:00Z"
}
```

If **PascalCase** is returned (policy off/overridden), the body looks like:

```json
{
  "Id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "UserName": "jdoe",
  "FirstName": "John",
  "LastName": "Doe",
  "Email": "jdoe@example.com",
  "EmployeeNumber": "E001",
  "Role": "Admin",
  "TaxNumber": "ATU12345678",
  "Notes": "Muster-Notizen",
  "IsActive": true,
  "CreatedAt": "2025-01-15T10:00:00Z",
  "LastLoginAt": "2025-03-08T14:30:00Z"
}
```

Axios `response.data` is this object directly; there is no extra `data`/`result` wrapper.

---

## 2. Gateway: normalizeUserInfo output (`user` passed to the drawer)

`getUserById` → `normalizeUserInfo(raw)` reads both camelCase and PascalCase keys. The `user` passed to the drawer is **the same camelCase shape** in both cases:

```js
// user (after normalizeUserInfo — prop into the drawer)
{
  id: "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  userName: "jdoe",
  firstName: "John",
  lastName: "Doe",
  email: "jdoe@example.com",
  employeeNumber: "E001",
  role: "Admin",
  taxNumber: "ATU12345678",
  notes: "Muster-Notizen",
  isActive: true,
  createdAt: "2025-01-15T10:00:00Z",
  lastLoginAt: "2025-03-08T14:30:00Z"
}
```

---

## 3. userToFormValues(user) output (object given to the form)

Inside `UserFormDrawer`, `userToFormValues(user)` builds the object that maps 1:1 onto form fields. Form.Item `name`s: `firstName`, `lastName`, `email`, `employeeNumber`, `role`, `taxNumber`, `notes`.

For the `user` above:

```js
// userToFormValues(user) result — form.setFieldsValue(...) / initialValues
{
  firstName: "John",
  lastName: "Doe",
  email: "jdoe@example.com",
  employeeNumber: "E001",
  role: "Admin",
  taxNumber: "ATU12345678",
  notes: "Muster-Notizen"
}
```

If the backend sends some fields null/empty, the mapper returns empty strings:

```js
// Example: email and notes missing, taxNumber null
{
  firstName: "John",
  lastName: "Doe",
  email: "",
  employeeNumber: "E001",
  role: "Admin",
  taxNumber: "",
  notes: ""
}
```

---

## 4. Likely bug points

| Issue | Check |
|-------|-------|
| Does the backend really return camelCase? | Browser Network → GET `/api/UserManagement/{id}` → are keys `firstName` or `FirstName`? |
| Is `user` undefined/empty in the drawer? | On `UsersPage`, `user={editUserFull ?? undefined}`; `editUserFull` must come from the same GET. |
| Do form keys match? | Form.Item `name`: `firstName`, `lastName`, `email`, `employeeNumber`, `role`, `taxNumber`, `notes` ↔ same keys from `userToFormValues`. |
| Is role a string? | Backend `Role` is a string (for example `"Admin"`); Select `options={roleOptions}` values are strings; mapper supplies `role` as string. |

If Network shows PascalCase, the gateway `normalizeUserInfo` should still produce camelCase `user`. If the form is still empty, confirm the `user` prop is actually populated and inspect `userToFormValues(user)` in the console.
