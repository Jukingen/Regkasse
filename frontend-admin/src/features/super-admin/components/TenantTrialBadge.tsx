'use client';

import { Tag } from 'antd';
import React from 'react';

import { useI18n } from '@/i18n';

type Props = {
  trialStatus?: string | null;
  trialDaysRemaining?: number | null;
};

function colorForDays(days: number | null | undefined): string {
  if (days == null) return 'blue';
  if (days > 7) return 'success';
  if (days >= 3) return 'warning';
  return 'error';
}

/** Compact trial indicator for Super Admin tenant list. */
export function TenantTrialBadge({ trialStatus, trialDaysRemaining }: Props) {
  const { t } = useI18n();
  if (!trialStatus || (trialStatus !== 'active' && trialStatus !== 'expired')) {
    return null;
  }

  const daysLabel =
    trialDaysRemaining != null
      ? t('trials.badge.days', { count: trialDaysRemaining })
      : t('trials.badge.label');

  return (
    <Tag color={colorForDays(trialDaysRemaining)}>
      {t('trials.badge.label')} · {daysLabel}
    </Tag>
  );
}
