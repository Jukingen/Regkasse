/**
 * POS breakpoints – layout and spacing decisions for responsive behavior.
 * Use with useWindowDimensions(); values are width in dp.
 */
export const Breakpoints = {
  /** Narrow phone (< 360dp) – reduce padding, ensure scroll */
  xs: 360,
  /** Small phone (< 480dp) */
  sm: 480,
  /** Medium / large phone (< 768dp) */
  md: 768,
  /** Tablet and up (>= 768dp) – ProductList uses 3 columns */
  lg: 768,
} as const;

export type BreakpointKey = keyof typeof Breakpoints;

/** Tab bar height (fixed in _layout) – use for footer padding so CTA stays above tab bar */
export const TAB_BAR_HEIGHT = 60;

/**
 * Returns breakpoint name for current width.
 * xs: width < 360, sm: < 480, md: < 768, lg: >= 768
 */
export function getBreakpoint(width: number): BreakpointKey {
  if (width < Breakpoints.xs) return 'xs';
  if (width < Breakpoints.sm) return 'sm';
  if (width < Breakpoints.md) return 'md';
  return 'lg';
}

/** Responsive padding (8px base): smaller on xs/sm so content doesn’t cramp */
export function getContentPaddingHorizontal(width: number): number {
  const key = getBreakpoint(width);
  if (key === 'xs') return 8;
  if (key === 'sm') return 12;
  return 16;
}
