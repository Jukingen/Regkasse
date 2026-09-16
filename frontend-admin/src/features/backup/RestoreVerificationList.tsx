'use client';

import { SearchOutlined } from '@ant-design/icons';
import { Alert, Button, Card, DatePicker, Input, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import Link from 'next/link';
import { useMemo, useState } from 'react';

import { RestoreVerificationProgress } from '@/features/backup/components/RestoreVerificationProgress';
import { useRestoreVerificationRuns } from '@/features/backup/hooks/useRestoreVerificationRuns';
import type { RestoreVerificationRunDto } from '@/features/backup/logic/restoreVerificationApi';
import {
  restoreVerificationStatusColor,
  restoreVerificationStatusLabelKey,
  restoreVerificationTypeLabelKey,
  restoreVerificationVerdictColor,
  restoreVerificationVerdictLabelKey,
} from '@/features/backup/logic/restoreVerificationPresentation';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/lib/dateUtils';
import {
  ADMIN_RESTORE_VERIFICATION_PATH,
  restoreVerificationDetailPath,
} from '@/shared/backupAreaRoutes';

const PAGE_SIZE = 20;

export function RestoreVerificationList() {
  const { t } = useI18n();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<number | undefined>();
  const [triggerSource, setTriggerSource] = useState<number | undefined>();
  const [backupId, setBackupId] = useState('');
  const [appliedBackupId, setAppliedBackupId] = useState('');
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);

  const fromUtc = range?.[0]?.toISOString();
  const toUtc = range?.[1]?.endOf('day').toISOString();

  const query = useRestoreVerificationRuns({
    page,
    pageSize: PAGE_SIZE,
    status,
    triggerSource,
    fromUtc,
    toUtc,
    sourceBackupRunId: appliedBackupId.trim() || undefined,
  });

  const columns: ColumnsType<RestoreVerificationRunDto> = useMemo(
    () => [
      {
        title: t('backupDr.restoreVerificationPage.columns.date'),
        dataIndex: 'requestedAt',
        render: (iso: string) => formatDateTime(iso),
      },
      {
        title: t('backupDr.restoreVerificationPage.columns.backupId'),
        dataIndex: 'sourceBackupRunId',
        ellipsis: true,
        render: (id: string | null | undefined) =>
          id ? (
            <Typography.Text copyable={{ text: id }} style={{ fontSize: 12 }}>
              {id.slice(0, 8)}…
            </Typography.Text>
          ) : (
            '—'
          ),
      },
      {
        title: t('backupDr.restoreVerificationPage.columns.type'),
        dataIndex: 'triggerSource',
        width: 130,
        render: (trigger: RestoreVerificationRunDto['triggerSource']) => (
          <Tag>{t(restoreVerificationTypeLabelKey(trigger))}</Tag>
        ),
      },
      {
        title: t('backupDr.restoreVerificationPage.columns.status'),
        dataIndex: 'status',
        width: 140,
        render: (statusValue: RestoreVerificationRunDto['status']) => (
          <Tag color={restoreVerificationStatusColor(statusValue)}>
            {t(restoreVerificationStatusLabelKey(statusValue))}
          </Tag>
        ),
      },
      {
        title: t('backupDr.restoreVerificationPage.columns.verdict'),
        dataIndex: 'verdict',
        width: 140,
        render: (verdict: RestoreVerificationRunDto['verdict']) => (
          <Tag color={restoreVerificationVerdictColor(verdict)} style={{ fontWeight: 700 }}>
            {verdict === 'passed' ? '✅ ' : verdict === 'failed' ? '❌ ' : ''}
            {t(restoreVerificationVerdictLabelKey(verdict))}
          </Tag>
        ),
      },
      {
        title: t('backupDr.restoreVerificationPage.columns.actions'),
        key: 'actions',
        width: 120,
        render: (_: unknown, row) => (
          <Link href={restoreVerificationDetailPath(row.id, ADMIN_RESTORE_VERIFICATION_PATH)} prefetch={false}>
            {t('backupDr.restoreVerificationPage.openDetail')}
          </Link>
        ),
      },
    ],
    [t]
  );

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <RestoreVerificationProgress hideWhenIdle={false} />
      <Card size="small">
        <Space wrap>
          <Select
            allowClear
            placeholder={t('backupDr.restoreVerificationPage.filters.type')}
            style={{ minWidth: 160 }}
            value={triggerSource}
            onChange={(v) => {
              setTriggerSource(v);
              setPage(1);
            }}
            options={[
              { value: 0, label: t('backupDr.restoreVerificationPage.type.manual') },
              { value: 1, label: t('backupDr.restoreVerificationPage.type.scheduled') },
            ]}
          />
          <Select
            allowClear
            placeholder={t('backupDr.restoreVerificationPage.filters.status')}
            style={{ minWidth: 160 }}
            value={status}
            onChange={(v) => {
              setStatus(v);
              setPage(1);
            }}
            options={[
              { value: 1, label: t('backupDr.restoreVerificationPage.status.running') },
              { value: 2, label: t('backupDr.restoreVerificationPage.status.succeeded') },
              { value: 3, label: t('backupDr.restoreVerificationPage.status.failed') },
              { value: 0, label: t('backupDr.restoreVerificationPage.status.queued') },
            ]}
          />
          <DatePicker.RangePicker
            allowClear
            value={range}
            onChange={(next) => {
              setRange(next);
              setPage(1);
            }}
          />
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={t('backupDr.restoreVerificationPage.filters.backupId')}
            value={backupId}
            onChange={(e) => setBackupId(e.target.value)}
            onPressEnter={() => {
              setAppliedBackupId(backupId);
              setPage(1);
            }}
            style={{ minWidth: 240 }}
          />
          <Button
            type="primary"
            onClick={() => {
              setAppliedBackupId(backupId);
              setPage(1);
            }}
          >
            {t('backupDr.restoreVerificationPage.filters.apply')}
          </Button>
        </Space>
      </Card>
      {query.isError ? (
        <Alert type="error" showIcon title={t('backupDr.restoreVerificationPage.loadError')} />
      ) : null}
      <Table<RestoreVerificationRunDto>
        rowKey="id"
        columns={columns}
        dataSource={query.data?.items ?? []}
        loading={query.isFetching}
        pagination={{
          current: page,
          pageSize: PAGE_SIZE,
          total: query.data?.totalCount ?? 0,
          showSizeChanger: false,
          onChange: setPage,
        }}
        locale={{ emptyText: t('backupDr.restoreVerificationPage.empty') }}
      />
    </Space>
  );
}
