/**
 * Typography + spacing scales for FA.
 * Prefer Ant Design `theme.useToken()` (`token.fontSize`, `token.marginMD`, …)
 * in components; these constants document the seed values we pass into ConfigProvider.
 */

/** Base font size seeds — density may override via `buildAntdTheme`. */
export const typography = {
  fontFamily:
    "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, 'Noto Sans', sans-serif, 'Apple Color Emoji', 'Segoe UI Emoji'",
  /** Default / standard density */
  fontSize: 14,
  fontSizeSM: 12,
  fontSizeLG: 16,
  fontSizeXL: 20,
  fontSizeHeading1: 38,
  fontSizeHeading2: 30,
  fontSizeHeading3: 24,
  fontSizeHeading4: 20,
  fontSizeHeading5: 16,
  lineHeight: 1.5714285714285714,
  fontWeightStrong: 600,
} as const;

/**
 * Spacing scale (px) — mirrors Ant Design sizeMap roughly.
 * Use `token.marginXS`…`token.marginXXL` / `token.padding*` at runtime.
 */
export const spacing = {
  xxs: 4,
  xs: 8,
  sm: 12,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
} as const;

export const radii = {
  sm: 4,
  /** Default control / card radius */
  md: 6,
  lg: 8,
} as const;

export const zIndex = {
  popupBase: 1050,
  modal: 1100,
} as const;

/** Control heights (middle size); compact/comfortable via ConfigProvider `componentSize`. */
export const controlHeight = {
  sm: 24,
  md: 32,
  lg: 40,
} as const;
