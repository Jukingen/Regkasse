'use client';

import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button,
  DatePicker,
  Descriptions,
  Drawer,
  Input,
  Select,
  Space,
  Table,
  Typography,
} from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import { useEffect, useMemo, useRef, useState } from 'react';

import {
  fetchAllFiskalyHistory,
  getFiskalyHistory,
  getFiskalyHistoryById,
  retryFiskalyHistory,
  type FiskalyHistoryDetail,
  type FiskalyHistoryListItem,
  type FiskalyHistoryPaged,
  type FiskalyHistoryQuery,
} from '@/features/fiskaly/api/fiskalyHistory';
import {
  FiskalyOperationLivePanel,
  FiskalyOperationStatusBadge,
} from '@/features/fiskaly/components/FiskalyOperationStatusBadge';
import {
  canRetryFiskalyHistoryStatus,
  FISKALY_STATUS_POLL_MS,
  isFiskalyHistoryInFlight,
  isFiskalyHistoryTerminal,
  type FiskalyOperationStatusEvent,
} from '@/features/fiskaly/fiskalyOperationStatus';
import { useFiskalyOperationStatusLive } from '@/features/fiskaly/hooks/useFiskalyOperationStatusLive';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { downloadCsvText, rowsToCsv } from '@/shared/utils/csv';

const OPERATION_TYPES = [
  'normal',
  'cancel',
  'nullbeleg',
  'startbeleg',
  'monatsbeleg',
  'jahresbeleg',
  'schlussbeleg',
  'tagesabschluss',
] as const;

const HISTORY_QUERY_KEY = ['admin', 'fiskaly', 'history'] as const;

type KindKey =
  | 'tseFiskaly.operations.kinds.normal'
  | 'tseFiskaly.operations.kinds.cancel'
  | 'tseFiskaly.operations.kinds.nullbeleg'
  | 'tseFiskaly.operations.kinds.startbeleg'
  | 'tseFiskaly.operations.kinds.monatsbeleg'
  | 'tseFiskaly.operations.kinds.jahresbeleg'
  | 'tseFiskaly.operations.kinds.schlussbeleg'
  | 'tseFiskaly.operations.kinds.tagesabschluss';

const KIND_KEYS: Record<string, KindKey> = {
  normal: 'tseFiskaly.operations.kinds.normal',
  cancel: 'tseFiskaly.operations.kinds.cancel',
  nullbeleg: 'tseFiskaly.operations.kinds.nullbeleg',
  startbeleg: 'tseFiskaly.operations.kinds.startbeleg',
  monatsbeleg: 'tseFiskaly.operations.kinds.monatsbeleg',
  jahresbeleg: 'tseFiskaly.operations.kinds.jahresbeleg',
  schlussbeleg: 'tseFiskaly.operations.kinds.schlussbeleg',
  tagesabschluss: 'tseFiskaly.operations.kinds.tagesabschluss',
};

