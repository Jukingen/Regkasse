'use client';

import React from 'react';
import { Card, Typography, message } from 'antd';
import GenerateReceiptForm from '@/features/receipt-templates/components/GenerateReceiptForm';
import { useReceiptTemplateQueries } from '@/features/receipt-templates/hooks/useReceiptTemplateQueries';
import type { GenerateReceiptRequest } from '@/api/generated/model';

const { Title } = Typography;

export default function GenerateReceiptPage() {
    const { useGenerate } = useReceiptTemplateQueries();

    const { mutateAsync: generateReceipt, isPending } = useGenerate({
        mutation: {
            onError: (error: Error) => {
                message.error(`Generate failed: ${error.message}`);
            },
        },
    });

    const handleGenerate = async (request: GenerateReceiptRequest): Promise<string> => {
        const result = await generateReceipt({ data: request });
        return result.generatedContent;
    };

    return (
        <Card>
            <Title level={3}>Generate Receipt</Title>
            <GenerateReceiptForm onGenerate={handleGenerate} loading={isPending} />
        </Card>
    );
}
