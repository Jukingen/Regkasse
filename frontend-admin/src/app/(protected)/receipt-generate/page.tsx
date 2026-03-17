'use client';

import React from 'react';
import { useSearchParams } from 'next/navigation';
import { Alert, Card, message } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';

import GenerateReceiptForm from '@/features/receipt-templates/components/GenerateReceiptForm';
import { useReceiptTemplates } from '@/features/receipt-templates/hooks/useReceiptTemplates';
import type { GenerateReceiptRequest } from '@/api/generated/model';

/** Receipt-generate page: template-based sample output only. Not fiscal; no TSE, no payment. */
export default function GenerateReceiptPage() {
    const searchParams = useSearchParams();
    const templateIdFromUrl = searchParams.get('templateId') ?? undefined;
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
                title="Belegvorschau erzeugen"
                breadcrumbs={[{ title: 'Dashboard', href: '/' }, { title: 'Belegvorschau erzeugen' }]}
            />

            <Alert
                type="info"
                showIcon
                message="Nur Vorschau – keine fiskale Quittung"
                description="Hier wird ausschließlich Mustertext aus Vorlagen erzeugt. Es entstehen keine Zahlungen, keine TSE-Signatur und keine fiskalrelevanten Belege."
                style={{ marginBottom: 16 }}
            />

            <Card>
                <GenerateReceiptForm
                onGenerate={handleGenerate}
                loading={isPending}
                initialTemplateId={templateIdFromUrl}
            />
            </Card>
        </React.Fragment>
    );
}
