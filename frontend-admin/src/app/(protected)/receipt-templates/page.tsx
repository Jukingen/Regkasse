'use client';

import { PlusOutlined } from '@ant-design/icons';
import { Button, Space } from 'antd';
import Link from 'next/link';
import React, { useState } from 'react';

import { AdminDataList } from '@/components/admin-layout/AdminDataList';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useAuth } from '@/features/auth/hooks/useAuth';
import ReceiptFilters from '@/features/receipt-templates/components/ReceiptFilters';
import ReceiptPreviewModal from '@/features/receipt-templates/components/ReceiptPreviewModal';
import ReceiptTemplateList from '@/features/receipt-templates/components/ReceiptTemplateList';
import {
  useReceiptTemplateFilters,
  useReceiptTemplates,
} from '@/features/receipt-templates/hooks/useReceiptTemplates';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

export default function ReceiptTemplatesPage() {
  const { message } = useAntdApp();

  const { t } = useI18n();
  // 1. URL State
  const { filters } = useReceiptTemplateFilters();
  const mode = filters.mode || 'all';
  const value = filters.value || '';

  const [previewId, setPreviewId] = useState<string | null>(null);

  // 2. Data Fetching
  const { useList, useListByLanguage, useListByType, useDelete, invalidateList } =
    useReceiptTemplates();

  const {
    data: allTemplates,
    isLoading: loadingAll,
    error: errorAll,
  } = useList({
    query: { enabled: mode === 'all' },
  });

  const {
    data: languageTemplates,
    isLoading: loadingLang,
    error: errorLang,
  } = useListByLanguage(value, {
    query: { enabled: mode === 'language' && !!value },
  });

  const {
    data: typeTemplates,
    isLoading: loadingType,
    error: errorType,
  } = useListByType(value, {
    query: { enabled: mode === 'type' && !!value },
  });

  // 3. Derived Data
  const data =
    mode === 'language' ? languageTemplates : mode === 'type' ? typeTemplates : allTemplates;

  const isLoading = loadingAll || loadingLang || loadingType;
  const error = errorAll || errorLang || errorType;

  const { user } = useAuth();
  const canManage = hasPermission(user, PERMISSIONS.RECEIPT_TEMPLATE_MANAGE);

  // 4. Mutations
  const { mutate: deleteTemplate } = useDelete({
    mutation: {
      onSuccess: () => {
        message.success(t('receiptTemplates.page.deleteSuccess'));
        invalidateList();
      },
      onError: (err: Error) => {
        message.error(t('receiptTemplates.page.deleteError', { message: err.message }));
      },
    },
  });

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <AdminPageHeader
        title={ADMIN_NAV_LABELS.receiptTemplates}
        breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.receiptTemplates }]}
        actions={
          canManage ? (
            <Link href="/receipt-templates/new">
              <Button type="primary" icon={<PlusOutlined />}>
                {t('receiptTemplates.page.newTemplate')}
              </Button>
            </Link>
          ) : undefined
        }
      >
        <ReceiptFilters />
      </AdminPageHeader>

      <AdminDataList
        isLoading={isLoading}
        isError={!!error}
        error={error as Error}
        isEmpty={!data || data.length === 0}
        emptyText={t('receiptTemplates.page.emptyList')}
      >
        <ReceiptTemplateList
          data={data || []}
          loading={isLoading}
          canManage={canManage}
          onDelete={(id) => deleteTemplate({ id })}
          onPreview={setPreviewId}
        />
      </AdminDataList>

      <ReceiptPreviewModal templateId={previewId} onClose={() => setPreviewId(null)} />
    </Space>
  );
}
