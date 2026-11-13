import { DENSITY_STYLES } from './density';
import { resolveEffectiveTheme } from './theme';
import type { DensityMode, ResolvedTheme, ThemeMode } from './types';

export function applyDocumentTheme(mode: ThemeMode, effective?: ResolvedTheme): ResolvedTheme {
  if (typeof document === 'undefined') return effective ?? 'light';
  const resolved = effective ?? resolveEffectiveTheme(mode);
  document.documentElement.setAttribute('data-theme', resolved);
  return resolved;
}

export function applyDocumentDensity(density: DensityMode): void {
  if (typeof document === 'undefined') return;
  document.documentElement.setAttribute('data-density', density);
  const tokens = DENSITY_STYLES[density];
  const root = document.documentElement.style;
  root.setProperty('--admin-density-padding', tokens.padding);
  root.setProperty('--admin-density-font-size', tokens.fontSize);
  root.setProperty('--admin-density-line-height', tokens.lineHeight);
}

export function applyReducedAnimations(reduced: boolean): void {
  if (typeof document === 'undefined') return;
  if (reduced) {
    document.documentElement.setAttribute('data-reduced-motion', 'true');
  } else {
    document.documentElement.removeAttribute('data-reduced-motion');
  }
}
