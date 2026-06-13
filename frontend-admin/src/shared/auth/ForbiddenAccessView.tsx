'use client';

import Link from 'next/link';
import { Button, Result, Typography } from 'antd';
import { useI18n } from '@/i18n';
import { useSafeNavigateBack } from '@/shared/auth/useSafeNavigateBack';

export type ForbiddenAccessViewProps = {
    /** Tighter padding when rendered inside the admin content shell. */
    compact?: boolean;
};

export function ForbiddenAccessView({ compact = false }: ForbiddenAccessViewProps) {
    const goBack = useSafeNavigateBack('/dashboard');
    const { t } = useI18n();

    return (
        <Result
            status="403"
            title={t('common.system.forbidden403Title')}
            subTitle={
                <>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
                        {t('common.system.forbidden403Subtitle')}
                    </Typography.Paragraph>
                    <Typography.Text type="secondary">{t('common.system.forbidden403Hint')}</Typography.Text>
                </>
            }
            extra={[
                <Link href="/dashboard" key="dashboard">
                    <Button type="primary">{t('common.system.backToDashboard')}</Button>
                </Link>,
                <Button key="back" onClick={goBack}>
                    {t('common.system.goBack')}
                </Button>,
            ]}
            style={compact ? { padding: '32px 16px' } : undefined}
        />
    );
}
