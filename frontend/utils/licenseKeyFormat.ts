/** Legacy display: REGK-XXXXX-XXXXX-XXXXX */
export const LICENSE_KEY_DISPLAY_PATTERN = /^REGK-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$/i;

/** Unified: REGK-yyyyMMdd-{slug}-{8} (system or tenant slug). */
export const LICENSE_KEY_UNIFIED_PATTERN =
  /^REGK-\d{8}-[A-Z0-9]+(?:-[A-Z0-9]+)*-[A-Z0-9]{8}$/i;

export const LICENSE_KEY_MAX_LENGTH = 100;

export function isValidPosLicenseKey(value: string | undefined | null): boolean {
  const key = (value ?? '').trim();
  if (!key) return false;
  return LICENSE_KEY_DISPLAY_PATTERN.test(key) || LICENSE_KEY_UNIFIED_PATTERN.test(key);
}

export function sanitizeLicenseKeyInput(raw: string): string {
  return raw
    .toUpperCase()
    .replace(/[^A-Z0-9-]/g, '')
    .slice(0, LICENSE_KEY_MAX_LENGTH);
}
