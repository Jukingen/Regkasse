'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button,
  Card,
  DatePicker,
  Descriptions,
  Empty,
  Input,
  Modal,
  Select,
  Space,
  Statistic,
  Table,
  Timeline,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import { useParams, useRouter } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';

import type { FinanzOnlineOutboxItemDto } from '@/api/generated/model/finanzOnlineOutboxItemDto';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n/I18nProvider';
import { translateApiError } from '@/lib/api/errorTranslator';

import {
  OUTBOX_FAILED_STATUSES,
  type OutboxStatusGroup,
  type OutboxUiDetail,
  fetchFinanzOnlineOutboxDetail,
  fetchFinanzOnlineOutboxList,
  markOutboxResolved,
  retryAllFailedOutbox,
  retryOutboxMessage,
  retryOutboxMessages,
} from '../api/finanzOnlineOutboxUi';
import { maskOutboxPayload } from '../utils/maskOutboxPayload';

const PAGE_SIZE = 50;

function lastAttemptedAt(row: FinanzOnlineOutboxItemDto): string | null | undefined {
  return row.processedAtUtc ?? row.nextAttemptAtUtc ?? row.createdAtUtc;
}

function summarize(items: FinanzOnlineOutboxItemDto[]) {
  const since = Date.now() - 7 * 24 * 60 * 60 * 1000;
  const window = items.filter((item) => {
    const created = item.createdAtUtc ? new Date(item.createdAtUtc).getTime() : 0;
    return created >= since;
  });
  const success = window.filter((item) => item.status === 'ProtocolSuccess').length;
  const attempts = window.reduce((sum, item) => sum + (item.attemptCount ?? 0), 0);
  const failures = new Map<string, number>();
  for (const item of window) {
    if (!item.status || !OUTBOX_FAILED_STATUSES.has(item.status)) continue;
    const key = item.messageType ?? 'unknown';
    failures.set(key, (failures.get(key) ?? 0) + 1);
  }
  const top = [...failures.entries()].sort((a, b) => b[1] - a[1]).slice(0, 3);
  return {
    count: window.length,
    successRate: window.length === 0 ? 0 : Math.round((success / window.length) * 100),
    averageRetries: window.length === 0 ? 0 : Math.round((attempts / window.length) * 10) / 10,
    top,
  };
}

