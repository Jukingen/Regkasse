'use client';

/**
 * Compact role-scoped permission history for RoleManagementDrawer tab.
 * Last 20 changes, date filter, 7-day summary, color coding, CSV export.
 */
import { DownloadOutlined, HistoryOutlined } from '@ant-design/icons';
import { Alert, Button, DatePicker, Empty, Space, Spin, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import React, { useMemo, useState } from 'react';

import {
  type PermissionAuditEntry,
  usePermissionAudit,
} from '@/features/audit/hooks/usePermissionAudit';
import { SimpleList as List } from '@/components/ui/SimpleList';
import {
  permissionAuditBorderColor,
  type PermissionAuditColor,
} from '@/features/users/utils/permissionAuditDiff';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { PERMISSIONS } from '@/shared/auth/permissions';

const PAGE_SIZE = 20;

type Props = {
  roleName: string;
  roleId?: string | null;
  subjectLabel?: string | null;
};

function stateLabel(
  state: string | null | undefined,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  if (!state) return '—';
  const known = ['allowed', 'denied', 'individual', 'absent', 'defaults'] as const;
  if ((known as readonly string[]).includes(state)) {
    return t(`users.permissionAudit.state.${state}`);
  }
  return state;
}

function colorForEntry(entry: PermissionAuditEntry): PermissionAuditColor {
  if (entry.action === 'created' || entry.action === 'deleted') return 'blue';
  if (entry.action === 'reverted') return 'yellow';
  const oldV = (entry.oldValue ?? '').toLowerCase();
  const newV = (entry.newValue ?? '').toLowerCase();
  const added =
    (oldV === 'absent' || oldV === '' || oldV === 'denied') &&
    (newV === 'allowed' || newV === 'individual');
  const removed =
    (oldV === 'allowed' || oldV === 'individual') &&
    (newV === 'absent' || newV === '' || newV === 'denied');
  if (added) return 'green';
  if (removed) return 'red';
  return 'yellow';
}

function colorEmoji(color: PermissionAuditColor): string {
  switch (color) {
    case 'green':
      return '🟢';
    case 'red':
      return '🔴';
    case 'blue':
      return '🔵';
    case 'yellow':
    default:
      return '🟡';
  }
}

function downloadCsv(entries: PermissionAuditEntry[], filename: string): void {
  const header = [
    'timestamp',
    'actor',
    'permission',
    'old_value',
    'new_value',
    'action',
    'role',
  ];
  const rows = entries.map((e) =>
    [
      e.timestamp,
      e.actorName || e.actorEmail || e.actorUserId,
      e.permissionKey,
      e.oldValue ?? '',
      e.newValue ?? '',
      e.action,
      e.roleName,
    ]
      .map((cell) => {
        const s = String(cell ?? '');
        if (s.includes('"') || s.includes(',') || s.includes('\n')) {
          return `"${s.replace(/"/g, '""')}"`;
        }
        return s;
      })
      .join(',')
  );
  const blob = new Blob([[header.join(','), ...rows].join('\n')], {
    type: 'text/csv;charset=utf-8',
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export function RolePermissionHistoryTab({ roleName, roleId, subjectLabel }: Props) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const { hasPermission } = usePermissions();
  const canView = hasPermission(PERMISSIONS.AUDIT_VIEW);
  const canExport =
    hasPermission(PERMISSIONS.AUDIT_EXPORT) || hasPermission(PERMISSIONS.AUDIT_VIEW);

  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);

  const listParams = useMemo(
    () => ({
      roleId: roleId || undefined,
      roleName,
      fromDate: range?.[0]?.startOf('day').toISOString(),
      toDate: range?.[1]?.endOf('day').toISOString(),
      page: 1,
      pageSize: PAGE_SIZE,
    }),
    [roleId, roleName, range]
  );

  const summaryParams = useMemo(
    () => ({
      roleId: roleId || undefined,
      roleName,
      fromDate: dayjs().subtract(7, 'day').startOf('day').toISOString(),
      toDate: dayjs().endOf('day').toISOString(),
      page: 1,
      pageSize: 1,
    }),
    [roleId, roleName]
  );

  const listQuery = usePermissionAudit(listParams, { enabled: canView && Boolean(roleName) });
  const summaryQuery = usePermissionAudit(summaryParams, {
    enabled: canView && Boolean(roleName),
  });

  const entries = listQuery.data?.items ?? [];
  const last7Count = summaryQuery.data?.totalCount ?? 0;

  if (!canView) {
    return (
      <Alert type="warning" showIcon title={t('users.permissionAudit.noAuditPermission')} />
    );
  }

  return (
    <div style={{ marginTop: 4 }}>
      <Space
        wrap
        style={{ width: '100%', justifyContent: 'space-between', marginBottom: 12 }}
        size={8}
      >
        <Typography.Text strong>
          <HistoryOutlined style={{ marginRight: 8 }} />
          {subjectLabel
            ? t('users.permissionAudit.titleWithSubject', { subject: subjectLabel })
            : t('users.roleDrawer.tabHistory')}
        </Typography.Text>
        <Space wrap size={8}>
          <DatePicker.RangePicker
            value={range}
            onChange={(v) => setRange(v as [Dayjs | null, Dayjs | null] | null)}
            allowClear
            style={{ maxWidth: 280 }}
            placeholder={[
              t('users.permissionAudit.filterDate'),
              t('users.permissionAudit.filterDate'),
            ]}
          />
          <Button
            icon={<DownloadOutlined />}
            disabled={!canExport || entries.length === 0}
            loading={listQuery.isFetching}
            onClick={() => {
              try {
                const safe = roleName.replace(/[^\w.-]+/g, '_');
                downloadCsv(entries, `permission-history-${safe}-${dayjs().format('YYYYMMDD')}.csv`);
                message.success(t('users.permissionAudit.exportSuccess'));
              } catch {
                message.error(t('users.permissionAudit.exportError'));
              }
            }}
          >
            {t('users.permissionAudit.exportCsv')}
          </Button>
        </Space>
      </Space>

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        title={t('users.permissionAudit.summaryLast7Days', { count: last7Count })}
      />

      <div style={{ marginBottom: 8, fontSize: 12, color: 'rgba(0,0,0,0.45)' }}>
        <Space size={12} wrap>
          <span>🟢 {t('users.permissionAudit.legendAdded')}</span>
          <span>🔴 {t('users.permissionAudit.legendRemoved')}</span>
          <span>🟡 {t('users.permissionAudit.legendChanged')}</span>
        </Space>
      </div>

      {listQuery.isLoading ? (
        <Spin />
      ) : listQuery.isError ? (
        <Alert type="error" showIcon title={t('users.permissionAudit.loadError')} />
      ) : entries.length === 0 ? (
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description={t('users.permissionAudit.empty')}
        />
      ) : (
        <List
          size="small"
          bordered
          dataSource={entries}
          style={{ maxHeight: 420, overflow: 'auto', background: '#fff' }}
          renderItem={(entry) => {
            const color = colorForEntry(entry);
            const border = permissionAuditBorderColor(color);
            return (
              <List.Item
                style={{
                  borderLeft: `4px solid ${border}`,
                  alignItems: 'flex-start',
                }}
              >
                <div style={{ width: '100%' }}>
                  <Space wrap size={[8, 4]} style={{ marginBottom: 4 }}>
                    <Typography.Text style={{ fontSize: 12 }}>
                      {colorEmoji(color)}{' '}
                      {entry.timestamp ? formatDateTime(entry.timestamp) : '—'}
                    </Typography.Text>
                    <Typography.Text strong style={{ fontSize: 12 }}>
                      👤 {entry.actorName?.trim() || t('users.permissionAudit.systemActor')}
                    </Typography.Text>
                  </Space>
                  <div style={{ fontSize: 12 }}>
                    <Typography.Text code>
                      {entry.permissionKey || entry.roleName || '—'}
                    </Typography.Text>
                    <span style={{ margin: '0 8px' }}>
                      {stateLabel(entry.oldValue, t)}
                      {' → '}
                      {stateLabel(entry.newValue, t)}
                    </span>
                  </div>
                </div>
              </List.Item>
            );
          }}
        />
      )}
    </div>
  );
}
