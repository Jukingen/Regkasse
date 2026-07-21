export function maskTenantLicenseKey(key: string | null | undefined): string {
  if (!key?.trim()) return '—';
  const trimmed = key.trim();
  if (trimmed.length <= 12) return trimmed;
  return `${trimmed.slice(0, 8)}…${trimmed.slice(-4)}`;
}
