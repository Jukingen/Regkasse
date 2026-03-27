'use client';

import React from 'react';
import { useSearchParams } from 'next/navigation';
import { Alert, Card, message } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';

import GenerateReceiptForm from '@/features/receipt-templates/components/GenerateReceiptForm';
import { useReceiptTemplates } from '@/features/receipt-templates/hooks/useReceiptTemplates';
import type { GenerateReceiptRequest } from '@/api/generated/model';
import { useI18n } from '@/i18n';

/** Receipt-generate page: template-based sample output only. Not fiscal; no TSE, no payment. */
export default function GenerateReceiptPage() {
    const { t } = useI18n();
    const searchParams = useSearchParams();
    const templateIdFromUrl = searchParams.get('templateId') ?? undefined;
    const { useGenerate } = useReceiptTemplates();

    const { mutateAsync: generateReceipt, isPending } = useGenerate({
        mutation: {
            onError: (error: Error) => {
                message.error(t('receiptTemplates.page.generatePageError', { message: error.message }));
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
                title={t('receiptTemplates.page.generateTitle')}
                breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: t('receiptTemplates.page.generateBreadcrumb') }]}
            />

            <Alert
                type="warning"
                showIcon
                message={t('receiptTemplates.page.generateWarningTitle')}
                description={t('receiptTemplates.page.generateWarningDescription')}
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
