'use client';

import React, { Suspense, useCallback, useState } from 'react';
import { Tabs, Typography } from 'antd';
import { PageSkeleton } from '@/components/Skeleton';
import RksvSignatureChainVerification from '@/features/rksv/signature-chain/RksvSignatureChainVerification';
import { SingleSignatureVerifyCard } from '@/features/rksv/components/SingleSignatureVerifyPage';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';

function SignatureVerifyTabs() {
    const { t } = useI18n();
    const tp = useCallback((path: string) => t(`rksvHub.signatureVerifyPage.${path}`), [t]);
    const [activeTab, setActiveTab] = useState('verify');

    const tabItems = [
        {
            key: 'verify',
            label: tp('tabVerify'),
            children: <SingleSignatureVerifyCard />,
        },
        {
            key: 'chain',
            label: tp('tabChain'),
            children: <RksvSignatureChainVerification embedded />,
        },
    ];

    return (
        <>
            <AdminPageHeader
                title={tp('title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('adminShell.group.rksv'), href: '/rksv' },
                    { title: tp('breadcrumb') },
                ]}
            >
                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                    {tp('subtitle')}
                </Typography.Paragraph>
            </AdminPageHeader>
            <Tabs activeKey={activeTab} onChange={setActiveTab} items={tabItems} />
        </>
    );
}

/** Suspense boundary required for useSearchParams in RksvSignatureChainVerification. */
export default function AdminRksvSignatureVerifyPage() {
    return (
        <Suspense fallback={<PageSkeleton widgets={3} />}>
            <SignatureVerifyTabs />
        </Suspense>
    );
}
