'use client';

import React, { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, Button, message } from 'antd';
import { ArrowLeftOutlined, EyeOutlined } from '@ant-design/icons';

import ReceiptTemplateForm from '@/features/receipt-templates/components/ReceiptTemplateForm';
import ReceiptPreviewModal from '@/features/receipt-templates/components/ReceiptPreviewModal';
import { useReceiptTemplates } from '@/features/receipt-templates/hooks/useReceiptTemplates';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminDataList } from '@/components/admin-layout/AdminDataList';

import type { UpdateReceiptTemplateRequest } from '@/api/generated/model';

export default function EditReceiptTemplatePage() {
    const { id } = useParams<{ id: string }>();
    const router = useRouter();
    const [showPreview, setShowPreview] = useState(false);

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
