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
import { AdminDataList } from '@/components/admin-layout/AdminDataList';

import type { UpdateReceiptTemplateRequest } from '@/api/generated/model';

function isNotFoundError(err: unknown): boolean {
    const status =
        (err as { response?: { status?: number } })?.response?.status ??
        (err as { normalized?: { status?: number } })?.normalized?.status;
    return status === 404;
}

export default function EditReceiptTemplatePage() {
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
                message.success('Template updated');
                invalidateList();
            },
            onError: (error: Error) => {
                message.error(`Update failed: ${error.message}`);
            },
        },
    });

    const handleSubmit = (values: UpdateReceiptTemplateRequest) => {
        updateTemplate({ id, data: values });
    };

    if (!isInitialized || !canManage) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
                <Spin size="large" tip="Checking access..." />
            </div>
        );
    }

    if (isError && isNotFoundError(error)) {
        return (
            <React.Fragment>
                <AdminPageHeader
                    title="Edit Template"
                    breadcrumbs={[
                        { title: 'Receipt Templates', href: '/receipt-templates' },
                        { title: 'Edit' },
                    ]}
                    actions={
                        <Button icon={<ArrowLeftOutlined />} onClick={() => router.push('/receipt-templates')}>
                            Back to list
                        </Button>
                    }
                />
                <Card>
                    <Alert
                        type="warning"
                        message="Template not found"
                        description="The template may have been deleted or you do not have access to it."
                        showIcon
                        action={
                            <Button onClick={() => router.push('/receipt-templates')}>Back to list</Button>
                        }
                    />
                </Card>
            </React.Fragment>
        );
    }

    return (
        <React.Fragment>
            <AdminPageHeader
                title={`Edit Template: ${data?.templateName || 'Loading...'}`}
                breadcrumbs={[
                    { title: 'Receipt Templates', href: '/receipt-templates' },
                    { title: 'Edit' }
                ]}
                actions={
                    <>
                        <Button
                            icon={<ArrowLeftOutlined />}
                            onClick={() => router.push('/receipt-templates')}
                        >
                            Back
                        </Button>
                        <Button
                            icon={<EyeOutlined />}
                            onClick={() => setShowPreview(true)}
                            disabled={!data}
                        >
                            Preview
                        </Button>
                    </>
                }
            />

            <AdminDataList
                isLoading={isLoading}
                isError={isError}
                error={error as Error}
                isEmpty={!id}
                emptyText="Template ID missing"
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
