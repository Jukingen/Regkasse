/** Matches backend <c>LicenseFeatureIds</c>. */

export const LICENSE_DEPLOYMENT_FEATURE = {
  PosFiscal: 'pos_fiscal',
  PosOffline: 'pos_offline',
  AdminBasic: 'admin_basic',
  AdminRksv: 'admin_rksv',
  AdminLicenseManage: 'admin_license_manage',
} as const;

export function deploymentLicenseAllows(
  features: readonly string[] | null | undefined,
  featureId: string
): boolean {
  if (!features || features.length === 0) return true;
  return features.includes(featureId);
}
