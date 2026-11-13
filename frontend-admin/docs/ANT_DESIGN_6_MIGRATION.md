# Ant Design 6 Migration Guide

Reference for Frontend Admin (`frontend-admin`) on Ant Design **6.x**.  
Also summarized in root [`AGENTS.md`](../../AGENTS.md) (FA conventions).

Upstream: [Ant Design v5 → v6 migration](https://ant.design/docs/react/migration-v6).

## Do not blind-replace

A global search/replace of `width={` → `size={` or `message={` → `title={` **will break** unrelated APIs:

| Keep as-is | Why |
|------------|-----|
| `Modal` `width={…}` | Still valid (not Drawer `size`) |
| `Form` / `ConfirmDialog` / `Popconfirm` `message=` | Not Alert `title` |
| `Collapse` `bordered={false}` | Still valid (not Card/Tag `variant`) |
| Divider `titlePlacement` | **Correct** Ant Design 6 API (do not rename to `placement`) |
| Alert `banner` | Still valid; banner mode defaults `showIcon` to `true` |

Use the component-aware script instead:

```bash
# From repo root
node scripts/fix-antd-deprecations.mjs --dry-run
node scripts/fix-antd-deprecations.mjs
```

## ESLint (catch regressions)

FA uses **ESLint flat config** (`eslint.config.mjs`), not `.eslintrc.json`.

There is **no** built-in `no-restricted-props` rule. Deprecated Ant Design JSX props are enforced with `no-restricted-syntax` AST selectors (Drawer `width`, Alert `message`, Card/Tag `bordered`, `destroyOnClose`, `dropdownRender`, Space `direction`, Modal/Drawer `maskClosable`).

```bash
cd frontend-admin && npm run lint
```

Notes:

- Selectors match **element names** (`<Drawer>`, `<Alert>`). Renamed aliases (`const D = Drawer; <D width={…} />`) are not covered.
- Modal `width` and Collapse `bordered` are **not** banned.
- `eslint-plugin-deprecation` is optional type-aware noise (`@deprecated` in `.d.ts`); FA prefers the explicit JSX selectors above.

## Breaking / deprecated props

### 1. Drawer

| Old | New |
|-----|-----|
| `width={400}` / `height={…}` | `size={400}` (number) or `size="default"` / `size="large"` |
| `destroyOnClose` | `destroyOnHidden` |
| `maskClosable` | `mask={{ closable: … }}` |
| `bodyStyle` / `headerStyle` / … | `styles.body` / `styles.header` / … |

```tsx
// ❌
<Drawer width={420} destroyOnClose open={open} onClose={onClose} />

// ✅
<Drawer size={420} destroyOnHidden open={open} onClose={onClose} />
```

### 2. Alert

| Old | New |
|-----|-----|
| `message={…}` | `title={…}` |
| `description` | unchanged |

`banner` remains supported. Prefer explicit `showIcon` when you want an icon outside banner mode.

```tsx
// ❌
<Alert type="info" showIcon message={t('…')} />

// ✅
<Alert type="info" showIcon title={t('…')} />
```

### 3. Card

| Old | New |
|-----|-----|
| `bordered={false}` | `variant="borderless"` |
| `bordered` / `bordered={true}` | `variant="outlined"` (default) |
| `bodyStyle` / `headStyle` | `styles.body` / `styles.header` |

### 4. Tag

| Old | New |
|-----|-----|
| `bordered={false}` | `variant="filled"` |

(Per project convention in `AGENTS.md`; prefer `variant` over deprecated `bordered`.)

### 5. Modal

| Old | New |
|-----|-----|
| `destroyOnClose` | `destroyOnHidden` |
| `maskClosable={false}` | `mask={{ closable: false }}` |
| `maskClosable={!loading}` | `mask={{ closable: !loading }}` |

`width` on **Modal** stays valid. Do not convert Modal `width` to `size`.

```tsx
// ❌
<Modal destroyOnClose maskClosable={!loading} width={600} />

// ✅
<Modal destroyOnHidden mask={{ closable: !loading }} width={600} />
```

### 6. Dropdown & Select

| Old | New |
|-----|-----|
| `dropdownRender` | `popupRender` |
| `onDropdownVisibleChange` / related | prefer `onOpenChange` / popup APIs |

### 7. Divider

| Prefer (Ant Design 6) | Notes |
|------------------------|--------|
| `titlePlacement="start" \| "end" \| "center"` | Title position inside the divider |
| `orientation="horizontal" \| "vertical"` | Line axis (replaces deprecated Divider `type` in older APIs) |

**Do not** rename `titlePlacement` → `placement`. That mapping is incorrect for Divider.

```tsx
// ✅ Current FA pattern
<Divider titlePlacement="left" plain>
  {t('…')}
</Divider>
```

(`left`/`right` may still appear in older call sites; prefer `start`/`end`/`center` when touching code.)

### 8. Space (also migrated in FA)

| Old | New |
|-----|-----|
| `direction="vertical"` | `orientation="vertical"` |
| `direction="horizontal"` | `orientation="horizontal"` |

### 9. Tabs / Collapse (related)

| Old | New |
|-----|-----|
| Tabs `destroyInactiveTabPane` | `destroyOnHidden` |
| Tabs `tabPosition` | `tabPlacement` |
| Collapse `destroyInactivePanel` | `destroyOnHidden` |
| Collapse `expandIconPosition` | `expandIconPlacement` |

Collapse `bordered={false}` is **not** deprecated — leave it.

## Feedback APIs (not JSX props)

Static Ant Design APIs do not receive theme context. FA rule:

| Avoid | Use |
|-------|-----|
| `import { message, notification } from 'antd'` | `useNotify()` / `NotificationService` |
| `Modal.confirm(…)` static | `useAntdApp().modal` |

See `AGENTS.md` → “Ant Design 6 — batch fix pattern”.

## Quick reference

| Component | Old | New |
|-----------|-----|-----|
| Drawer | `width={400}` | `size={400}` |
| Drawer | `destroyOnClose` | `destroyOnHidden` |
| Drawer / Modal | `maskClosable={…}` | `mask={{ closable: … }}` |
| Alert | `message="…"` | `title="…"` |
| Card | `bordered={false}` | `variant="borderless"` |
| Tag | `bordered={false}` | `variant="filled"` |
| Modal | `destroyOnClose` | `destroyOnHidden` |
| Dropdown | `dropdownRender` | `popupRender` |
| Select | `dropdownRender` | `popupRender` |
| Space | `direction="vertical"` | `orientation="vertical"` |
| Divider | (legacy title position APIs) | **`titlePlacement`** (keep / prefer) |

## Scan checklist

From repo root (PowerShell-friendly):

```bash
# Drawer width (should be empty)
rg -U --glob '*.tsx' '<Drawer[\s\S]{0,500}?\bwidth=' frontend-admin/src

# Alert message (should be empty)
rg -U --glob '*.tsx' '<Alert[\s\S]{0,800}?\bmessage=' frontend-admin/src

# Card / Tag bordered={false}
rg -U --glob '*.tsx' '<Card[\s\S]{0,300}?\bbordered=\{false\}' frontend-admin/src
rg -U --glob '*.tsx' '<Tag[\s\S]{0,300}?\bbordered=\{false\}' frontend-admin/src

# Shared renames
rg --glob '*.tsx' 'destroyOnClose|dropdownRender' frontend-admin/src
rg -U --glob '*.tsx' '<(Space|Space\.Compact)[\s\S]{0,200}?\bdirection=' frontend-admin/src
rg -U --glob '*.tsx' '<(Modal|Drawer)[\s\S]{0,600}?\bmaskClosable' frontend-admin/src
```

Or re-run:

```bash
node scripts/fix-antd-deprecations.mjs --dry-run
```

## Migration status (FA, 2026-07)

Batch work (Drawer `width`→`size`, Alert `message`→`title`, Space `direction`→`orientation`, Modal `maskClosable`→`mask.closable`) plus prior `destroyOnHidden` / `popupRender` usage.

- [x] Drawer `width` → `size` (all FA Drawers, including `FeedbackWidget.tsx`)
- [x] Alert `message` → `title` (all FA Alerts, including TSE `auto-healing` and related pages)
- [x] Space `direction` → `orientation`
- [x] Modal `maskClosable` → `mask={{ closable: … }}`
- [x] `destroyOnClose` — no remaining FA usages (prefer `destroyOnHidden`)
- [x] `dropdownRender` — no remaining FA usages (prefer `popupRender`)
- [x] Card `bordered={false}` — no FA usages found (use `variant="borderless"` if added)
- [x] Tag `bordered={false}` — no FA usages found (use `variant="filled"` if added)
- [x] Divider — already on `titlePlacement` (do not “fix” to `placement`)
- [ ] Optional follow-ups when touching files: Modal/Drawer `bodyStyle`→`styles.*`, Tabs `tabPosition`→`tabPlacement`, Divider `left`/`right` → `start`/`end`

## Related

- Safe migrator: [`scripts/fix-antd-deprecations.mjs`](../../scripts/fix-antd-deprecations.mjs)
- Project rules: [`AGENTS.md`](../../AGENTS.md) (Ant Design 6 + FA toast/modal patterns)
- Official guide: https://ant.design/docs/react/migration-v6
