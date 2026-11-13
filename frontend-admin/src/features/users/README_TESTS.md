# Users module test pack

> **Status:** NEEDS HUMAN REVIEW. Validate freshness before treating this as the current source of truth.

## Scope

- **`hooks/__tests__/useUsersList.test.ts`** — List hook: success/empty/error load, parameter passing (`role`, `isActive`, `query`, `page`, `pageSize`), no call when `enabled: false`.
- **`app/(protected)/users/__tests__/page.test.tsx`** — Users page: list, filters, create/edit, deactivate/reactivate, reset password, permission-based button visibility.

## Run

```bash
npm run test
# or watch
npm run test:watch
```

## Mocks

- **usersGateway:** `getUsersList`, `createUser`, `updateUser`, `deactivateUser`, `reactivateUser`, `resetPassword`, `createRole`, `normalizeError` — real endpoint shapes (`UserInfo`, `UsersListResponse`).
- **useAuth:** `{ user: { id, role: 'Admin' } }`
- **useUsersPolicy:** Default full permission; permission test overrides `canCreate: false`.
- **UserFormDrawer / UserDetailDrawer:** Simple stub (form submit and close).

## Behavior-focused scenarios

| Scenario | Expected behavior |
|----------|-------------------|
| List load success | Users, email/role visible in the table |
| List empty | "Keine Benutzer gefunden." |
| List error | Error text + "Erneut versuchen" button |
| Filter default | First call `page: 1`, `pageSize: 20`, `isActive: true` |
| Search | Repeat call with `query` after search submit |
| Create success | `createUser` called, "Benutzer angelegt." |
| Create error | `message.error` called |
| Edit submit | `updateUser(id, data)` called |
| Deactivate | Modal opens, reason entered, `deactivateUser(id, { reason })` |
| Reactivate | Modal opens, confirm `reactivateUser(id, undefined)` |
| Reset password too short | Modal stays open, `resetPassword` not called |
| Reset password valid | `resetPassword(id, { newPassword })` + success message |
| canCreate false | No "Benutzer anlegen" button |

## Flakiness prevention

- `retry: false` (QueryClient) disables network retries.
- `testTimeout: 15000` (vitest.config) reduces timeouts in slow environments.
- Button selectors use regex for Ant Design icon+text combined names (for example `/Bearbeiten/`).
