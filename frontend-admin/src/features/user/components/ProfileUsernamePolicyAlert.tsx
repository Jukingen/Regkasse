'use client';

import { Alert } from 'antd';

import type { UsernameChangePolicy } from '@/features/user/hooks/useUsernameChangePolicy';
import { formatDateTime, useI18n } from '@/i18n';

type ProfileUsernamePolicyAlertProps = {
  policy: UsernameChangePolicy | undefined;
  isLoading?: boolean;
};

export function ProfileUsernamePolicyAlert({ policy, isLoading }: ProfileUsernamePolicyAlertProps) {
  const { t, formatLocale } = useI18n();

  if (isLoading || !policy) {
    return null;
  }

  if (policy.restrictionsApply === false) {
    return (
      <Alert
        type="info"
        showIcon
        title={t('profile.username.policyTitle')}
        description={t('profile.username.policySuperAdminExempt')}
        style={{ marginBottom: 16 }}
      />
    );
  }

  const cooldownDays = policy.cooldownDays > 0 ? policy.cooldownDays : 7;
  const baseDescription = t('profile.username.policyDescription', { days: cooldownDays });

  if (!policy.canChange && policy.nextChangeAllowedAtUtc) {
    const nextChangeLabel = formatDateTime(policy.nextChangeAllowedAtUtc, formatLocale, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'UTC',
    });

    return (
      <Alert
        type="warning"
        showIcon
        title={t('profile.username.policyTitle')}
        description={
          <>
            <div>{baseDescription}</div>
            <div style={{ marginTop: 8 }}>
              {t('profile.username.policyCooldownActive', { date: nextChangeLabel })}
            </div>
          </>
        }
        style={{ marginBottom: 16 }}
      />
    );
  }

  const lastChangedLabel = policy.lastChangedAtUtc
    ? formatDateTime(policy.lastChangedAtUtc, formatLocale, {
        dateStyle: 'medium',
        timeStyle: 'short',
        timeZone: 'UTC',
      })
    : null;

  return (
    <Alert
      type="info"
      showIcon
      title={t('profile.username.policyTitle')}
      description={
        <>
          <div>{baseDescription}</div>
          {lastChangedLabel ? (
            <div style={{ marginTop: 8 }}>
              {t('profile.username.policyLastChanged', { date: lastChangedLabel })}
            </div>
          ) : (
            <div style={{ marginTop: 8 }}>{t('profile.username.policyChangeAllowed')}</div>
          )}
        </>
      }
      style={{ marginBottom: 16 }}
    />
  );
}
