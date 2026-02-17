'use client';

import React from 'react';
import { Card, message } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';

import GenerateReceiptForm from '@/features/receipt-templates/components/GenerateReceiptForm';
import { useReceiptTemplates } from '@/features/receipt-templates/hooks/useReceiptTemplates';
import type { GenerateReceiptRequest } from '@/api/generated/model';

export default function GenerateReceiptPage() {
    const { useGenerate } = useReceiptTemplates();

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
        <React.Fragment>
            <AdminPageHeader
                title="Generate Receipt"
                breadcrumbs={[{ title: 'Dashboard', href: '/' }, { title: 'Generate Receipt' }]}
            />

            <Card>
                <GenerateReceiptForm onGenerate={handleGenerate} loading={isPending} />
            </Card>
        </React.Fragment>
    );
}
