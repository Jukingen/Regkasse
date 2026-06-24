'use client';

import { Button, Space } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import { useRouter, useSearchParams } from 'next/navigation';
import { useI18n } from '@/i18n';
import { BillingAccessGate } from '@/features/billing/components/BillingAccessGate';
import { BillingNewSaleForm } from '@/features/billing/components/BillingNewSaleForm';

export default function BillingNewSalePage() {
    const { t } = useI18n();
    const router = useRouter();
    const searchParams = useSearchParams();
    const initialTenantId = searchParams.get('tenantId') ?? undefined;

    return (
        <BillingAccessGate>
            <div style={{ padding: 24 }}>
                <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                    <div style={{ display: 'flex', alignItems: 'flex-start', gap: 16, flexWrap: 'wrap' }}>
                        <Button icon={<ArrowLeftOutlined />} onClick={() => router.push('/admin/billing/sales')}>
                            {t('billing.new.back')}
                        </Button>
                        <div>
                            <h1 style={{ margin: 0 }}>{t('billing.new.title')}</h1>
                            <p style={{ color: '#64748b', marginBottom: 0 }}>{t('billing.new.subtitle')}</p>
                        </div>
                    </div>
                    <BillingNewSaleForm initialTenantId={initialTenantId} />
                </Space>
            </div>
        </BillingAccessGate>
    );
}
