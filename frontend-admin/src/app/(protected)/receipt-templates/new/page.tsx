'use client';

import { ArrowLeftOutlined } from '@ant-design/icons';
import { Button, Card, Spin } from 'antd';
import { useRouter } from 'next/navigation';
import React, { useEffect } from 'react';

import type {
  CreateReceiptTemplateRequest,
  UpdateReceiptTemplateRequest,
} from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useAuth } from '@/features/auth/hooks/useAuth';
import ReceiptTemplateForm from '@/features/receipt-templates/components/ReceiptTemplateForm';
import { useReceiptTemplates } from '@/features/receipt-templates/hooks/useReceiptTemplates';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

export default function NewReceiptTemplatePage() {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const router = useRouter();
  const { user, isInitialized } = useAuth();
  const canManage = hasPermission(user, PERMISSIONS.RECEIPT_TEMPLATE_MANAGE);

  useEffect(() => {
    if (isInitialized && !canManage) {
      router.replace('/receipt-templates');
    }
  }, [isInitialized, canManage, router]);
  const { useCreate, invalidateList } = useReceiptTemplates();

  const { mutate: createTemplate, isPending } = useCreate({
    mutation: {
      onSuccess: () => {
        message.success(t('receiptTemplates.page.createSuccess'));
        invalidateList();
        router.push('/receipt-templates');
      },
      onError: (error: Error) => {
        message.error(t('receiptTemplates.page.createError', { message: error.message }));
      },
    },
  });

  const handleSubmit = (values: CreateReceiptTemplateRequest | UpdateReceiptTemplateRequest) => {
    // Strict typing: values from form should match CreateReceiptTemplateRequest
    createTemplate({ data: values as CreateReceiptTemplateRequest });
  };

  if (!isInitialized || !canManage) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
        <Spin size="large" description={t('receiptTemplates.page.accessCheck')} />
      </div>
    );
  }

  return (
    <React.Fragment>
      <AdminPageHeader
        title={t('receiptTemplates.page.newTitle')}
        breadcrumbs={[
          ADMIN_OVERVIEW_CRUMB,
          { title: ADMIN_NAV_LABELS.receiptTemplates, href: '/receipt-templates' },
          { title: t('receiptTemplates.page.breadcrumbNew') },
        ]}
        actions={
          <Button icon={<ArrowLeftOutlined />} onClick={() => router.push('/receipt-templates')}>
            {t('receiptTemplates.page.back')}
          </Button>
        }
      />

      <Card>
        <ReceiptTemplateForm onSubmit={handleSubmit} loading={isPending} mode="create" />
      </Card>
    </React.Fragment>
  );
}
