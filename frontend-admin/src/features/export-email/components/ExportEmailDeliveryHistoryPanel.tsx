'use client';

import { MailOutlined, StopOutlined } from '@ant-design/icons';
import { Button, Card, Empty, List, Select, Space, Tag, Typography } from 'antd';
import { useState } from 'react';

import {
  useCancelScheduledExportEmail,
  useExportEmailHistory,
} from '@/features/export-email/api/exportEmailApi';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';
import { formatMobileFileSize } from '@/lib/download/mobileDownload';

function statusColor(status: string): string {
  switch (status) {
    case 'sent':
      return 'success';
    case 'scheduled':
      return 'processing';
    case 'failed':
      return 'error';
    case 'cancelled':
      return 'default';
    default:
      return 'default';
  }
}

function statusLabel(status: string, t: (key: string) => string): string {
  switch (status) {
    case 'sent':
      return t('common.exportEmail.status.sent');
    case 'scheduled':
      return t('common.exportEmail.status.scheduled');
    case 'failed':
      return t('common.exportEmail.status.failed');
    case 'cancelled':
      return t('common.exportEmail.status.cancelled');
    case 'pending':
      return t('common.exportEmail.status.pending');
    default:
      return status;
  }
}

/**
 * Lists export-as-email deliveries (sent / scheduled / failed) for the tenant.
 */
export function ExportEmailDeliveryHistoryPanel() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<string | undefined>();
  const query = useExportEmailHistory({ status, page, pageSize: 10 });
  const cancelMutation = useCancelScheduledExportEmail();

  const items = query.data?.items ?? [];
  const total = query.data?.totalCount ?? 0;

  return (
    <Card
      title={
        <Space>
          <MailOutlined />
          <span>{t('common.exportEmail.historyTitle')}</span>
        </Space>
      }
      extra={
        <Select
          allowClear
          placeholder={t('common.exportEmail.filterStatus')}
          style={{ minWidth: 160 }}
          value={status}
          onChange={(v) => {
            setStatus(v);
            setPage(1);
          }}
          options={[
            { value: 'sent', label: t('common.exportEmail.status.sent') },
            { value: 'scheduled', label: t('common.exportEmail.status.scheduled') },
            { value: 'failed', label: t('common.exportEmail.status.failed') },
            { value: 'cancelled', label: t('common.exportEmail.status.cancelled') },
          ]}
        />
      }
    >
      {items.length === 0 && !query.isLoading ? (
        <Empty description={t('common.exportEmail.historyEmpty')} />
      ) : (
        <List
          loading={query.isLoading}
          dataSource={items}
          pagination={{
            current: page,
            pageSize: 10,
            total,
            onChange: setPage,
            showSizeChanger: false,
          }}
          renderItem={(item) => (
            <List.Item
              actions={
                item.status === 'scheduled'
                  ? [
                      <Button
                        key="cancel"
                        size="small"
                        danger
                        icon={<StopOutlined />}
                        loading={cancelMutation.isPending}
                        onClick={() => {
                          void cancelMutation
                            .mutateAsync(item.id)
                            .then(() => notify.success(t('common.exportEmail.cancelScheduledSuccess')))
                            .catch(() => notify.error(t('common.exportEmail.cancelScheduledFailed')));
                        }}
                      >
                        {t('common.exportEmail.cancelSchedule')}
                      </Button>,
                    ]
                  : undefined
              }
            >
              <List.Item.Meta
                title={
                  <Space wrap>
                    <Typography.Text strong>{item.subject}</Typography.Text>
                    <Tag color={statusColor(item.status)}>{statusLabel(item.status, t)}</Tag>
                    <Tag>
                      {item.deliveryMode === 'link'
                        ? t('common.exportEmail.modeLink')
                        : t('common.exportEmail.modeAttachment')}
                    </Tag>
                  </Space>
                }
                description={
                  <Space orientation="vertical" size={0}>
                    <Typography.Text type="secondary">
                      {t('common.exportEmail.to')}: {item.recipientEmail}
                    </Typography.Text>
                    <Typography.Text type="secondary">
                      {item.fileName} · {formatMobileFileSize(item.fileSizeBytes, formatLocale)} ·{' '}
                      {formatDateTime(item.createdAtUtc, formatLocale)}
                    </Typography.Text>
                    {item.scheduledForUtc ? (
                      <Typography.Text type="secondary">
                        {t('common.exportEmail.scheduleAt')}:{' '}
                        {formatDateTime(item.scheduledForUtc, formatLocale)}
                      </Typography.Text>
                    ) : null}
                    {item.errorMessage ? (
                      <Typography.Text type="danger">{item.errorMessage}</Typography.Text>
                    ) : null}
                  </Space>
                }
              />
            </List.Item>
          )}
        />
      )}
    </Card>
  );
}
