'use client';

import React, { useState } from 'react';
import { Button, message, Space } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import Link from 'next/link';

import ReceiptTemplateList from '@/features/receipt-templates/components/ReceiptTemplateList';
import ReceiptPreviewModal from '@/features/receipt-templates/components/ReceiptPreviewModal';
import ReceiptFilters from '@/features/receipt-templates/components/ReceiptFilters';
import { useReceiptTemplates, useReceiptTemplateFilters } from '@/features/receipt-templates/hooks/useReceiptTemplates';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { hasPermission } from '@/shared/auth/permissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminDataList } from '@/components/admin-layout/AdminDataList';

export default function ReceiptTemplatesPage() {
    // 1. URL State
    const { filters } = useReceiptTemplateFilters();
    const mode = filters.mode || 'all';
    const value = filters.value || '';

    const [previewId, setPreviewId] = useState<string | null>(null);

    // 2. Data Fetching
    const { useList, useListByLanguage, useListByType, useDelete, invalidateList } =
        useReceiptTemplates();

    const { data: allTemplates, isLoading: loadingAll, error: errorAll } = useList({
        query: { enabled: mode === 'all' },
    });

    const { data: languageTemplates, isLoading: loadingLang, error: errorLang } = useListByLanguage(value, {
        query: { enabled: mode === 'language' && !!value },
    });

    const { data: typeTemplates, isLoading: loadingType, error: errorType } = useListByType(value, {
        query: { enabled: mode === 'type' && !!value },
    });

    // 3. Derived Data
    const data =
        mode === 'language'
            ? languageTemplates
            : mode === 'type'
                ? typeTemplates
                : allTemplates;

    const isLoading = loadingAll || loadingLang || loadingType;
    const error = errorAll || errorLang || errorType;

    const { user } = useAuth();
    const canManage = hasPermission(user, PERMISSIONS.RECEIPT_TEMPLATE_MANAGE);

    // 4. Mutations
    const { mutate: deleteTemplate } = useDelete({
        mutation: {
            onSuccess: () => {
                message.success('Template deleted');
                invalidateList();
            },
            onError: (err: Error) => {
                message.error(`Delete failed: ${err.message}`);
            },
        },
    });

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>

            <AdminPageHeader
                title="Receipt Templates"
                breadcrumbs={[{ title: 'Dashboard', href: '/dashboard' }, { title: 'Receipt Templates' }]}
                actions={
                    canManage ? (
                        <Link href="/receipt-templates/new">
                            <Button type="primary" icon={<PlusOutlined />}>
                                New Template
                            </Button>
                        </Link>
                    ) : undefined
                }
            >
                <ReceiptFilters />
            </AdminPageHeader>

            <AdminDataList
                isLoading={isLoading}
                isError={!!error}
                error={error as Error}
                isEmpty={!data || data.length === 0}
                emptyText="No receipt templates found. Try changing filters or create a new template."
            >
                <ReceiptTemplateList
                    data={data || []}
                    loading={isLoading}
                    canManage={canManage}
                    onDelete={(id) => deleteTemplate({ id })}
                    onPreview={setPreviewId}
                />
            </AdminDataList>

            <ReceiptPreviewModal
                templateId={previewId}
                onClose={() => setPreviewId(null)}
            />
        </Space>
    );
}