export function FinanzOnlineOutboxInbox() {
  const { t } = useI18n();
  const notify = useNotify();
  const { isSuperAdmin } = usePermissions();
  const router = useRouter();
  const params = useParams<{ id?: string }>();
  const queryClient = useQueryClient();
  const routeId = typeof params?.id === 'string' ? params.id : undefined;

  const [status, setStatus] = useState<OutboxStatusGroup | undefined>();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [registerId, setRegisterId] = useState('');
  const [resolveReason, setResolveReason] = useState('');
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [openId, setOpenId] = useState<string | undefined>(routeId);

  useEffect(() => {
    if (routeId) setOpenId(routeId);
  }, [routeId]);

  const listQuery = useQuery({
    queryKey: [
      'finanz-online-outbox-inbox',
      status,
      range?.[0]?.toISOString(),
      range?.[1]?.toISOString(),
    ],
    queryFn: () =>
      fetchFinanzOnlineOutboxList({
        status,
        fromUtc: range?.[0]?.startOf('day').toISOString(),
        toUtc: range?.[1]?.endOf('day').toISOString(),
      }),
  });

  const detailQuery = useQuery({
    queryKey: ['finanz-online-outbox-detail', openId],
    queryFn: () => fetchFinanzOnlineOutboxDetail(openId as string),
    enabled: Boolean(openId),
  });

  const items = useMemo(() => {
    const rows = listQuery.data?.items ?? [];
    const needle = registerId.trim().toLowerCase();
    if (!needle) return rows;
    return rows.filter((row) => (row.registerId ?? '').toLowerCase().includes(needle));
  }, [listQuery.data?.items, registerId]);

  const stats = useMemo(() => summarize(listQuery.data?.items ?? []), [listQuery.data?.items]);

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['finanz-online-outbox-inbox'] });
  };

  const retryOne = useMutation({
    mutationFn: (id: string) => retryOutboxMessage(id),
    onSuccess: () => {
      notify.success(t('finanzOnline.outbox.retryQueued'));
      invalidate();
    },
    onError: (error: unknown) => {
      notify.error(
        translateApiError(t, error, {
          logContext: 'FinanzOnlineOutbox.retry',
          fallbackKey: 'finanzOnline.outbox.retryError',
        })
      );
    },
  });

  const retrySelected = useMutation({
    mutationFn: (ids: string[]) => retryOutboxMessages(ids),
    onSuccess: () => {
      notify.success(t('finanzOnline.outbox.retryQueued'));
      setSelectedIds([]);
      invalidate();
    },
    onError: (error: unknown) => {
      notify.error(
        translateApiError(t, error, {
          logContext: 'FinanzOnlineOutbox.retrySelected',
          fallbackKey: 'finanzOnline.outbox.retryError',
        })
      );
    },
  });

  const retryFailed = useMutation({
    mutationFn: () =>
      retryAllFailedOutbox({
        cashRegisterId: registerId.trim() || undefined,
        fromUtc: range?.[0]?.startOf('day').toISOString(),
        toUtc: range?.[1]?.endOf('day').toISOString(),
      }),
    onSuccess: () => {
      notify.success(t('finanzOnline.outbox.retryQueued'));
      invalidate();
    },
    onError: (error: unknown) => {
      notify.error(
        translateApiError(t, error, {
          logContext: 'FinanzOnlineOutbox.retryFailed',
          fallbackKey: 'finanzOnline.outbox.retryError',
        })
      );
    },
  });

  const resolveOne = useMutation({
    mutationFn: (input: { id: string; reason: string }) =>
      markOutboxResolved(input.id, input.reason),
    onSuccess: () => {
      notify.success(t('finanzOnline.outbox.resolved'));
      invalidate();
    },
    onError: (error: unknown) => {
      notify.error(
        translateApiError(t, error, {
          logContext: 'FinanzOnlineOutbox.resolve',
          fallbackKey: 'finanzOnline.outbox.retryError',
        })
      );
    },
  });

  const openDetail = (id: string) => {
    setResolveReason('');
    setOpenId(id);
    router.push(`/rksv/finanz-online-outbox/${id}`);
  };

  const columns: ColumnsType<FinanzOnlineOutboxItemDto> = [
    { title: t('finanzOnline.outbox.columns.messageType'), dataIndex: 'messageType' },
    { title: t('finanzOnline.outbox.columns.status'), dataIndex: 'status' },
    { title: t('finanzOnline.outbox.columns.attempts'), dataIndex: 'attemptCount' },
    {
      title: t('finanzOnline.outbox.columns.lastAttemptedAt'),
      render: (_, row) => lastAttemptedAt(row) ?? '—',
    },
    {
      title: t('finanzOnline.outbox.columns.errorCode'),
      dataIndex: 'lastErrorCode',
      render: (value) => value ?? '—',
    },
    {
      title: t('finanzOnline.outbox.actions.details'),
      render: (_, row) =>
        row.outboxId ? (
          <Space>
            <Button size="small" onClick={() => openDetail(row.outboxId as string)}>
              {t('finanzOnline.outbox.actions.details')}
            </Button>
            <Button
              size="small"
              data-testid={`fon-outbox-retry-${row.outboxId}`}
              loading={retryOne.isPending}
              disabled={row.status === 'ProtocolSuccess' || row.status === 'AwaitingProtocol'}
              onClick={() => retryOne.mutate(row.outboxId as string)}
            >
              {t('finanzOnline.outbox.actions.retry')}
            </Button>
          </Space>
        ) : null,
    },
  ];

  const detail = detailQuery.data;
  const masked = detail
    ? JSON.stringify(maskOutboxPayload(buildDisplayPayload(detail)), null, 2)
    : '';

  return (
    <Card
      title={t('finanzOnline.outbox.title')}
      style={{ marginBottom: 16 }}
      data-testid="fon-outbox-inbox"
    >
      <Space wrap style={{ marginBottom: 12 }}>
        <Select
          allowClear
          placeholder={t('finanzOnline.outbox.filters.status')}
          style={{ width: 160 }}
          value={status}
          onChange={(value) => setStatus(value)}
          options={[
            { value: 'Pending', label: t('finanzOnline.outbox.filters.statusPending') },
            { value: 'Sent', label: t('finanzOnline.outbox.filters.statusSent') },
            { value: 'Failed', label: t('finanzOnline.outbox.filters.statusFailed') },
          ]}
        />
        <DatePicker.RangePicker
          value={range}
          onChange={(value) => setRange(value)}
          placeholder={[
            t('finanzOnline.outbox.filters.dateRange'),
            t('finanzOnline.outbox.filters.dateRange'),
          ]}
        />
        <Input
          allowClear
          style={{ width: 220 }}
          placeholder={t('finanzOnline.outbox.filters.cashRegister')}
          value={registerId}
          onChange={(event) => setRegisterId(event.target.value)}
        />
        <Button
          disabled={selectedIds.length === 0}
          loading={retrySelected.isPending}
          onClick={() => retrySelected.mutate(selectedIds)}
        >
          {t('finanzOnline.outbox.actions.retrySelected')}
        </Button>
        <Button loading={retryFailed.isPending} onClick={() => retryFailed.mutate()}>
          {t('finanzOnline.outbox.actions.retryAllFailed')}
        </Button>
      </Space>

      <Table<FinanzOnlineOutboxItemDto>
        size="small"
        rowKey={(row) => row.outboxId ?? `${row.businessKey}-${row.createdAtUtc}`}
        columns={columns}
        dataSource={items}
        loading={listQuery.isLoading}
        locale={{ emptyText: t('finanzOnline.outbox.empty') }}
        pagination={{ pageSize: PAGE_SIZE, showSizeChanger: true }}
        rowSelection={{
          selectedRowKeys: selectedIds,
          onChange: (keys) => setSelectedIds(keys.map(String)),
        }}
      />

      <Card
        size="small"
        title={t('finanzOnline.outbox.analytics.title')}
        style={{ marginTop: 16 }}
        data-testid="fon-outbox-analytics"
      >
        {stats.count === 0 ? (
          <Empty description={t('finanzOnline.outbox.analytics.empty')} />
        ) : (
          <Space size="large" wrap>
            <Statistic
              title={t('finanzOnline.outbox.analytics.successRate')}
              value={stats.successRate}
              suffix="%"
            />
            <Statistic
              title={t('finanzOnline.outbox.analytics.averageRetries')}
              value={stats.averageRetries}
            />
            <div>
              <Typography.Text type="secondary">
                {t('finanzOnline.outbox.analytics.topFailures')}
              </Typography.Text>
              {stats.top.length === 0 ? (
                <div>—</div>
              ) : (
                stats.top.map(([type, count]) => (
                  <div key={type}>
                    {type}: {count}
                  </div>
                ))
              )}
            </div>
          </Space>
        )}
      </Card>

      <Modal
        title={t('finanzOnline.outbox.detail.title')}
        open={Boolean(openId)}
        onCancel={() => {
          setOpenId(undefined);
          setResolveReason('');
          router.push('/rksv/finanz-online-outbox');
        }}
        destroyOnHidden
        footer={
          <Space>
            <Button
              data-testid="fon-outbox-detail-retry"
              loading={retryOne.isPending}
              onClick={() => openId && retryOne.mutate(openId)}
            >
              {t('finanzOnline.outbox.actions.retry')}
            </Button>
            {isSuperAdmin ? (
              <Button
                loading={resolveOne.isPending}
                disabled={!resolveReason.trim() || !openId}
                onClick={() =>
                  openId && resolveOne.mutate({ id: openId, reason: resolveReason.trim() })
                }
              >
                {t('finanzOnline.outbox.actions.resolve')}
              </Button>
            ) : null}
          </Space>
        }
      >
        {detail ? (
          <div data-testid="fon-outbox-detail">
            <Descriptions size="small" column={1}>
              <Descriptions.Item label={t('finanzOnline.outbox.detail.errorCode')}>
                {detail.lastErrorCode ?? '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('finanzOnline.outbox.detail.errorMessage')}>
                {detail.lastErrorSummary ?? '—'}
              </Descriptions.Item>
            </Descriptions>
            {isSuperAdmin ? (
              <Input.TextArea
                style={{ marginTop: 12 }}
                rows={2}
                value={resolveReason}
                onChange={(event) => setResolveReason(event.target.value)}
                placeholder={t('finanzOnline.outbox.detail.resolveReason')}
              />
            ) : null}
            <Typography.Title level={5}>
              {t('finanzOnline.outbox.detail.timeline')}
            </Typography.Title>
            <Timeline
              items={[
                {
                  children: `${t('finanzOnline.outbox.timeline.created')}: ${detail.createdAtUtc ?? '—'}`,
                },
                {
                  children: t('finanzOnline.outbox.timeline.attempts', {
                    count: detail.attemptCount ?? 0,
                  }),
                },
                {
                  children: `${t('finanzOnline.outbox.timeline.next')}: ${detail.nextAttemptAtUtc ?? '—'}`,
                },
                {
                  children: `${t('finanzOnline.outbox.timeline.processed')}: ${detail.processedAtUtc ?? '—'}`,
                },
              ]}
            />
            <Typography.Title level={5}>{t('finanzOnline.outbox.detail.payload')}</Typography.Title>
            <pre data-testid="fon-outbox-payload">{masked}</pre>
          </div>
        ) : null}
      </Modal>
    </Card>
  );
}

function buildDisplayPayload(detail: OutboxUiDetail): unknown {
  return {
    messageType: detail.messageType,
    status: detail.status,
    registerId: detail.registerId,
    protocolSummary: detail.protocolSummary,
    lastResponsePreview: detail.lastResponsePreview,
    tseSignature: '***',
    participantCredentials: '***',
    payload: detail.payload,
  };
}
