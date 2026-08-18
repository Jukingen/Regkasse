import type { LicenseLayerPublicStatusDto } from '@/api/manual/adminLicense';

export function isLicenseLayerActive(layer?: LicenseLayerPublicStatusDto | null): boolean {
  if (!layer) return false;
  if (layer.isActive === true) return true;
  const status = layer.status?.trim().toLowerCase();
  return status === 'active' || status === 'grace';
}

export function isSystemActiveTenantLocked(args: {
  systemLicense?: LicenseLayerPublicStatusDto | null;
  tenantLicense?: LicenseLayerPublicStatusDto | null;
}): boolean {
  return isLicenseLayerActive(args.systemLicense) && !isLicenseLayerActive(args.tenantLicense);
}

export function resolveLicenseLayerLabelKey(
  layer?: LicenseLayerPublicStatusDto | null
): 'active' | 'grace' | 'locked' | 'expired' {
  const status = layer?.status?.trim().toLowerCase();
  if (status === 'grace' || (layer?.isActive && status === 'grace')) return 'grace';
  if (layer?.isActive === true || status === 'active') return 'active';
  if (status === 'locked') return 'locked';
  return 'expired';
}
