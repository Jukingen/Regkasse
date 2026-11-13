import { describe, expect, it } from '@jest/globals';

import {
  canEncodeQrPayload,
  getRksvQrVersion,
  resolveRksvQrEcl,
  RKSV_QR_FALLBACK_ECL,
  RKSV_QR_LARGE_PAYLOAD_CHARS,
  RKSV_QR_MAX_UTF8_BYTES,
  RKSV_QR_PREFERRED_ECL,
  utf8ByteLength,
} from '../utils/rksvQrEncode';

/** Compact-JWS-like RKSV machine code (typical POS `tse.qrPayload`). */
function buildRksvMachineCode(bodyLen: number): string {
  const body = 'K1_R-0001_2026-07-21T12:00:00_0.00_0.00_0.00_12.50_0.00_CERT'.padEnd(
    Math.max(bodyLen, 20),
    'X'
  );
  const jws = `eyJhbGciOiJFUzI1NiJ9.${'a'.repeat(80)}.${'b'.repeat(80)}`;
  return `_R1-AT1_${body}_${jws}`;
}

/** Backend-style Gutschein mini payload with optional `|G:` gross. */
function buildGutscheinMiniPayload(gross = '12.50'): string {
  return `_R1-MINI|K:REG01|R:42|D:2026-07-21T12:00:00|A:${gross}|S:abcd1234..wxyz9876|G:${gross}`;
}

describe('rksvQrEncode', () => {
  it('prefers ECC M for short payloads', () => {
    const short = '_R1-AT1_short_payload';
    expect(short.length).toBeLessThan(RKSV_QR_LARGE_PAYLOAD_CHARS);
    expect(resolveRksvQrEcl(short)).toBe(RKSV_QR_PREFERRED_ECL);
    expect(canEncodeQrPayload(short, RKSV_QR_PREFERRED_ECL)).toBe(true);
  });

  it('prefers ECC L for long RKSV machine codes and auto-selects a version', () => {
    const long = buildRksvMachineCode(500);
    expect(long.length).toBeGreaterThanOrEqual(RKSV_QR_LARGE_PAYLOAD_CHARS);
    expect(resolveRksvQrEcl(long)).toBe(RKSV_QR_FALLBACK_ECL);
    const version = getRksvQrVersion(long);
    expect(version).not.toBeNull();
    expect(version!).toBeGreaterThanOrEqual(1);
    expect(version!).toBeLessThanOrEqual(40);
  });

  it('encodes Gutschein mini payloads (including |G: voucher gross)', () => {
    const gutschein = buildGutscheinMiniPayload('25.00');
    expect(gutschein).toContain('|G:25.00');
    expect(resolveRksvQrEcl(gutschein)).not.toBeNull();
    expect(canEncodeQrPayload(gutschein, RKSV_QR_PREFERRED_ECL)).toBe(true);
  });

  it('returns null when UTF-8 payload exceeds the soft RKSV byte budget', () => {
    const oversized = 'Ö'.repeat(RKSV_QR_MAX_UTF8_BYTES); // 2 bytes each in UTF-8
    expect(utf8ByteLength(oversized)).toBeGreaterThan(RKSV_QR_MAX_UTF8_BYTES);
    expect(resolveRksvQrEcl(oversized)).toBeNull();
  });

  it('trims whitespace before encoding checks', () => {
    expect(resolveRksvQrEcl('   ')).toBeNull();
    expect(resolveRksvQrEcl('  _R1-AT1_ok  ')).toBe(RKSV_QR_PREFERRED_ECL);
  });

  it('falls back to ECC L when preferred M cannot encode', () => {
    // ~500 ASCII chars: short enough for L/M capacity, long enough to prefer L path first.
    const mediumLong = `_${'A'.repeat(500)}`;
    expect(mediumLong.length).toBeGreaterThanOrEqual(RKSV_QR_LARGE_PAYLOAD_CHARS);
    expect(utf8ByteLength(mediumLong)).toBeLessThanOrEqual(RKSV_QR_MAX_UTF8_BYTES);
    const ecl = resolveRksvQrEcl(mediumLong);
    expect(ecl).toBe(RKSV_QR_FALLBACK_ECL);
    expect(canEncodeQrPayload(mediumLong, ecl!)).toBe(true);
  });
});
