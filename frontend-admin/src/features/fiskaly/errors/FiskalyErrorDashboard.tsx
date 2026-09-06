'use client';

import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Input,
  Row,
  Segmented,
  Select,
  Skeleton,
  Space,
  Statistic,
  Table,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import dynamic from 'next/dynamic';
import { useMemo, useState } from 'react';

import {
  exportFiskalyErrors,
  getFiskalyErrorById,
  getFiskalyErrorStats,
  getFiskalyErrors,
  setFiskalyErrorReview,
  type FiskalyErrorQuery,
  type FiskalyErrorReviewStatus,
} from '@/features/fiskaly/api/fiskalyErrors';
import { FiskalyErrorDetailView } from '@/features/fiskaly/errors/FiskalyErrorDetail';
import { FiskalyErrorList } from '@/features/fiskaly/errors/FiskalyErrorList';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import { triggerBlobDownload } from '@/lib/download/exportDownload';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const FiskalyErrorCharts = dynamic(
  () => import('@/features/fiskaly/components/FiskalyErrorCharts'),
  { ssr: false, loading: () => <Skeleton active paragraph={{ rows: 8 }} /> }
);

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

const PIE_COLORS = ['#cf1322', '#fa541c', '#fa8c16', '#1677ff', '#722ed1', '#13c2c2', '#eb2f96'];
const QUERY_KEY = ['admin', 'fiskaly', 'errors'] as const;

function defaultRange(): [Dayjs, Dayjs] {
  return [dayjs().subtract(29, 'day').startOf('day'), dayjs().endOf('day')];
}

