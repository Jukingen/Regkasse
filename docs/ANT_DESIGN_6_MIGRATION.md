# Ant Design 6 migration (frontend-admin)

**Last updated:** 2026-09-16  
**Related:** [`../AGENTS.md`](../AGENTS.md) § Frontend-Admin (FA) Conventions · [`../frontend-admin/eslint.config.mjs`](../frontend-admin/eslint.config.mjs)

The admin panel runs Ant Design **6** (installed: `6.5.1`). A deprecated prop still compiles and still renders, so the deprecations are enforced by lint instead: `frontend-admin/eslint.config.mjs` declares component-scoped `no-restricted-syntax` rules at **error** severity, and each message links here.

This page lists only what that rule set enforces. Every replacement below was checked against the `@deprecated` annotations in the installed `antd` type definitions — it is not a general upgrade guide.

---

## 1. Enforced replacements

| Component | Deprecated | Use instead | Notes |
|-----------|-----------|-------------|-------|
| `Drawer` | `width` | `size` | `size?: sizeType \| number \| string`, so a pixel value carries over unchanged (`width={480}` → `size={480}`) |
| `Drawer` | `height` | `size` | Same prop for both axes; the open direction decides which one applies |
| `Drawer` / `Modal` | `maskClosable` | `mask={{ closable: … }}` | Mask options moved into the `mask` object |
| `Modal` / `Drawer` / `Tabs` | `destroyOnClose` | `destroyOnHidden` | Rename only |
| `Alert` | `message` | `title` | Both are `React.ReactNode`; `description` is unchanged |
| `Card` | `bordered` | `variant="borderless"` / `variant="outlined"` | `bordered={false}` → `variant="borderless"` |
| `Tag` | `bordered` | `variant="filled"` | `bordered={false}` → `variant="filled"` |
| `Dropdown` / `Select` | `dropdownRender` | `popupRender` | Rename only; `onDropdownVisibleChange` → `onOpenChange` is deprecated in the same family but is not lint-enforced yet |
| `Space` | `direction` | `orientation` | Same `Orientation` type, so `direction="vertical"` → `orientation="vertical"` |
| `Space.Compact` | `direction` | `orientation` | Same as `Space` |

`Divider` `titlePlacement` is listed in [`../AGENTS.md`](../AGENTS.md) but is not part of the lint rule set yet.

---

## 2. When lint blocks you

The rules are errors, so `npm run lint -w registrierkasse-admin` fails, and the pre-commit hook fails whenever a `frontend-admin/` file is staged. Apply the replacement from the table; none of them need a behavior decision.

Do not silence a rule with an inline `eslint-disable`. If a replacement genuinely does not fit a case, raise it in review instead — a disabled rule hides the same problem from everyone else.

---

## 3. Related conventions that lint does not cover

- **Feedback APIs:** never import static `message` / `notification` from `antd`, and never call `Modal.confirm` statically. Use `useNotify()` for toasts and `useAntdApp()` for `modal`. See [`../AGENTS.md`](../AGENTS.md) § Frontend-Admin (FA) Conventions.
- **Registry:** `<App>` and `AntdRegistry` wrap the tree in `src/app/layout.tsx`; component-level theme access depends on it.

---

## 4. Do not

- Do not upgrade or pin a different `antd` major in `frontend-admin` without updating this page and the lint rules together.
- Do not add a replacement to the table that is not backed by an `@deprecated` annotation in the installed typings or by an existing lint rule.
