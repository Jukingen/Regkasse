/**
 * RKSV tutar metnini güvenli biçimde parse eder:
 * - de formatı: 11,84
 * - en formatı: 11.84
 * - negatif: -11,84 / -11.84
 * - boşluk içeren: " -1 234,56 "
 */
export function parseRksvAmount(raw: string): number | null {
  const compact = raw.replace(/\s+/g, '');
  if (!compact) return null;

  const signMatch = compact.match(/^[+-]?/);
  const sign = signMatch?.[0] ?? '';
  const unsigned = compact.slice(sign.length);
  if (!unsigned) return null;

  if (!/^[\d.,]+$/.test(unsigned)) return null;

  const lastComma = unsigned.lastIndexOf(',');
  const lastDot = unsigned.lastIndexOf('.');
  const decimalIndex = Math.max(lastComma, lastDot);

  let normalizedUnsigned: string;
  if (decimalIndex === -1) {
    normalizedUnsigned = unsigned.replace(/[.,]/g, '');
  } else {
    const integerPart = unsigned.slice(0, decimalIndex).replace(/[.,]/g, '');
    const fractionPart = unsigned.slice(decimalIndex + 1).replace(/[.,]/g, '');
    if (fractionPart.length === 0 || fractionPart.length > 2) return null;
    if (!integerPart && !fractionPart) return null;
    normalizedUnsigned = `${integerPart || '0'}.${fractionPart}`;
  }

  const normalized = `${sign}${normalizedUnsigned}`;
  if (!/^[+-]?\d+(\.\d+)?$/.test(normalized)) return null;

  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : null;
}
