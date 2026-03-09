# GET /api/UserManagement/{id} – Response & userToFormValues mapping

## 1. Backend response (gerçek şekil)

Backend `UserInfo` DTO (C#): `Id`, `UserName`, `FirstName`, `LastName`, `Email`, `EmployeeNumber`, `Role`, `TaxNumber`, `Notes`, `IsActive`, `CreatedAt`, `LastLoginAt`.

`Program.cs` içinde `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` **açıksa** HTTP body **camelCase** gelir:

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

Eğer bir sebeple **PascalCase** dönüyorsa (policy kapalı/override edilmişse) body şöyle olur:

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

Axios `response.data` ile bu obje doğrudan gelir; ek `data`/`result` sarmalayıcısı yok.

---

## 2. Gateway: normalizeUserInfo çıktısı (drawer’a giden `user`)

`getUserById` → `normalizeUserInfo(raw)` hem camelCase hem PascalCase key’leri okuyor. Drawer’a giden `user` her iki durumda da **aynı camelCase şekilde**:

```js
// user (normalizeUserInfo sonrası – drawer’a gelen prop)
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

## 3. userToFormValues(user) çıktısı (form’a verilen obje)

Mapper, `UserFormDrawer` içinde `userToFormValues(user)` ile form alanlarına birebir gidecek objeyi üretir. Form.Item `name`’leri: `firstName`, `lastName`, `email`, `employeeNumber`, `role`, `taxNumber`, `notes`.

Yukarıdaki `user` ile:

```js
// userToFormValues(user) sonucu – form.setFieldsValue(...) / initialValues
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

Eğer backend bazı alanları null/boş gönderirse mapper boş string döner:

```js
// Örnek: email ve notes yok, taxNumber null
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

## 4. Olası bug noktaları

| Sorun | Kontrol |
|--------|--------|
| Backend gerçekten camelCase mi dönüyor? | Tarayıcı Network → GET `/api/UserManagement/{id}` → Response body’de key’ler `firstName` mi `FirstName` mi? |
| `user` drawer’a undefined/boş mu geliyor? | `UsersPage` içinde `user={editUserFull ?? undefined}`; `editUserFull` aynı GET cevabından gelmeli. |
| Form key’leri uyuşuyor mu? | Form.Item `name`: `firstName`, `lastName`, `email`, `employeeNumber`, `role`, `taxNumber`, `notes` ↔ `userToFormValues` çıktısı aynı key’ler. |
| Role string mi? | Backend `Role` string (örn. `"Admin"`); Select `options={roleOptions}` value’lar da string; mapper `role` string veriyor. |

Network’te body PascalCase ise gateway `normalizeUserInfo` ile yine camelCase `user` üretmeli; buna rağmen form boşsa, `user` prop’unun gerçekten dolu gelip gelmediği ve `userToFormValues(user)` çıktısının console ile doğrulanması gerekir.
