/** Default POS category icon when FA has not set one. */
export const DEFAULT_CATEGORY_ICON = '📦';

const HEX_COLOR = /^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/;

const LEGACY_IONICON_TO_EMOJI: Record<string, string> = {
  wine: '🍷',
  restaurant: '🍽️',
  'ice-cream': '🍰',
  'fast-food': '🍔',
  cafe: '☕',
  folder: DEFAULT_CATEGORY_ICON,
  nutrition: '🥗',
  pizza: '🍕',
  grid: DEFAULT_CATEGORY_ICON,
};

export function resolveCategoryIcon(icon?: string | null): string {
  const trimmed = icon?.trim();
  if (!trimmed) return DEFAULT_CATEGORY_ICON;
  return LEGACY_IONICON_TO_EMOJI[trimmed] ?? trimmed;
}

export function isCategoryColor(color?: string | null): color is string {
  return !!color && HEX_COLOR.test(color.trim());
}

export function categoryColorTint(color: string, alpha = 0.18): string {
  const hex = expandHex(color.trim());
  if (!hex) return `rgba(0,0,0,${alpha})`;
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return `rgba(${r},${g},${b},${alpha})`;
}

/** Picks white or dark text so the label stays readable on a category color. */
export function textOnHex(background: string): '#FFFFFF' | '#3D3229' {
  const hex = expandHex(background.trim());
  if (!hex) return '#3D3229';
  const channel = (start: number) => parseInt(hex.slice(start, start + 2), 16);
  const linear = (value: number) => {
    const s = value / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  const luminance =
    0.2126 * linear(channel(1)) + 0.7152 * linear(channel(3)) + 0.0722 * linear(channel(5));
  const whiteContrast = 1.05 / (luminance + 0.05);
  const darkLuminance = 0.051;
  const darkContrast = (luminance + 0.05) / (darkLuminance + 0.05);
  return whiteContrast >= 4.5 && whiteContrast >= darkContrast ? '#FFFFFF' : '#3D3229';
}

function expandHex(color: string): string | null {
  if (!HEX_COLOR.test(color)) return null;
  if (color.length === 4) {
    return `#${color[1]}${color[1]}${color[2]}${color[2]}${color[3]}${color[3]}`;
  }
  return color;
}
