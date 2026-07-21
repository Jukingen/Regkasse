'use client';

import { Alert } from 'antd';

import { useI18n } from '@/i18n';

type AccessDeniedProps = {
  /** Optional override; defaults to digital-services denial copy. */
  message?: string;
  title?: string;
};

/**
 * Page-level denial surface when the user lacks the required permission.
 */
export function AccessDenied({ message, title }: AccessDeniedProps) {
  const { t } = useI18n();
  return (
    <Alert
      type="error"
      showIcon
      title={title ?? t('tenants.digitalServices.accessDeniedTitle')}
      description={message ?? t('tenants.digitalServices.accessDenied')}
    />
  );
}
