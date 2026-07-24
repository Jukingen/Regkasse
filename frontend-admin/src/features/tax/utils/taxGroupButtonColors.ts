/** Pick readable label color for a tax-group chip/button background. */
export function contrastingTextColor(hex: string | null | undefined): string {
  if (!hex || typeof hex !== 'string') return 'rgba(0,0,0,0.88)';
  const cleaned = hex.replace('#', '').trim();
  const full =
    cleaned.length === 3
      ? cleaned
          .split('')
          .map((c) => c + c)
          .join('')
      : cleaned;
  if (!/^[0-9a-fA-F]{6}$/.test(full)) return 'rgba(0,0,0,0.88)';
  const r = Number.parseInt(full.slice(0, 2), 16);
  const g = Number.parseInt(full.slice(2, 4), 16);
  const b = Number.parseInt(full.slice(4, 6), 16);
  // Relative luminance (sRGB approx)
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  return luminance > 0.55 ? 'rgba(0,0,0,0.88)' : '#fff';
}
