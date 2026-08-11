'use client';

import { Tag, Tooltip } from 'antd';
import type { ReactNode } from 'react';

import { DateColumn } from '@/components/DateColumn';
import {
  formatLicenseDaysRemainingLabel,
  formatLicenseValidityTooltip,
  licenseValidityHealthTagColor,
  resolveLicenseValidityHealth,
} from '@/features/billing/utils/billingFormatters';
import { useI18n } from '@/i18n';

type LicenseValidityCellProps = {
  validUntilUtc: string | null | undefined;
  /** `date` → Gültig bis; `days` → Kalan Gün / Days Remaining */
  mode: 'date' | 'days';
};

export function LicenseValidityCell({ validUntilUtc, mode }: LicenseValidityCellProps) {
  const { t } = useI18n();

  if (!validUntilUtc) {
    return <span>—</span>;
  }

  const health = resolveLicenseValidityHealth(validUntilUtc);
  const color = licenseValidityHealthTagColor(health);
  const tooltip = formatLicenseValidityTooltip(validUntilUtc, t);

  const content: ReactNode =
    mode === 'days' ? (
      formatLicenseDaysRemainingLabel(validUntilUtc, t)
    ) : (
      <DateColumn date={validUntilUtc} format="datetime" />
    );

  if (!color) {
    return <>{content}</>;
  }

  return (
    <Tooltip title={tooltip}>
      <Tag color={color} style={{ marginInlineEnd: 0, maxWidth: '100%' }}>
        {content}
      </Tag>
    </Tooltip>
  );
}
