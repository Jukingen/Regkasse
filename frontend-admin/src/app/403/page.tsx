'use client';

import React from 'react';
import { Result, Button } from 'antd';
import { useRouter } from 'next/navigation';
import { useI18n } from '@/i18n';

export default function ForbiddenPage() {
    const router = useRouter();
    const { t } = useI18n();

    return (
        <div style={{ height: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
            <Result
                status="403"
                title={t('common.system.status403')}
                subTitle={t('common.system.forbidden403Subtitle')}
                extra={
                    <Button type="primary" onClick={() => router.push('/dashboard')}>
                        {t('common.system.backToDashboard')}
                    </Button>
                }
            />
        </div>
    );
}
