'use client';

import React, { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, Typography, Spin, Alert, Button, Space, Divider, message } from 'antd';
import { ArrowLeftOutlined, EyeOutlined } from '@ant-design/icons';
import ReceiptTemplateForm from '@/features/receipt-templates/components/ReceiptTemplateForm';
import ReceiptPreviewModal from '@/features/receipt-templates/components/ReceiptPreviewModal';
import { useReceiptTemplateQueries } from '@/features/receipt-templates/hooks/useReceiptTemplateQueries';
import type { UpdateReceiptTemplateRequest } from '@/api/generated/model';

const { Title } = Typography;

export default function EditReceiptTemplatePage() {
    const { id } = useParams<{ id: string }>();
    const router = useRouter();
    const [showPreview, setShowPreview] = useState(false);
    const { useDetail, useUpdate, invalidateList } = useReceiptTemplateQueries();

    const { data, isLoading, isError, error } = useDetail(id);

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

    if (isLoading) return <Spin style={{ display: 'block', margin: '80px auto' }} />;

    if (isError) {
        return (
            <Alert
                type="error"
                message="Failed to load template"
                description={(error as Error)?.message}
                showIcon
                action={
                    <Button onClick={() => router.push('/receipt-templates')}>
                        Back to Templates
                    </Button>
                }
            />
        );
    }

    if (!data) {
        return (
            <Alert
                type="warning"
                message="Template not found"
                showIcon
                action={
                    <Button onClick={() => router.push('/receipt-templates')}>
                        Back to Templates
                    </Button>
                }
            />
        );
    }

    return (
        <Card>
            <Space direction="vertical" size="large" style={{ width: '100%' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <Space>
                        <Button
                            icon={<ArrowLeftOutlined />}
                            onClick={() => router.push('/receipt-templates')}
                        >
                            Back
                        </Button>
                        <Title level={3} style={{ margin: 0 }}>
                            Edit Template: {data.templateName}
                        </Title>
                    </Space>
                    <Button icon={<EyeOutlined />} onClick={() => setShowPreview(true)}>
                        Preview
                    </Button>
                </div>

                <Divider />

                <ReceiptTemplateForm
                    initialValues={data}
                    onSubmit={handleSubmit}
                    loading={isPending}
                    mode="edit"
                />
            </Space>

            <ReceiptPreviewModal
                templateId={showPreview ? id : null}
                onClose={() => setShowPreview(false)}
            />
        </Card>
    );
}
