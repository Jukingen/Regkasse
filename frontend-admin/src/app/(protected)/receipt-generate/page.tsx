'use client';

import { Alert, Card } from 'antd';
import { useSearchParams } from 'next/navigation';
import React from 'react';

import type { GenerateReceiptRequest } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import GenerateReceiptForm from '@/features/receipt-templates/components/GenerateReceiptForm';
import { useReceiptTemplates } from '@/features/receipt-templates/hooks/useReceiptTemplates';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';

/** Receipt-generate page: template-based sample output only. Not fiscal; no TSE, no payment. */
export default function GenerateReceiptPage() {
  const { message } = useAntdApp();

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
        breadcrumbs={[
          ADMIN_OVERVIEW_CRUMB,
          { title: t('receiptTemplates.page.generateBreadcrumb') },
        ]}
      />

      <Alert
        type="warning"
        showIcon
        title={t('receiptTemplates.page.generateWarningTitle')}
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
