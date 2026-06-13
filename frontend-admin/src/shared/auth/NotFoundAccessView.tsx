'use client';

import Link from 'next/link';
import { Button, Result, Typography } from 'antd';
import { useI18n } from '@/i18n';
import { useSafeNavigateBack } from '@/shared/auth/useSafeNavigateBack';

export type NotFoundAccessViewProps = {
    compact?: boolean;
};

export function NotFoundAccessView({ compact = false }: NotFoundAccessViewProps) {
    const goBack = useSafeNavigateBack('/dashboard');
    const { t } = useI18n();

    return (
        <Result
            status="404"
            title={t('common.system.status404')}
            subTitle={
                <>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
                        {t('common.system.notFound404Subtitle')}
                    </Typography.Paragraph>
                    <Typography.Text type="secondary">{t('common.system.notFound404Hint')}</Typography.Text>
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
