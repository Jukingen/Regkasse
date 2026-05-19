'use client';

import { Button, Empty } from 'antd';
import Link from 'next/link';

import { useI18n } from '@/i18n';

export function SelectTenantFirstPlaceholder() {
    const { t } = useI18n();

    return (
        <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={
                <span>
                    <strong>{t('adminShell.tenant.selectTenantFirstTitle')}</strong>
                    <br />
                    {t('adminShell.tenant.selectTenantFirstBody')}
                </span>
            }
        >
            <Link href="/admin/tenants">
                <Button type="primary">{t('adminShell.tenant.superAdminPromptAction')}</Button>
            </Link>
        </Empty>
    );
}
