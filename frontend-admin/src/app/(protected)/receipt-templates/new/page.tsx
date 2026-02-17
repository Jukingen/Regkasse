'use client';

import React from 'react';
import { Card, Typography, message } from 'antd';
import { useRouter } from 'next/navigation';
import ReceiptTemplateForm from '@/features/receipt-templates/components/ReceiptTemplateForm';
import { useReceiptTemplateQueries } from '@/features/receipt-templates/hooks/useReceiptTemplateQueries';
import type { CreateReceiptTemplateRequest, UpdateReceiptTemplateRequest } from '@/api/generated/model';

const { Title } = Typography;

export default function NewReceiptTemplatePage() {
    const router = useRouter();
    const { useCreate, invalidateList } = useReceiptTemplateQueries();

    const { mutate: createTemplate, isPending } = useCreate({
        mutation: {
            onSuccess: () => {
                message.success('Template created');
                invalidateList();
                router.push('/receipt-templates');
            },
            onError: (error: Error) => {
                message.error(`Create failed: ${error.message}`);
            },
        },
    });

    const handleSubmit = (values: CreateReceiptTemplateRequest | UpdateReceiptTemplateRequest) => {
        createTemplate({ data: values as CreateReceiptTemplateRequest });
    };

    return (
        <Card>
            <Title level={3}>Create Receipt Template</Title>
            <ReceiptTemplateForm
                onSubmit={handleSubmit}
                loading={isPending}
                mode="create"
            />
        </Card>
    );
}
