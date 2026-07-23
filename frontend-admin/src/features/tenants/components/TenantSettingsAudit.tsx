'use client';

import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  RollbackOutlined,
} from '@ant-design/icons';
import { Button, Card, Space, Spin, Tag, Timeline, Typography } from 'antd';
import type { ReactNode } from 'react';

import {
  type TenantSettingsHistoryItem,
  formatSettingsJsonValue,
} from '@/features/tenants/api/tenantSettings';
import { useDateFormatter } from '@/lib/hooks/useDateFormatter';
import { useI18n } from '@/i18n';

export type TenantSettingsAuditProps = {
  history: TenantSettingsHistoryItem[];
  loading?: boolean;
  currentUserId?: string;
  busy?: boolean;
  onApprove?: (item: TenantSettingsHistoryItem) => void;
  onReject?: (item: TenantSettingsHistoryItem) => void;
  onRevert?: (item: TenantSettingsHistoryItem) => void;
};

function statusColor(status: string): string {
  switch (status) {
    case 'approved':
      return 'green';
    case 'pending':
      return 'orange';
    case 'rejected':
      return 'red';
    case 'reverted':
      return 'gray';
    default:
      return 'blue';
  }
}

function statusDot(status: string): ReactNode {
  switch (status) {
    case 'approved':
      return <CheckCircleOutlined />;
    case 'rejected':
      return <CloseCircleOutlined />;
    case 'reverted':
      return <RollbackOutlined />;
    default:
      return <ClockCircleOutlined />;
  }
}

function settingTypeLabel(
  t: (key: string) => string,
  settingType: string
): string {
  const key = `tenants.settingsChange.settingTypes.${settingType}`;
  const translated = t(key);
  return translated === key ? settingType : translated;
}

function statusLabel(t: (key: string) => string, status: string): string {
  const key = `tenants.settingsChange.statuses.${status}`;
  const translated = t(key);
  return translated === key ? status : translated;
}

export function TenantSettingsAudit({
  history,
  loading = false,
  currentUserId,
  busy = false,
  onApprove,
  onReject,
  onRevert,
}: TenantSettingsAuditProps) {
  const { t } = useI18n();
  const { formatDateTime } = useDateFormatter();

  const items = history.map((entry) => {
    const isOwn =
      !!currentUserId &&
      currentUserId.localeCompare(entry.requestedBy, undefined, {
        sensitivity: 'accent',
      }) === 0;

    return {
      key: entry.id,
      color: statusColor(entry.status),
      dot: statusDot(entry.status),
      children: (
        <div>
          <Space wrap size="small" style={{ marginBottom: 4 }}>
            <Typography.Text strong>
              {settingTypeLabel(t, String(entry.settingType))}
            </Typography.Text>
            <Tag color={statusColor(entry.status)}>
              {statusLabel(t, String(entry.status))}
            </Tag>
          </Space>
          <div>
            <Typography.Text type="secondary">
              {formatSettingsJsonValue(entry.oldValue)} →{' '}
              {formatSettingsJsonValue(entry.newValue)}
            </Typography.Text>
          </div>
          <div>
            <Typography.Text type="secondary">
              {t('tenants.settingsChange.audit.requestedByAt', {
                userId: entry.requestedBy,
                at: formatDateTime(entry.requestedAt),
              })}
            </Typography.Text>
          </div>
          {entry.approvedBy ? (
            <div>
              <Typography.Text type="secondary">
                {t('tenants.settingsChange.audit.resolvedByAt', {
                  userId: entry.approvedBy,
                  at: entry.approvedAt
                    ? formatDateTime(entry.approvedAt)
                    : '—',
                })}
              </Typography.Text>
            </div>
          ) : null}
          {entry.reason ? (
            <div>
              <Typography.Text italic>
                {t('tenants.settingsChange.audit.reason', { reason: entry.reason })}
              </Typography.Text>
            </div>
          ) : null}
          {entry.notes ? (
            <div>
              <Typography.Text type="secondary">
                {t('tenants.settingsChange.audit.notes', { notes: entry.notes })}
              </Typography.Text>
            </div>
          ) : null}

          <Space wrap size="small" style={{ marginTop: 8 }}>
            {entry.status === 'pending' && onApprove ? (
              <Button
                type="link"
                size="small"
                disabled={isOwn || busy}
                title={
                  isOwn
                    ? t('tenants.settingsChange.messages.selfApprovalForbidden')
                    : undefined
                }
                onClick={() => onApprove(entry)}
              >
                {t('tenants.settingsChange.actions.approve')}
              </Button>
            ) : null}
            {entry.status === 'pending' && onReject ? (
              <Button
                type="link"
                size="small"
                danger
                disabled={isOwn || busy}
                title={
                  isOwn
                    ? t('tenants.settingsChange.messages.selfApprovalForbidden')
                    : undefined
                }
                onClick={() => onReject(entry)}
              >
                {t('tenants.settingsChange.actions.reject')}
              </Button>
            ) : null}
            {entry.status === 'approved' && onRevert ? (
              <Button
                type="link"
                size="small"
                disabled={busy}
                onClick={() => onRevert(entry)}
              >
                {t('tenants.settingsChange.actions.revert')}
              </Button>
            ) : null}
          </Space>
        </div>
      ),
    };
  });

  return (
    <Card title={t('tenants.settingsChange.audit.title')}>
      {loading ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin />
        </div>
      ) : history.length === 0 ? (
        <Typography.Text type="secondary">
          {t('tenants.settingsChange.history.empty')}
        </Typography.Text>
      ) : (
        <Timeline items={items} />
      )}
    </Card>
  );
}