export function FiskalyErrorDashboard() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const isSuperAdmin = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const canResolve = isSuperAdmin && hasPermission(PERMISSIONS.FISKALY_HISTORY_RETRY);
  const { tenants, isLoading: tenantsLoading } = useTenantList({ enabled: isSuperAdmin });

  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange);
  const [operationType, setOperationType] = useState<string | undefined>(undefined);
  const [tenantId, setTenantId] = useState<string | undefined>(undefined);
  const [reviewStatus, setReviewStatus] = useState<FiskalyErrorReviewStatus | undefined>(undefined);
  const [errorCode, setErrorCode] = useState('');
  const [errorCodeApplied, setErrorCodeApplied] = useState('');
  const [search, setSearch] = useState('');
  const [searchApplied, setSearchApplied] = useState('');
  const [granularity, setGranularity] = useState<'day' | 'week'>('day');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const scopeParams = useMemo(
    () => ({
      fromUtc: range[0].toISOString(),
      toUtc: range[1].toISOString(),
      operationType,
      tenantId: isSuperAdmin ? tenantId : undefined,
    }),
    [range, operationType, tenantId, isSuperAdmin]
  );

  const listParams = useMemo(
    (): FiskalyErrorQuery => ({
      ...scopeParams,
      reviewStatus,
      errorCode: errorCodeApplied.trim() || undefined,
      search: searchApplied.trim() || undefined,
      page,
      pageSize,
    }),
    [scopeParams, reviewStatus, errorCodeApplied, searchApplied, page, pageSize]
  );

  const statsQuery = useQuery({
    queryKey: [...QUERY_KEY, 'stats', scopeParams],
    queryFn: ({ signal }) => getFiskalyErrorStats(scopeParams, signal),
  });

  const listQuery = useQuery({
    queryKey: [...QUERY_KEY, 'list', listParams],
    queryFn: ({ signal }) => getFiskalyErrors(listParams, signal),
    placeholderData: keepPreviousData,
  });

  const detailQuery = useQuery({
    queryKey: [...QUERY_KEY, 'detail', selectedId],
    queryFn: ({ signal }) => getFiskalyErrorById(selectedId!, signal),
    enabled: Boolean(selectedId),
  });

  const exportMutation = useMutation({
    mutationFn: (format: 'csv' | 'pdf') => exportFiskalyErrors({ ...listParams, format }),
    onSuccess: (result) => {
      triggerBlobDownload(result.blob, result.fileName);
      notify.success(t('tseFiskaly.errors.exportSuccess'));
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FiskalyErrors.export',
        fallbackKey: 'tseFiskaly.errors.loadFailed',
      });
    },
  });

  const reviewMutation = useMutation({
    mutationFn: ({ id, status }: { id: string; status: FiskalyErrorReviewStatus }) =>
      setFiskalyErrorReview(id, status),
    onSuccess: () => {
      notify.success(t('tseFiskaly.errors.reviewUpdated'));
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FiskalyErrors.review',
        fallbackKey: 'tseFiskaly.errors.reviewFailed',
      });
    },
  });

  const stats = statsQuery.data;
  const kpis = stats?.kpis;
  const kindLabel = (key: string | null | undefined) =>
    key && KIND_KEYS[key] ? t(KIND_KEYS[key]) : (key ?? '—');
  const reviewLabel = (status: string) => {
    if (status === 'resolved') return t('tseFiskaly.errors.reviewResolved');
    if (status === 'known_issue') return t('tseFiskaly.errors.reviewKnownIssue');
    return t('tseFiskaly.errors.reviewOpen');
  };

  const chartLabels = useMemo(
    () => ({
      trend: t('tseFiskaly.errors.chartTrend'),
      distribution: t('tseFiskaly.errors.chartDistribution'),
      byType: t('tseFiskaly.errors.chartByType'),
      byTenant: t('tseFiskaly.errors.chartByTenant'),
      errors: t('tseFiskaly.errors.kpiTotal'),
    }),
    [t]
  );

  const trend = useMemo(() => {
    if (!stats) return [];
    if (granularity === 'week') {
      return (stats.weekly ?? []).map((row) => ({ date: row.week, count: row.count }));
    }
    return stats.daily ?? [];
  }, [stats, granularity]);

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      {statsQuery.isError || listQuery.isError ? (
        <Alert type="error" showIcon title={t('tseFiskaly.errors.loadFailed')} />
      ) : null}
      <Card size="small">
        <Space wrap>
          {isSuperAdmin ? (
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              loading={tenantsLoading}
              placeholder={t('tseFiskaly.errors.allTenants')}
              style={{ minWidth: 220 }}
              value={tenantId}
              onChange={(value) => {
                setTenantId(value);
                setPage(1);
              }}
              options={tenants.map((row) => ({ value: row.id, label: row.name }))}
            />
          ) : null}
          <DatePicker.RangePicker
            value={range}
            format={DAYJS_DATE_FORMAT}
            onChange={(value) => {
              setRange(value?.[0] && value[1] ? [value[0], value[1]] : defaultRange());
              setPage(1);
            }}
            allowClear={false}
          />
          <Select
            allowClear
            placeholder={t('tseFiskaly.errors.filterType')}
            style={{ minWidth: 180 }}
            value={operationType}
            onChange={(value) => {
              setOperationType(value);
              setPage(1);
            }}
            options={OPERATION_TYPES.map((kind) => ({
              value: kind,
              label: t(KIND_KEYS[kind]),
            }))}
          />
          <Select
            allowClear
            placeholder={t('tseFiskaly.errors.filterReview')}
            style={{ minWidth: 180 }}
            value={reviewStatus}
            onChange={(value) => {
              setReviewStatus(value);
              setPage(1);
            }}
            options={[
              { value: 'open', label: t('tseFiskaly.errors.reviewOpen') },
              { value: 'resolved', label: t('tseFiskaly.errors.reviewResolved') },
              { value: 'known_issue', label: t('tseFiskaly.errors.reviewKnownIssue') },
            ]}
          />
          <Input.Search
            allowClear
            placeholder={t('tseFiskaly.errors.filterErrorCode')}
            style={{ minWidth: 160 }}
            value={errorCode}
            onChange={(event) => setErrorCode(event.target.value)}
            onSearch={(value) => {
              setErrorCodeApplied(value);
              setPage(1);
            }}
          />
          <Input.Search
            allowClear
            placeholder={t('tseFiskaly.errors.searchPlaceholder')}
            style={{ minWidth: 240 }}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            onSearch={(value) => {
              setSearchApplied(value);
              setPage(1);
            }}
          />
          <Segmented
            value={granularity}
            onChange={(value) => setGranularity(value as 'day' | 'week')}
            options={[
              { value: 'day', label: t('tseFiskaly.errors.granularityDaily') },
              { value: 'week', label: t('tseFiskaly.errors.granularityWeekly') },
            ]}
          />
          <Button
            icon={<ReloadOutlined />}
            onClick={() => {
              void statsQuery.refetch();
              void listQuery.refetch();
            }}
          >
            {t('common.buttons.refresh')}
          </Button>
          <Button
            icon={<DownloadOutlined />}
            loading={exportMutation.isPending}
            onClick={() => exportMutation.mutate('csv')}
          >
            {t('tseFiskaly.errors.exportCsv')}
          </Button>
          <Button
            icon={<DownloadOutlined />}
            loading={exportMutation.isPending}
            onClick={() => exportMutation.mutate('pdf')}
          >
            {t('tseFiskaly.errors.exportPdf')}
          </Button>
        </Space>
      </Card>

      {statsQuery.isLoading || !kpis ? (
        <Skeleton active paragraph={{ rows: 4 }} />
      ) : (
        <Row gutter={[16, 16]}>
          <Col xs={24} sm={12} lg={isSuperAdmin ? 6 : 8}>
            <Card size="small">
              <Statistic title={t('tseFiskaly.errors.kpiTotal')} value={kpis.totalErrors} />
            </Card>
          </Col>
          <Col xs={24} sm={12} lg={isSuperAdmin ? 6 : 8}>
            <Card size="small">
              <Statistic
                title={t('tseFiskaly.errors.kpiRate')}
                value={kpis.errorRatePercent}
                suffix="%"
                precision={2}
              />
            </Card>
          </Col>
          <Col xs={24} sm={12} lg={isSuperAdmin ? 6 : 8}>
            <Card size="small">
              <Statistic
                title={t('tseFiskaly.errors.kpiMostCommon')}
                value={kpis.mostCommonErrorCode || '—'}
                suffix={kpis.mostCommonErrorCount > 0 ? `(${kpis.mostCommonErrorCount})` : undefined}
              />
            </Card>
          </Col>
          {isSuperAdmin ? (
            <Col xs={24} sm={12} lg={6}>
              <Card size="small">
                <Statistic
                  title={t('tseFiskaly.errors.kpiTenant')}
                  value={kpis.tenantWithMostErrorsName || kpis.tenantWithMostErrorsId || '—'}
                  suffix={
                    kpis.tenantWithMostErrorsCount > 0 ? `(${kpis.tenantWithMostErrorsCount})` : undefined
                  }
                />
              </Card>
            </Col>
          ) : null}
        </Row>
      )}

      {statsQuery.isLoading || !stats ? (
        <Skeleton active paragraph={{ rows: 10 }} />
      ) : (
        <>
          <FiskalyErrorCharts
            trend={trend}
            byCode={(stats.topErrors ?? [])
              .filter((row) => row.count > 0)
              .map((row) => ({ name: row.key, value: row.count }))}
            byType={(stats.byOperationType ?? [])
              .filter((row) => row.count > 0)
              .map((row) => ({ name: kindLabel(row.key), value: row.count }))}
            byTenant={(stats.byTenant ?? []).map((row) => ({
              name: row.tenantName || row.tenantId.slice(0, 8),
              value: row.count,
            }))}
            showTenant={isSuperAdmin}
            labels={chartLabels}
            pieColors={PIE_COLORS}
          />
          <Card size="small" title={t('tseFiskaly.errors.topTitle')}>
            <Table
              rowKey="key"
              size="small"
              pagination={false}
              dataSource={stats.topErrors ?? []}
              locale={{ emptyText: t('tseFiskaly.errors.empty') }}
              columns={[
                { title: t('tseFiskaly.errors.colCode'), dataIndex: 'key' },
                { title: t('tseFiskaly.errors.colCount'), dataIndex: 'count' },
                {
                  title: t('tseFiskaly.errors.colMessage'),
                  dataIndex: 'sampleMessage',
                  ellipsis: true,
                  render: (value: string | null | undefined) => value || '—',
                },
              ]}
            />
          </Card>
          <Card size="small" title={t('tseFiskaly.errors.listTitle')}>
            <FiskalyErrorList
              items={listQuery.data?.items ?? []}
              loading={listQuery.isFetching}
              isSuperAdmin={isSuperAdmin}
              canResolve={canResolve}
              resolvePending={reviewMutation.isPending}
              page={page}
              pageSize={pageSize}
              total={listQuery.data?.totalCount ?? 0}
              emptyText={t('tseFiskaly.errors.empty')}
              labels={{
                date: t('tseFiskaly.errors.colDate'),
                code: t('tseFiskaly.errors.colCode'),
                message: t('tseFiskaly.errors.colMessage'),
                type: t('tseFiskaly.errors.colType'),
                tenant: t('tseFiskaly.errors.colTenant'),
                user: t('tseFiskaly.errors.colUser'),
                status: t('tseFiskaly.errors.colReview'),
                actions: t('tseFiskaly.errors.colActions'),
                markResolved: t('tseFiskaly.errors.markResolved'),
                markKnownIssue: t('tseFiskaly.errors.markKnownIssue'),
                markOpen: t('tseFiskaly.errors.markOpen'),
              }}
              kindLabel={kindLabel}
              reviewLabel={reviewLabel}
              formatDate={(value) => formatDateTime(value, formatLocale)}
              onPageChange={(nextPage, nextSize) => {
                setPage(nextPage);
                setPageSize(nextSize);
              }}
              onSelect={setSelectedId}
              onResolve={(id, status) => reviewMutation.mutate({ id, status })}
            />
          </Card>
        </>
      )}

      <FiskalyErrorDetailView
        open={Boolean(selectedId)}
        loading={detailQuery.isFetching}
        error={detailQuery.isError}
        isSuperAdmin={isSuperAdmin}
        canResolve={canResolve}
        resolvePending={reviewMutation.isPending}
        detail={detailQuery.data}
        title={t('tseFiskaly.errors.detailTitle')}
        loadFailed={t('tseFiskaly.errors.loadFailed')}
        labels={{
          date: t('tseFiskaly.errors.colDate'),
          code: t('tseFiskaly.errors.colCode'),
          message: t('tseFiskaly.errors.colMessage'),
          type: t('tseFiskaly.errors.colType'),
          tenant: t('tseFiskaly.errors.colTenant'),
          user: t('tseFiskaly.errors.colUser'),
          status: t('tseFiskaly.errors.colReview'),
          receipt: t('tseFiskaly.errors.colReceipt'),
          register: t('tseFiskaly.errors.colRegister'),
          request: t('tseFiskaly.errors.detailRequest'),
          response: t('tseFiskaly.errors.detailResponse'),
          stack: t('tseFiskaly.errors.detailStack'),
          solution: t('tseFiskaly.errors.knownSolution'),
          markResolved: t('tseFiskaly.errors.markResolved'),
          markKnownIssue: t('tseFiskaly.errors.markKnownIssue'),
          markOpen: t('tseFiskaly.errors.markOpen'),
        }}
        kindLabel={kindLabel}
        reviewLabel={reviewLabel}
        formatDate={(value) => formatDateTime(value, formatLocale)}
        t={t}
        onClose={() => setSelectedId(null)}
        onResolve={(id, status) => reviewMutation.mutate({ id, status })}
      />
    </Space>
  );
}
