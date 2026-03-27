# Localization Guide (frontend + frontend-admin)

This guide harmonizes localization practices between `frontend` and `frontend-admin` without requiring a shared package.

## Scope and current baseline

- Default source language is `de`.
- Supported languages are `de`, `en`, `tr`.
- `frontend` uses `i18next` + `react-i18next`.
- `frontend-admin` uses a custom provider (`useI18n`) with catalog loading from JSON.
- Both apps store locale resources under app-local `i18n/locales/{de,en,tr}/`.

Implementation can differ per app, but rules below should be shared.

---

## 1) Common translation key naming rules

### Key syntax

- Use `namespace.path.to.leaf`.
- Use `snake_case` for new key segments.
- Keep legacy camelCase keys working; do not mass-rename existing keys in one step.

### Leaf naming

- Prefer semantic names over UI-position names.
- Good: `settings.form.tax_number.label`
- Avoid: `settings.left_panel.input1_label`

### Reserved patterns

- `*.label`, `*.placeholder`, `*.hint`, `*.description`
- `*.title`, `*.subtitle`
- `*.success`, `*.error`, `*.warning`
- `*.empty`, `*.loading`
- `*.confirm_title`, `*.confirm_body`

---

## 2) Common namespace philosophy

### Shared conceptual namespaces (both apps)

- `common`: generic reusable UI terms (save/cancel/close/loading/yes/no)
- `auth`: login/session/logout text
- `navigation` or `nav`: navigation labels and accessibility text
- `settings`: settings pages/forms
- `errors`: reusable error taxonomy labels and generic messages
- `tables`: reusable table/filter/pagination/empty states
- `modals`: reusable confirm/info modal copy

### Feature/domain namespaces

- Keep feature/domain text inside feature namespaces:
  - POS app examples: `checkout`, `orders`, `payment`, `invoices`, `reports`, `products`
  - Admin app examples: `users`, `products`, `settings`, `benefits`, `receipt_templates`

### Boundary rule

- If text is reused in 3+ unrelated screens, move to a shared namespace.
- Otherwise keep it in feature namespace for clarity.

---

## 3) Interpolation, pluralization, formatting, fallback rules

### Interpolation

- Use named tokens: `{{count}}`, `{{status}}`, `{{details}}`.
- Do not concatenate translated fragments in code.
- Translate full sentences when variables are involved.

Example:

- Good: `t('navigation.offline_queue.sync_summary_partial', { processed, failed })`
- Avoid: ```${processed} ${t('...')} ${failed}```

### Pluralization

- For `frontend` (`i18next`): use i18next plural forms (`key_one`, `key_other`) where practical.
- For `frontend-admin` custom provider: keep explicit count-aware messages for now (e.g., `items_one`, `items_other` or a dedicated key per form) until plural engine is introduced.
- Do not mix raw singular/plural branching ad-hoc across screens.

### Date/time/number/currency formatting

- Never hardcode locale formatting in UI strings.
- Use app formatting helpers and `TEXT_TO_FORMAT_LOCALE` mapping.
- Keep text locale (`de|en|tr`) separate from format locale (`de-AT|en-US|tr-TR`).

### Fallback handling

- Fallback language: `de` (catalog merge in `I18nProvider`).
- **frontend-admin** missing key behavior (see `frontend-admin/src/i18n/I18nProvider.tsx`):
  - Resolution order: active locale → German catalog → fixed user-facing placeholder (not the raw key string in production).
  - **Development:** `console.warn` for missing keys (deduped); **production:** no console warning for the same path.
- Avoid `t('key', 'inline fallback')` in new code except temporary migration patches.

---

## 4) Validation messages and error taxonomy text

### Validation message shape

- Put field-level validation under feature:
  - `feature.validation.field.required`
  - `feature.validation.field.invalid`
  - `feature.validation.field.min`
  - `feature.validation.field.max`

### Error taxonomy

- Reusable global errors under `errors` namespace:
  - `errors.network.timeout`
  - `errors.network.offline`
  - `errors.auth.invalid_credentials`
  - `errors.auth.session_expired`
  - `errors.server.unexpected`
- Feature-specific errors remain in feature namespace when context-specific.

### API error display

- User-facing banners/toasts should use translated wrappers.
- Raw backend error details may be appended only if safe and useful.

---

## 5) Reuse vs create-new decision rules

Reuse an existing key when:

- Same meaning, tone, and context.
- Same grammatical role (button label vs page heading is not always reusable).

Create a new key when:

- Semantics differ even if words look similar.
- Context requires different tone (formal/informal, confirmation vs warning).
- Variable structure differs (different interpolation tokens).

Avoid over-reusing generic keys for domain terms.

---

## 6) Feature-based organization rules

For each feature namespace, prefer:

- `page.*` (title, subtitle, empty, loading)
- `form.*` (labels/placeholders/help)
- `validation.*`
- `actions.*` (button/action labels)
- `messages.*` (toast/snackbar/banner text)
- `table.*` (headers/states)
- `modal.*` (confirm dialogs)

This keeps structure discoverable and predictable across both apps.

---

## 7) Migration checklist for new screens

Use this checklist before merging:

1. Add new keys in `de` first (source of truth).
2. Add matching key paths in `en` and `tr`.
3. Place keys in the correct namespace (shared vs feature).
4. Avoid inline fallback text in `t(...)` for final code.
5. Avoid string concatenation for user-facing text.
6. Include accessibility labels/hints in localization when user-visible.
7. Run localization validation scripts for target app:
   - `npm run i18n:validate`
8. Smoke-test language switch in UI.
9. Verify no hardcoded user-facing strings remain in touched files.

---

## App-specific implementation notes (allowed differences)

- `frontend`: continue using `useTranslation` + namespace-prefixed keys.
- `frontend-admin`: continue using `useI18n` and catalog mapping from `src/i18n/config.ts`.
- **Namespace naming (frontend-admin):** In code, `t('firstSegment.rest')` uses **camelCase** catalog keys (`adminShell`, `finanzOnlineOutbox`, …). On disk, almost all locale files use the same stem; the exception is **`admin-shell.json`** ↔ catalog key **`adminShell`**. The localization manifest lists **file stems** (e.g. `admin-shell`); do not rename files to “fix” drift without updating imports in `config.ts`.
- Do not block feature work waiting for a shared package.
- Harmonize at convention level first; extract shared package later only if maintenance cost justifies it.
