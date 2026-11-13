import dayjs, { type Dayjs } from 'dayjs';
import utc from 'dayjs/plugin/utc';

import {
  getLicenseStatusDayText,
  getLicenseStatusLabel,
  getLicenseStatusMessage,
  getLicenseStatusTagColor,
  resolveTenantRowLicenseStatus,
} from '@/features/license/utils/licenseStatus';

dayjs.extend(utc);

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

export type TenantLicenseBadgeDisplay = {
  label: string;
  color: string;
  tooltip: string;
};

export function resolveTenantLicenseBadgeDisplay(
  licenseValidUntilUtc: string | null | undefined,
  licenseKey: string | null | undefined,
  t: TranslateFn,
  now: Dayjs = dayjs.utc(),
  licenseDaysRemaining?: number | null
): TenantLicenseBadgeDisplay {
  const status = resolveTenantRowLicenseStatus(
    {
      licenseValidUntilUtc,
      licenseKey,
      licenseDaysRemaining,
    },
    now.valueOf()
  );
  const dayText = getLicenseStatusDayText(status, t);
  const message = getLicenseStatusMessage(status, 'tenant', t);

  return {
    label: getLicenseStatusLabel(status.kind, t),
    color: getLicenseStatusTagColor(status.kind),
    tooltip: dayText ? `${message} ${dayText}` : message,
  };
}
