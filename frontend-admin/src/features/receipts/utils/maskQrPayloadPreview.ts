/**
 * Short on-screen preview for RKSV QR wire strings (full value is copied separately).
 */
export function maskQrPayloadPreview(payload: string, headChars = 40, tailChars = 24): string {
  const s = payload.trim();
  if (!s) return '';
  const minMask = headChars + tailChars + 1;
  if (s.length <= minMask) return s;
  return `${s.slice(0, headChars)}…${s.slice(-tailChars)}`;
}
