/**
 * RKSV / Gutschein QR encode helpers for POS display.
 *
 * Backend (`QrImageService`) prefers ECC M then L with version 10–20 sweep.
 * Client encoding uses the `qrcode` library (auto version) with the same ECC order.
 * Exact payload is encoded as-is — compaction / voucher `|G:` mini payloads stay server-side.
 */
import QRCodeImport from 'qrcode';

export type RksvQrEcl = 'L' | 'M' | 'Q' | 'H';

type QrcodeCreateApi = {
  create: (text: string, options?: { errorCorrectionLevel?: RksvQrEcl }) => { version: number };
};

/** Resolve CJS/ESM interop for `qrcode` (Jest + Metro). */
function getQrcodeApi(): QrcodeCreateApi {
  const mod = QRCodeImport as unknown as QrcodeCreateApi & { default?: QrcodeCreateApi };
  if (typeof mod?.create === 'function') {
    return mod;
  }
  if (typeof mod?.default?.create === 'function') {
    return mod.default;
  }
  // Fallback for environments that only expose the CJS export via require.

  return require('qrcode') as QrcodeCreateApi;
}

const QRCode = getQrcodeApi();

/** Prefer medium ECC when the payload fits (matches backend primary). */
export const RKSV_QR_PREFERRED_ECL: RksvQrEcl = 'M';

/** Fallback for long RKSV machine codes (higher capacity, lower redundancy). */
export const RKSV_QR_FALLBACK_ECL: RksvQrEcl = 'L';

/**
 * Long RKSV `_R1-AT…` strings (compact JWS) often exceed ~400 chars.
 * Prefer L early so auto version selection stays within scannable module sizes on screen.
 */
export const RKSV_QR_LARGE_PAYLOAD_CHARS = 400;

/**
 * Soft UTF-8 byte budget aligned with backend `PngPayloadMaxUtf8Bytes` (2200).
 * Above this, client generation is unlikely to succeed — rely on server PNG.
 */
export const RKSV_QR_MAX_UTF8_BYTES = 2200;

export function utf8ByteLength(text: string): number {
  if (typeof TextEncoder !== 'undefined') {
    return new TextEncoder().encode(text).length;
  }
  // Jest / older RN without TextEncoder
  return unescape(encodeURIComponent(text)).length;
}

export function canEncodeQrPayload(payload: string, ecl: RksvQrEcl): boolean {
  const text = payload.trim();
  if (!text) return false;
  try {
    QRCode.create(text, { errorCorrectionLevel: ecl });
    return true;
  } catch {
    return false;
  }
}

/**
 * Pick ECC for on-screen QR. Returns null when the payload cannot be encoded
 * (caller should fall back to server PNG / unavailable UI).
 */
export function resolveRksvQrEcl(payload: string): RksvQrEcl | null {
  const text = payload.trim();
  if (!text) return null;

  if (utf8ByteLength(text) > RKSV_QR_MAX_UTF8_BYTES) {
    return null;
  }

  const preferred: RksvQrEcl =
    text.length >= RKSV_QR_LARGE_PAYLOAD_CHARS ? RKSV_QR_FALLBACK_ECL : RKSV_QR_PREFERRED_ECL;

  if (canEncodeQrPayload(text, preferred)) {
    return preferred;
  }

  if (preferred !== RKSV_QR_FALLBACK_ECL && canEncodeQrPayload(text, RKSV_QR_FALLBACK_ECL)) {
    return RKSV_QR_FALLBACK_ECL;
  }

  return null;
}

/** Auto-selected QR version for diagnostics / tests (null if encode fails). */
export function getRksvQrVersion(payload: string, ecl?: RksvQrEcl): number | null {
  const text = payload.trim();
  if (!text) return null;
  const level = ecl ?? resolveRksvQrEcl(text);
  if (!level) return null;
  try {
    const created = QRCode.create(text, { errorCorrectionLevel: level });
    return created.version;
  } catch {
    return null;
  }
}
