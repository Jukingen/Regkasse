type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

export type LicensePreviewPlanKey = 'annual' | 'quarterly' | 'monthly' | 'custom';

const PLAN_KEY_SUFFIX: Record<LicensePreviewPlanKey, string> = {
  annual: 'Annual',
  quarterly: 'Quarterly',
  monthly: 'Monthly',
  custom: 'Custom',
};

export function inferLicensePreviewPlanKey(
  durationDays: number | null | undefined
): LicensePreviewPlanKey {
  if (durationDays == null) return 'custom';
  if (durationDays >= 360 && durationDays <= 370) return 'annual';
  if (durationDays >= 85 && durationDays <= 95) return 'quarterly';
  if (durationDays >= 28 && durationDays <= 31) return 'monthly';
  return 'custom';
}

export function formatLicensePreviewPlanName(
  durationDays: number | null | undefined,
  t: TranslateFn
): string {
  const planKey = inferLicensePreviewPlanKey(durationDays);
  return t(`license.extendModal.previewPlan${PLAN_KEY_SUFFIX[planKey]}`);
}

export function formatLicensePreviewDuration(
  durationDays: number | null | undefined,
  t: TranslateFn
): string {
  if (durationDays == null) return '—';
  const planKey = inferLicensePreviewPlanKey(durationDays);
  if (planKey === 'annual') return t('license.extendModal.previewDurationAnnual');
  if (planKey === 'quarterly') return t('license.extendModal.previewDurationQuarterly');
  if (planKey === 'monthly') return t('license.extendModal.previewDurationMonthly');
  return t('license.extendModal.previewDurationDays', { days: durationDays });
}

/** e.g. "365 Tage (1 Jahr)" */
export function formatLicensePreviewDurationCombined(
  durationDays: number | null | undefined,
  t: TranslateFn
): string {
  if (durationDays == null) return '—';
  const planKey = inferLicensePreviewPlanKey(durationDays);
  if (planKey === 'custom') {
    return t('license.extendModal.previewDurationDays', { days: durationDays });
  }
  return t('license.extendModal.previewDurationCombined', {
    days: durationDays,
    period: formatLicensePreviewDuration(durationDays, t),
  });
}

export function mapPreviewErrorMessage(
  errorCode: string | null | undefined,
  fallback: string | null | undefined,
  t: TranslateFn
): string {
  switch (errorCode) {
    case 'invalid_key':
    case 'not_found':
      return t('license.extendModal.previewError');
    case 'expired':
      return t('license.extendModal.previewErrorExpired');
    case 'wrong_tenant':
      return t('license.extendModal.previewErrorWrongTenant');
    default:
      return fallback?.trim() ? fallback : t('license.extendModal.error');
  }
}

export function getPreviewStatusLabel(status: string | null | undefined, t: TranslateFn): string {
  switch (status) {
    case 'valid':
      return t('license.extendModal.previewStatusValid');
    case 'expired':
      return t('license.extendModal.previewStatusExpired');
    default:
      return t('license.extendModal.previewStatusInvalid');
  }
}

export function getPreviewStatusColor(status: string | null | undefined): string {
  switch (status) {
    case 'valid':
      return 'green';
    case 'expired':
      return 'orange';
    default:
      return 'red';
  }
}
