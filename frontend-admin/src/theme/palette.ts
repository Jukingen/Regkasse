/**
 * Canonical Regkasse Admin color seeds.
 * Ant Design derives full palettes from these via algorithm (light/dark).
 * Prefer `theme.useToken()` at runtime; import from here only for non-React
 * contexts (charts, PDF export CSS, mapper style objects).
 */
export const palette = {
  /** Brand / interactive primary (Ant Design blue-6). */
  primary: '#1677ff',
  success: '#52c41a',
  warning: '#faad14',
  error: '#ff4d4f',
  /** Alias of primary — informational accents. */
  info: '#1677ff',
  /** Chart / multi-series extras (not Ant seed tokens). */
  purple: '#722ed1',
  cyan: '#13c2c2',
  geekblue: '#2f54eb',
  magenta: '#eb2f96',
  gold: '#faad14',
  lime: '#a0d911',
} as const;

export type PaletteKey = keyof typeof palette;

/** Ordered series for charts (Recharts, etc.). */
export const chartSeriesColors: readonly string[] = [
  palette.primary,
  palette.success,
  palette.warning,
  palette.error,
  palette.purple,
  palette.cyan,
];

/**
 * Light/dark layout surfaces that Ant algorithm does not fully own in our shell.
 * Kept in sync with `styles/theme-tokens.css` (`--bg-*`).
 */
export const surface = {
  light: {
    colorBgLayout: '#f5f5f5',
    colorBgContainer: '#ffffff',
    colorBgElevated: '#ffffff',
    colorBgSpotlight: '#fafafa',
    siderBg: '#ffffff',
    headerBg: '#ffffff',
    /** Auth / gate full-viewport wash (classic Ant layout gray). */
    colorBgAuth: '#f0f2f5',
  },
  dark: {
    colorBgLayout: '#000000',
    colorBgContainer: '#141414',
    colorBgElevated: '#1f1f1f',
    colorBgSpotlight: '#1f1f1f',
    siderBg: '#141414',
    headerBg: '#141414',
    colorBgAuth: '#000000',
  },
} as const;

export type SurfaceMode = keyof typeof surface;
