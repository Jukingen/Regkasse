'use client';

import React, { useState, useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, Button, message, Spin, Alert } from 'antd';
import { ArrowLeftOutlined, EyeOutlined } from '@ant-design/icons';

import ReceiptTemplateForm from '@/features/receipt-templates/components/ReceiptTemplateForm';
import ReceiptPreviewModal from '@/features/receipt-templates/components/ReceiptPreviewModal';
import { useReceiptTemplates } from '@/features/receipt-templates/hooks/useReceiptTemplates';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { hasPermission } from '@/shared/auth/permissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { AdminDataList } from '@/components/admin-layout/AdminDataList';

import type { UpdateReceiptTemplateRequest } from '@/api/generated/model';
import { useI18n } from '@/i18n';

function isNotFoundError(err: unknown): boolean {
    const status =
        (err as { response?: { status?: number } })?.response?.status ??
        (err as { normalized?: { status?: number } })?.normalized?.status;
    return status === 404;
}

export default function EditReceiptTemplatePage() {
    const { t } = useI18n();
    const { id } = useParams<{ id: string }>();
    const router = useRouter();
    const [showPreview, setShowPreview] = useState(false);
    const { user, isInitialized } = useAuth();
    const canManage = hasPermission(user, PERMISSIONS.RECEIPT_TEMPLATE_MANAGE);

    useEffect(() => {
        if (isInitialized && !canManage) {
            router.replace('/receipt-templates');
        }
    }, [isInitialized, canManage, router]);

    // Use new hook
    const { useDetail, useUpdate, invalidateList } = useReceiptTemplates();
    const { data, isLoading, isError, error } = useDetail(id, {
        query: {
            enabled: !!id,
        },
    });

    const { mutate: updateTemplate, isPending } = useUpdate({
        mutation: {
            onSuccess: () => {
                message.success(t('receiptTemplates.page.updateSuccess'));
                invalidateList();
            },
            onError: (err: Error) => {
                message.error(t('receiptTemplates.page.updateError', { message: err.message }));
            },
        },
    });

    const handleSubmit = (values: UpdateReceiptTemplateRequest) => {
        updateTemplate({ id, data: values });
    };

    if (!isInitialized || !canManage) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
                <Spin size="large" tip={t('receiptTemplates.page.accessCheck')} />
            </div>
        );
    }

    if (isError && isNotFoundError(error)) {
        return (
            <React.Fragment>
                <AdminPageHeader
                    title={t('receiptTemplates.page.editTitle')}
                    breadcrumbs={[
                        ADMIN_OVERVIEW_CRUMB,
                        { title: ADMIN_NAV_LABELS.receiptTemplates, href: '/receipt-templates' },
                        { title: t('receiptTemplates.page.breadcrumbEdit') },
                    ]}
                    actions={
                        <Button icon={<ArrowLeftOutlined />} onClick={() => router.push('/receipt-templates')}>
                            {t('receiptTemplates.page.backToList')}
                        </Button>
                    }
                />
                <Card>
                    <Alert
                        type="warning"
                        message={t('receiptTemplates.page.templateNotFoundTitle')}
                        description={t('receiptTemplates.page.templateNotFoundDescription')}
                        showIcon
                        action={
                            <Button onClick={() => router.push('/receipt-templates')}>{t('receiptTemplates.page.backToList')}</Button>
                        }
                    />
                </Card>
            </React.Fragment>
        );
    }

    return (
        <React.Fragment>
            <AdminPageHeader
                title={t('receiptTemplates.page.editTitleWithName', {
                    name: data?.templateName || '…',
                })}
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: ADMIN_NAV_LABELS.receiptTemplates, href: '/receipt-templates' },
                    { title: t('receiptTemplates.page.breadcrumbEdit') },
                ]}
                actions={
                    <>
                        <Button
                            icon={<ArrowLeftOutlined />}
                            onClick={() => router.push('/receipt-templates')}
                        >
                            {t('receiptTemplates.page.back')}
                        </Button>
                        <Button
                            icon={<EyeOutlined />}
                            onClick={() => setShowPreview(true)}
                            disabled={!data}
                        >
                            {t('receiptTemplates.page.previewButton')}
                        </Button>
                    </>
                }
            />

            <AdminDataList
                isLoading={isLoading}
                isError={isError}
                error={error as Error}
                isEmpty={!id}
                emptyText={t('receiptTemplates.page.emptyTemplateId')}
            >
                {data && (
                    <ReceiptTemplateForm
                        initialValues={data}
                        onSubmit={handleSubmit}
                        loading={isPending}
                        mode="edit"
                    />
                )}
            </AdminDataList>

            <ReceiptPreviewModal
                templateId={showPreview ? id : null}
                onClose={() => setShowPreview(false)}
            />
        </React.Fragment>
    );
}
