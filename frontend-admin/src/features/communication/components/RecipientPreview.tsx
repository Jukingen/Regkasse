'use client';

import { MailOutlined } from '@ant-design/icons';
import { Alert, Space, Spin, Typography } from 'antd';
import React from 'react';

import { useI18n } from '@/i18n';

export type RecipientPreviewProps = {
  recipientCount?: number;
  tenantCount?: number;
  loading?: boolean;
  error?: boolean;
  checked?: boolean;
};

export function RecipientPreview({
  recipientCount,
  tenantCount,
  loading = false,
  error = false,
  checked = false,
}: RecipientPreviewProps) {
  const { t } = useI18n();

  if (loading) {
    return (
      <Alert
        type="info"
        showIcon
        icon={<Spin size="small" />}
        title={t('communication.bulkEmail.preview')}
      />
    );
  }

  if (error) {
    return <Alert type="error" showIcon title={t('communication.bulkEmail.previewError')} />;
  }

  if (!checked) {
    return (
      <Alert
        type="info"
        showIcon
        icon={<MailOutlined />}
        title={t('communication.bulkEmail.preview')}
      />
    );
  }

  const count = recipientCount ?? 0;
  if (count <= 0) {
    return <Alert type="warning" showIcon title={t('communication.bulkEmail.noRecipients')} />;
  }

  return (
    <Alert
      type="success"
      showIcon
      icon={<MailOutlined />}
      title={
        <Space orientation="vertical" size={0}>
          <Typography.Text>
            {t('communication.bulkEmail.recipientCount', { count })}
          </Typography.Text>
          <Typography.Text type="secondary">
            {t('communication.bulkEmail.tenantCount', { count: tenantCount ?? 0 })}
          </Typography.Text>
        </Space>
      }
    />
  );
}
