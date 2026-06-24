'use client';

import React from 'react';
import { Alert } from 'antd';
import { useI18n } from '@/i18n';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function BillingAccessGate({ children }: { children: React.ReactNode }) {
    const { t } = useI18n();
    const canAccess = useBillingAccess();

    if (!canAccess) {
        return <Alert type="warning" showIcon message={t('billing.accessDenied')} />;
    }

    return <>{children}</>;
}
