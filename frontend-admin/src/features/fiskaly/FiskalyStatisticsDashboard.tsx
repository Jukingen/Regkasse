'use client';

import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Row,
  Select,
  Skeleton,
  Space,
  Statistic,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import dynamic from 'next/dynamic';
import { useMemo, useState } from 'react';

import {
  exportFiskalyStatistics,
  getFiskalyStatistics,
  type FiskalyStatisticsQuery,
} from '@/features/fiskaly/api/fiskalyStatistics';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { formatNumber } from '@/i18n/formatting';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import { triggerBlobDownload } from '@/lib/download/exportDownload';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const FiskalyStatisticsCharts = dynamic(
  () => import('@/features/fiskaly/components/FiskalyStatisticsCharts'),
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

const PIE_COLORS = ['#1677ff', '#52c41a', '#fa8c16', '#722ed1', '#13c2c2', '#eb2f96', '#cf1322'];
const QUERY_KEY = ['admin', 'fiskaly', 'statistics'] as const;

function defaultRange(): [Dayjs, Dayjs] {
  return [dayjs().subtract(29, 'day').startOf('day'), dayjs().endOf('day')];
}

export function FiskalyStatisticsDashboard() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const isSuperAdmin = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const { tenants, isLoading: tenantsLoading } = useTenantList({ enabled: isSuperAdmin });

  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange);
  const [operationType, setOperationType] = useState<string | undefined>(undefined);
  const [tenantId, setTenantId] = useState<string | undefined>(undefined);

  const params = useMemo((): FiskalyStatisticsQuery => {
    return {
      fromUtc: range[0].toISOString(),
      toUtc: range[1].toISOString(),
      operationType,
      tenantId: isSuperAdmin ? tenantId : undefined,
    };
  }, [range, operationType, tenantId, isSuperAdmin]);

  const statsQuery = useQuery({
    queryKey: [...QUERY_KEY, params],
    queryFn: ({ signal }) => getFiskalyStatistics(params, signal),
  });

  const exportMutation = useMutation({
    mutationFn: (format: 'csv' | 'pdf') => exportFiskalyStatistics({ ...params, format }),
    onSuccess: (result) => {
      triggerBlobDownload(result.blob, result.fileName);
      notify.success(t('tseFiskaly.statistics.exportSuccess'));
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FiskalyStatistics.export',
        fallbackKey: 'tseFiskaly.statistics.loadFailed',
      });
    },
  });

  const stats = statsQuery.data;
  const kpis = stats?.kpis;
  const kindLabel = (key: string | null | undefined) =>
    key && KIND_KEYS[key] ? t(KIND_KEYS[key]) : (key ?? '—');

  const chartLabels = useMemo(
    () => ({
      daily: t('tseFiskaly.statistics.chartDaily'),
      byType: t('tseFiskaly.statistics.chartByType'),
      successFailed: t('tseFiskaly.statistics.chartSuccessFailed'),
      monthly: t('tseFiskaly.statistics.chartMonthly'),
      total: t('tseFiskaly.statistics.seriesTotal'),
      success: t('tseFiskaly.statistics.seriesSuccess'),
      failed: t('tseFiskaly.statistics.seriesFailed'),
    }),
    [t]
  );

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      {statsQuery.isError ? (
        <Alert type="error" showIcon title={t('tseFiskaly.statistics.loadFailed')} />
      ) : null}
      <Card size="small">
        <Space wrap>
          {isSuperAdmin ? (
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              loading={tenantsLoading}
              placeholder={t('tseFiskaly.statistics.allTenants')}
              style={{ minWidth: 220 }}
              value={tenantId}
              onChange={(value) => setTenantId(value)}
              options={tenants.map((row) => ({ value: row.id, label: row.name }))}
            />
          ) : null}
          <DatePicker.RangePicker
            value={range}
            format={DAYJS_DATE_FORMAT}
            onChange={(value) =>
              setRange(value?.[0] && value[1] ? [value[0], value[1]] : defaultRange())
            }
            allowClear={false}
          />
          <Select
            allowClear
            placeholder={t('tseFiskaly.statistics.filterType')}
            style={{ minWidth: 180 }}
            value={operationType}
            onChange={(value) => setOperationType(value)}
            options={OPERATION_TYPES.map((kind) => ({
              value: kind,
              label: t(KIND_KEYS[kind]),
            }))}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void statsQuery.refetch()}>
            {t('common.buttons.refresh')}
          </Button>
          <Button
            icon={<DownloadOutlined />}
            loading={exportMutation.isPending && exportMutation.variables === 'csv'}
            onClick={() => exportMutation.mutate('csv')}
          >
            {t('tseFiskaly.statistics.exportCsv')}
          </Button>
          <Button
            icon={<DownloadOutlined />}
            loading={exportMutation.isPending && exportMutation.variables === 'pdf'}
            onClick={() => exportMutation.mutate('pdf')}
          >
            {t('tseFiskaly.statistics.exportPdf')}
          </Button>
        </Space>
      </Card>

      {statsQuery.isLoading || !kpis ? (
        <Skeleton active paragraph={{ rows: 4 }} />
      ) : (
        <Row gutter={[16, 16]}>
          <Col xs={24} sm={12} lg={8} xl={4}>
            <Card size="small">
              <Statistic
                title={t('tseFiskaly.statistics.kpiTotal')}
                value={kpis.totalOperations}
              />
            </Card>
          </Col>
          <Col xs={24} sm={12} lg={8} xl={4}>
            <Card size="small">
              <Statistic
                title={t('tseFiskaly.statistics.kpiSuccessRate')}
                value={kpis.successRatePercent}
                suffix="%"
                precision={2}
              />
            </Card>
          </Col>
          <Col xs={24} sm={12} lg={8} xl={5}>
            <Card size="small">
              <Statistic
                title={t('tseFiskaly.statistics.kpiMostUsed')}
                value={kindLabel(kpis.mostUsedOperationType)}
              />
            </Card>
          </Col>
          <Col xs={24} sm={12} lg={8} xl={6}>
            <Card size="small">
              <Statistic
                title={t('tseFiskaly.statistics.kpiAvgTime')}
                value={
                  kpis.averageProcessingTimeMs == null
                    ? '—'
                    : t('tseFiskaly.statistics.ms', {
                        value: formatNumber(kpis.averageProcessingTimeMs, formatLocale, {
                          maximumFractionDigits: 1,
                        }),
                      })
                }
              />
            </Card>
          </Col>
          <Col xs={24} sm={12} lg={8} xl={5}>
            <Card size="small">
              <Statistic title={t('tseFiskaly.statistics.kpiErrors')} value={kpis.totalErrors} />
            </Card>
          </Col>
        </Row>
      )}

      {statsQuery.isLoading || !stats ? (
        <Skeleton active paragraph={{ rows: 10 }} />
      ) : (
        <FiskalyStatisticsCharts
          daily={stats.daily ?? []}
          byType={(stats.byOperationType ?? [])
            .filter((row) => row.count > 0)
            .map((row) => ({ name: kindLabel(row.key), value: row.count }))}
          successFailed={[
            {
              name: t('tseFiskaly.statistics.seriesSuccess'),
              value: kpis?.successCount ?? 0,
            },
            {
              name: t('tseFiskaly.statistics.seriesFailed'),
              value: kpis?.failedCount ?? 0,
            },
          ]}
          monthly={stats.monthly ?? []}
          labels={chartLabels}
          pieColors={PIE_COLORS}
        />
      )}
    </Space>
  );
}