function prettyJson(raw: string | null | undefined): string {
  if (!raw) return '—';
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

function patchHistoryCaches(
  queryClient: ReturnType<typeof useQueryClient>,
  evt: FiskalyOperationStatusEvent
) {
  queryClient.setQueriesData<FiskalyHistoryPaged>({ queryKey: HISTORY_QUERY_KEY }, (old) => {
    if (!old?.items) return old;
    const idx = old.items.findIndex((item) => item.id === evt.id);
    if (idx < 0) return old;
    const next = [...old.items];
    const current = next[idx];
    next[idx] = {
      ...current,
      status: evt.status,
      progressPercent: evt.progressPercent,
      receiptNumber: evt.receiptNumber ?? current.receiptNumber,
      errorCode: evt.errorCode ?? current.errorCode,
      errorMessage: evt.errorMessage ?? current.errorMessage,
      completedAtUtc: evt.completedAtUtc ?? current.completedAtUtc,
    };
    return { ...old, items: next };
  });
  queryClient.setQueryData<FiskalyHistoryDetail>([...HISTORY_QUERY_KEY, 'detail', evt.id], (old) =>
    old
      ? {
          ...old,
          status: evt.status,
          progressPercent: evt.progressPercent,
          receiptNumber: evt.receiptNumber ?? old.receiptNumber,
          errorCode: evt.errorCode ?? old.errorCode,
          errorMessage: evt.errorMessage ?? old.errorMessage,
          completedAtUtc: evt.completedAtUtc ?? old.completedAtUtc,
        }
      : old
  );
}

export function FiskalyOperationHistory() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const { hasPermission } = usePermissions();
  const queryClient = useQueryClient();
  const canRetry = hasPermission(PERMISSIONS.FISKALY_HISTORY_RETRY);
  const isSuperAdmin = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [operationType, setOperationType] = useState<string | undefined>(undefined);
  const [status, setStatus] = useState<string | undefined>(undefined);
  const [search, setSearch] = useState('');
  const [searchApplied, setSearchApplied] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const listParams = useMemo((): FiskalyHistoryQuery => {
    return {
      fromUtc: range?.[0]?.startOf('day').toISOString(),
      toUtc: range?.[1]?.endOf('day').toISOString(),
      operationType,
      status,
      search: searchApplied.trim() || undefined,
      page,
      pageSize,
    };
  }, [range, operationType, status, searchApplied, page, pageSize]);

  const { connected: liveConnected } = useFiskalyOperationStatusLive({
    onEvent: (evt) => {
      patchHistoryCaches(queryClient, evt);
      const known = queryClient
        .getQueriesData<FiskalyHistoryPaged>({ queryKey: HISTORY_QUERY_KEY })
        .some(([, data]) => data?.items?.some((item) => item.id === evt.id));
      if (!known) {
        void queryClient.invalidateQueries({ queryKey: HISTORY_QUERY_KEY });
      }
    },
  });

  const listQuery = useQuery({
    queryKey: [...HISTORY_QUERY_KEY, listParams],
    queryFn: ({ signal }) => getFiskalyHistory(listParams, signal),
    placeholderData: keepPreviousData,
    refetchInterval: (query) => {
      const items = query.state.data?.items ?? [];
      if (!liveConnected || items.some((item) => isFiskalyHistoryInFlight(item.status))) {
        return FISKALY_STATUS_POLL_MS;
      }
      return false;
    },
  });

  const detailQuery = useQuery({
    queryKey: [...HISTORY_QUERY_KEY, 'detail', selectedId],
    queryFn: ({ signal }) => getFiskalyHistoryById(selectedId!, signal),
    enabled: Boolean(selectedId),
    refetchInterval: (query) =>
      isFiskalyHistoryInFlight(query.state.data?.status) || !liveConnected
        ? FISKALY_STATUS_POLL_MS
        : false,
  });

  const seenStatuses = useRef(new Map<string, string>());
  const primed = useRef(false);
  const suppressCompleteToastUntil = useRef(0);

  useEffect(() => {
    const items = listQuery.data?.items ?? [];
    if (!listQuery.isSuccess) return;
    if (!primed.current) {
      for (const item of items) seenStatuses.current.set(item.id, item.status);
      primed.current = true;
      return;
    }
    for (const item of items) {
      const previous = seenStatuses.current.get(item.id);
      seenStatuses.current.set(item.id, item.status);
      if (previous && isFiskalyHistoryInFlight(previous) && isFiskalyHistoryTerminal(item.status)) {
        if (Date.now() < suppressCompleteToastUntil.current) continue;
        if (item.status === 'Success') {
          notify.success(t('tseFiskaly.history.completedToast'));
        } else {
          notify.error(item.errorMessage || t('tseFiskaly.history.failedToast'));
        }
      }
    }
  }, [listQuery.data, listQuery.isSuccess, notify, t]);

  const retryMutation = useMutation({
    mutationFn: (id: string) => retryFiskalyHistory(id),
    onSuccess: async (result) => {
      suppressCompleteToastUntil.current = Date.now() + 4000;
      await queryClient.invalidateQueries({ queryKey: HISTORY_QUERY_KEY });
      if (result.operation.success) {
        notify.success(t('tseFiskaly.history.retrySuccess'));
      } else {
        notify.error(result.operation.error?.message || t('tseFiskaly.history.retryFailed'));
      }
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'FiskalyHistory.retry', fallbackKey: 'tseFiskaly.history.retryFailed' });
    },
  });

  const confirmRetry = (id: string) => {
    modal.confirm({
      title: t('tseFiskaly.history.retryConfirmTitle'),
      content: t('tseFiskaly.history.retryConfirmBody'),
      onOk: () => retryMutation.mutateAsync(id),
    });
  };

  const exportCsv = async () => {
    try {
      const rows = await fetchAllFiskalyHistory({
        fromUtc: listParams.fromUtc,
        toUtc: listParams.toUtc,
        operationType: listParams.operationType,
        status: listParams.status,
        search: listParams.search,
      });
      if (rows.length === 0) {
        notify.info(t('tseFiskaly.history.exportEmpty'));
        return;
      }
      const header = [
        t('tseFiskaly.history.colDate'),
        t('tseFiskaly.history.colType'),
        t('tseFiskaly.history.colRegister'),
        t('tseFiskaly.history.colStatus'),
        t('tseFiskaly.history.colReceipt'),
        t('tseFiskaly.history.colUser'),
        ...(isSuperAdmin ? [t('tseFiskaly.history.colTenant')] : []),
      ];
      const csv = rowsToCsv([
        header,
        ...rows.map((r) => [
          r.createdAtUtc,
          r.operationType,
          r.cashRegisterName ?? r.cashRegisterId,
          r.status,
          r.receiptNumber ?? '',
          r.userDisplayName ?? r.userId,
          ...(isSuperAdmin ? [r.tenantName ?? r.tenantId] : []),
        ]),
      ]);
      downloadCsvText(csv, `fiskaly-operation-history.csv`);
    } catch (err) {
      notify.apiError(err, { logContext: 'FiskalyHistory.export', fallbackKey: 'tseFiskaly.history.loadFailed' });
    }
  };

  const columns: ColumnsType<FiskalyHistoryListItem> = [
    {
      title: t('tseFiskaly.history.colDate'),
      dataIndex: 'createdAtUtc',
      render: (value: string) => formatDateTime(value, formatLocale),
    },
    {
      title: t('tseFiskaly.history.colType'),
      dataIndex: 'operationType',
      render: (value: string) => t(KIND_KEYS[value] ?? 'tseFiskaly.operations.kinds.normal'),
    },
    {
      title: t('tseFiskaly.history.colRegister'),
      dataIndex: 'cashRegisterName',
      render: (_: unknown, row) => row.cashRegisterName || row.cashRegisterId,
    },
    {
      title: t('tseFiskaly.history.colStatus'),
      dataIndex: 'status',
      render: (value: string) => <FiskalyOperationStatusBadge status={value} />,
    },
    {
      title: t('tseFiskaly.history.colReceipt'),
      dataIndex: 'receiptNumber',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('tseFiskaly.history.colUser'),
      dataIndex: 'userDisplayName',
      render: (_: unknown, row) => row.userDisplayName || row.userId,
    },
    ...(isSuperAdmin
      ? [
          {
            title: t('tseFiskaly.history.colTenant'),
            dataIndex: 'tenantName',
            render: (_: unknown, row: FiskalyHistoryListItem) => row.tenantName || row.tenantId,
          } satisfies ColumnsType<FiskalyHistoryListItem>[number],
        ]
      : []),
    {
      title: t('tseFiskaly.history.retry'),
      key: 'actions',
      width: 140,
      render: (_: unknown, row) =>
        canRetry && canRetryFiskalyHistoryStatus(row.status) ? (
          <Button
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              confirmRetry(row.id);
            }}
            loading={retryMutation.isPending && retryMutation.variables === row.id}
          >
            {t('tseFiskaly.history.retry')}
          </Button>
        ) : null,
    },
  ];

  const pagination: TablePaginationConfig = {
    current: page,
    pageSize,
    total: listQuery.data?.totalCount ?? 0,
    showSizeChanger: true,
    onChange: (nextPage, nextSize) => {
      setPage(nextPage);
      setPageSize(nextSize);
    },
  };

  const detail = detailQuery.data;

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap>
        <DatePicker.RangePicker
          format={DAYJS_DATE_FORMAT}
          value={range}
          onChange={(next) => {
            setRange(next as [Dayjs, Dayjs] | null);
            setPage(1);
          }}
          placeholder={[t('tseFiskaly.history.filterDate'), t('tseFiskaly.history.filterDate')]}
        />
        <Select
          allowClear
          placeholder={t('tseFiskaly.history.filterType')}
          style={{ minWidth: 180 }}
          value={operationType}
          onChange={(value) => {
            setOperationType(value);
            setPage(1);
          }}
          options={OPERATION_TYPES.map((op) => ({
            value: op,
            label: t(KIND_KEYS[op]),
          }))}
        />
        <Select
          allowClear
          placeholder={t('tseFiskaly.history.filterStatus')}
          style={{ minWidth: 160 }}
          value={status}
          onChange={(value) => {
            setStatus(value);
            setPage(1);
          }}
          options={[
            { value: 'Success', label: t('tseFiskaly.history.statusCompleted') },
            { value: 'Failed', label: t('tseFiskaly.history.statusFailed') },
            { value: 'Pending', label: t('tseFiskaly.history.statusPending') },
            { value: 'Processing', label: t('tseFiskaly.history.statusProcessing') },
          ]}
        />
        <Input.Search
          allowClear
          placeholder={t('tseFiskaly.history.filterSearch')}
          style={{ minWidth: 240 }}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onSearch={(value) => {
            setSearchApplied(value);
            setPage(1);
          }}
        />
        <Button icon={<ReloadOutlined />} onClick={() => void listQuery.refetch()}>
          {t('common.buttons.refresh')}
        </Button>
        <Button icon={<DownloadOutlined />} onClick={() => void exportCsv()}>
          {t('tseFiskaly.history.exportCsv')}
        </Button>
        <Typography.Text type="secondary">
          {liveConnected ? t('tseFiskaly.history.liveRealtime') : t('tseFiskaly.history.livePolling')}
        </Typography.Text>
      </Space>

      {listQuery.isError ? (
        <Typography.Text type="danger">{t('tseFiskaly.history.loadFailed')}</Typography.Text>
      ) : null}

      <Table<FiskalyHistoryListItem>
        rowKey="id"
        loading={listQuery.isPending}
        columns={columns}
        dataSource={listQuery.data?.items ?? []}
        pagination={pagination}
        locale={{ emptyText: t('tseFiskaly.history.empty') }}
        onRow={(row) => ({
          onClick: () => setSelectedId(row.id),
          style: { cursor: 'pointer' },
        })}
      />

      <Drawer
        title={t('tseFiskaly.history.detailTitle')}
        open={Boolean(selectedId)}
        onClose={() => setSelectedId(null)}
        size="large"
        extra={
          canRetry && detail && canRetryFiskalyHistoryStatus(detail.status) ? (
            <Button type="primary" onClick={() => confirmRetry(detail.id)} loading={retryMutation.isPending}>
              {t('tseFiskaly.history.retry')}
            </Button>
          ) : null
        }
      >
        {detailQuery.isFetching && !detail ? (
          <Typography.Text>{t('common.loading.data')}</Typography.Text>
        ) : null}
        {detail ? (
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            {isFiskalyHistoryInFlight(detail.status) ? (
              <FiskalyOperationLivePanel
                status={detail.status}
                progressPercent={detail.progressPercent}
                hint={t('tseFiskaly.operations.progressHint')}
              />
            ) : (
              <FiskalyOperationLivePanel status={detail.status} progressPercent={detail.progressPercent} />
            )}
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label={t('tseFiskaly.history.colDate')}>
                {formatDateTime(detail.createdAtUtc, formatLocale)}
              </Descriptions.Item>
              <Descriptions.Item label={t('tseFiskaly.history.colType')}>
                {t(KIND_KEYS[detail.operationType] ?? 'tseFiskaly.operations.kinds.normal')}
              </Descriptions.Item>
              <Descriptions.Item label={t('tseFiskaly.history.colRegister')}>
                {detail.cashRegisterName || detail.cashRegisterId}
              </Descriptions.Item>
              <Descriptions.Item label={t('tseFiskaly.history.colStatus')}>
                <FiskalyOperationStatusBadge status={detail.status} />
              </Descriptions.Item>
              <Descriptions.Item label={t('tseFiskaly.history.colReceipt')}>
                {detail.receiptNumber || '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('tseFiskaly.history.colUser')}>
                {detail.userDisplayName || detail.userId}
              </Descriptions.Item>
              {isSuperAdmin ? (
                <Descriptions.Item label={t('tseFiskaly.history.colTenant')}>
                  {detail.tenantName || detail.tenantId}
                </Descriptions.Item>
              ) : null}
              <Descriptions.Item label={t('tseFiskaly.history.errorCode')}>
                {detail.errorCode || '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('tseFiskaly.history.errorMessage')}>
                {detail.errorMessage || '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('tseFiskaly.history.retryCount')}>{detail.retryCount}</Descriptions.Item>
              <Descriptions.Item label={t('tseFiskaly.history.retriedFrom')}>
                {detail.retriedFromId || '—'}
              </Descriptions.Item>
            </Descriptions>
            <div>
              <Typography.Text strong>{t('tseFiskaly.history.requestPayload')}</Typography.Text>
              <pre style={{ whiteSpace: 'pre-wrap', marginTop: 8 }}>{prettyJson(detail.requestPayloadJson)}</pre>
            </div>
            <div>
              <Typography.Text strong>{t('tseFiskaly.history.responsePayload')}</Typography.Text>
              <pre style={{ whiteSpace: 'pre-wrap', marginTop: 8 }}>{prettyJson(detail.responsePayloadJson)}</pre>
            </div>
          </Space>
        ) : null}
      </Drawer>
    </Space>
  );
}
