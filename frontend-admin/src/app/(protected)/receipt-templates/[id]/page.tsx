'use client';

import { ArrowLeftOutlined, EyeOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Spin } from 'antd';
import { useParams, useRouter } from 'next/navigation';
import React, { useEffect, useState } from 'react';

import type { UpdateReceiptTemplateRequest } from '@/api/generated/model';
import { AdminDataList } from '@/components/admin-layout/AdminDataList';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useAuth } from '@/features/auth/hooks/useAuth';
import ReceiptPreviewModal from '@/features/receipt-templates/components/ReceiptPreviewModal';
import ReceiptTemplateForm from '@/features/receipt-templates/components/ReceiptTemplateForm';
import { useReceiptTemplates } from '@/features/receipt-templates/hooks/useReceiptTemplates';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

function isNotFoundError(err: unknown): boolean {
  const status =
    (err as { response?: { status?: number } })?.response?.status ??
    (err as { normalized?: { status?: number } })?.normalized?.status;
  return status === 404;
}

export default function EditReceiptTemplatePage() {
  const { message } = useAntdApp();

  const { t } = useI18n();
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
        message.success(t('receiptTemplates.page.updateSuccess'));
        invalidateList();
      },
      onError: (err: Error) => {
        message.error(t('receiptTemplates.page.updateError', { message: err.message }));
      },
    },
  });

  const handleSubmit = (values: UpdateReceiptTemplateRequest) => {
    updateTemplate({ id, data: values });
  };

  if (!isInitialized || !canManage) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
        <Spin size="large" description={t('receiptTemplates.page.accessCheck')} />
      </div>
    );
  }

  if (isError && isNotFoundError(error)) {
    return (
      <React.Fragment>
        <AdminPageHeader
          title={t('receiptTemplates.page.editTitle')}
          breadcrumbs={[
            ADMIN_OVERVIEW_CRUMB,
            { title: ADMIN_NAV_LABELS.receiptTemplates, href: '/receipt-templates' },
            { title: t('receiptTemplates.page.breadcrumbEdit') },
          ]}
          actions={
            <Button icon={<ArrowLeftOutlined />} onClick={() => router.push('/receipt-templates')}>
              {t('receiptTemplates.page.backToList')}
            </Button>
          }
        />
        <Card>
          <Alert
            type="warning"
            title={t('receiptTemplates.page.templateNotFoundTitle')}
            description={t('receiptTemplates.page.templateNotFoundDescription')}
            showIcon
            action={
              <Button onClick={() => router.push('/receipt-templates')}>
                {t('receiptTemplates.page.backToList')}
              </Button>
            }
          />
        </Card>
      </React.Fragment>
    );
  }

  return (
    <React.Fragment>
      <AdminPageHeader
        title={t('receiptTemplates.page.editTitleWithName', {
          name: data?.templateName || '…',
        })}
        breadcrumbs={[
          ADMIN_OVERVIEW_CRUMB,
          { title: ADMIN_NAV_LABELS.receiptTemplates, href: '/receipt-templates' },
          { title: t('receiptTemplates.page.breadcrumbEdit') },
        ]}
        actions={
          <>
            <Button icon={<ArrowLeftOutlined />} onClick={() => router.push('/receipt-templates')}>
              {t('receiptTemplates.page.back')}
            </Button>
            <Button icon={<EyeOutlined />} onClick={() => setShowPreview(true)} disabled={!data}>
              {t('receiptTemplates.page.previewButton')}
            </Button>
          </>
        }
      />

      <AdminDataList
        isLoading={isLoading}
        isError={isError}
        error={error as Error}
        isEmpty={!id}
        emptyText={t('receiptTemplates.page.emptyTemplateId')}
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
