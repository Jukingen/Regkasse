'use client';

import { Alert, Spin } from 'antd';
import React from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { useI18n } from '@/i18n';

export function BillingAccessGate({ children }: { children: React.ReactNode }) {
  const { t } = useI18n();
  const { isAuthInitializing } = useAuth();
  const canAccess = useBillingAccess();

  if (isAuthInitializing) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
        <Spin size="large" />
      </div>
    );
  }

  if (!canAccess) {
    return <Alert type="warning" showIcon message={t('billing.accessDenied')} />;
  }

  return <>{children}</>;
}
