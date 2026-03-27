'use client';

import Link from 'next/link';
import { Button, Result } from 'antd';
import { useI18n } from '@/i18n';

export default function NotFound() {
    const { t } = useI18n();

    return (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
            <Result
                status="404"
                title={t('common.system.status404')}
                subTitle={t('common.system.notFound404Subtitle')}
                extra={
                    <Link href="/dashboard">
                        <Button type="primary">{t('common.system.backToDashboard')}</Button>
                    </Link>
                }
            />
        </div>
    );
}
