# Frontend Admin — Ant Design theme

Single source of truth for FA visual design. Runtime theme is applied via `ThemeProvider` → `ConfigProvider` (`src/lib/personalization/ThemeProvider.tsx`).

## File map

| File                | Role                                                   |
| ------------------- | ------------------------------------------------------ |
| `palette.ts`        | Color seeds + light/dark surfaces + chart series       |
| `tokens.ts`         | Typography, spacing, radius, z-index seeds             |
| `themeConfig.ts`    | Base Ant Design `ThemeConfig` (`token` + `components`) |
| `buildAntdTheme.ts` | Merges base + light/dark algorithm + density           |
| `statusColors.ts`   | Semantic status colors for non-React mappers           |
| `AuthPageFrame.tsx` | Shared auth / gate layout using `theme.useToken()`     |
| `index.ts`          | Public exports                                         |

CSS companions (document-level, not Ant token engine):

- `src/styles/theme-tokens.css` — `--bg-*` / `--text-*` for raw HTML surfaces
- `src/styles/personalization.css` — density + reduced-motion (table cell padding mirrors `buildAntdTheme` Table tokens)

`@ant-design/nextjs-registry` (`AntdRegistry` in `src/app/layout.tsx`) extracts Ant Design CSS-in-JS for App Router SSR. Do **not** introduce a parallel `StyleProvider` / hand-rolled registry, and do **not** introduce `createStyles` / `antd-style` that re-declare the same seeds.

## Color palette

| Token / export                     | Hex       | Usage                                      |
| ---------------------------------- | --------- | ------------------------------------------ |
| `palette.primary` / `colorPrimary` | `#1677ff` | Brand, links, primary Button, info accents |
| `palette.success` / `colorSuccess` | `#52c41a` | Success Tag/Alert/Statistic                |
| `palette.warning` / `colorWarning` | `#faad14` | Warnings, pending states                   |
| `palette.error` / `colorError`     | `#ff4d4f` | Errors, destructive                        |
| `palette.info`                     | `#1677ff` | Informational (= primary)                  |
| `palette.purple` / `cyan`          | extras    | Charts only (`chartSeriesColors`)          |

**Surfaces** (`surface.light` / `surface.dark`):

| Key                    | Light     | Dark      | Maps to                    |
| ---------------------- | --------- | --------- | -------------------------- |
| `colorBgLayout`        | `#f5f5f5` | `#000000` | Layout body, auth frame    |
| `colorBgContainer`     | `#ffffff` | `#141414` | Cards, content             |
| `colorBgSpotlight`     | `#fafafa` | `#1f1f1f` | Nested panels, code blocks |
| `headerBg` / `siderBg` | `#ffffff` | `#141414` | Shell chrome               |

In React components prefer:

```tsx
const { token } = theme.useToken();
// token.colorPrimary, token.colorBgLayout, token.colorTextSecondary, …
```

Outside React (mappers, Recharts):

```ts
import { chartSeriesColors, palette, statusValueStyle } from '@/theme';
```

## Typography scale

Seeded in `tokens.typography` / `themeConfig.token`:

| Token        | Default (standard density) |
| ------------ | -------------------------- |
| `fontSize`   | 14                         |
| `fontSizeSM` | 12                         |
| `fontSizeLG` | 16                         |
| `fontSizeXL` | 20                         |
| Headings 1–5 | 38 / 30 / 24 / 20 / 16     |

Density overrides `fontSize` via `antdFontSizeForDensity` (compact 12 / standard 14 / comfortable 16) and `ConfigProvider` `componentSize`.

## Spacing scale

| Export        | px  | Typical Ant token          |
| ------------- | --- | -------------------------- |
| `spacing.xxs` | 4   | `marginXXS` / `paddingXXS` |
| `spacing.xs`  | 8   | `marginXS`                 |
| `spacing.sm`  | 12  | `marginSM`                 |
| `spacing.md`  | 16  | `marginMD`                 |
| `spacing.lg`  | 24  | `marginLG`                 |
| `spacing.xl`  | 32  | `marginXL`                 |
| `spacing.xxl` | 48  | `marginXXL`                |

Page stacks: prefer `AdminPageShell` + `Space orientation="vertical" size="large"` over repeated `style={{ marginBottom: 16 }}`.

## Component overrides (`themeConfig.components`)

| Component            | Intent                                                                                |
| -------------------- | ------------------------------------------------------------------------------------- |
| **Layout**           | Shell bg (`bodyBg` / `headerBg` / `siderBg`) — dark/light swapped in `buildAntdTheme` |
| **Button**           | Transparent `default` for quiet toolbar icons; primary uses `colorPrimary`            |
| **Input / Select**   | Shared `borderRadius`                                                                 |
| **Card**             | `borderRadiusLG`; density adjusts `paddingLG`                                         |
| **Table**            | Radius + density `padding` / `paddingLG`                                              |
| **Form**             | `itemMarginBottom` scales with density                                                |
| **Modal / Dropdown** | z-index stack (modal above popup base)                                                |
| **Tag / Alert**      | Radius alignment                                                                      |

## Light / dark / system

1. User preference: `light` \| `dark` \| `system` (`ThemeMode`) — stored in Zustand `useUiPreferencesStore`
2. `resolveEffectiveTheme` → `ResolvedTheme`
3. `buildAntdTheme(resolved, density)` picks `darkAlgorithm` or `defaultAlgorithm` (+ optional `compactAlgorithm`)
4. `ThemeProvider` applies `document.documentElement[data-theme]` via `theme-tokens.css`
5. Header quick switch: `HeaderThemeQuickSwitch`; settings: `AppearanceSettings`

Preference state is **not** React Context (migrated to Zustand). `ThemeProvider` remains as the Ant Design `ConfigProvider` shell only.

## Do / don’t

**Do**

- Extend `palette.ts` / `themeConfig.ts` for global look changes
- Use `AuthPageFrame` / `AuthCard` for login-like pages
- Use `statusValueStyle('healthy' | …)` in backup-dr mappers

**Don’t**

- Hardcode `#1677ff` / `#fafafa` / `rgba(0,0,0,0.45)` in feature components
- Add a second cssinjs theme layer that duplicates seeds
- Change committed Ant `components` tokens for one-off layout (keep those local)

## Smoke-test checklist

- [ ] Toggle light → dark → system (OS) from header; Layout, Card, Table, Modal, Menu update
- [ ] Density compact / standard / comfortable: Table cell padding + Form item gaps
- [ ] `/login` and auth gate spinner use layout background (not stuck light gray in dark)
- [ ] Primary Button / links still brand blue in both modes
