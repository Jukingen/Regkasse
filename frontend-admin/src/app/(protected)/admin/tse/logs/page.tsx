'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Col, DatePicker, Input, Row, Select, Space, Statistic, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  TseActiveTenantTag,
  TseTenantRequiredAlert,
} from '@/features/tse-shared/components/TseTenantContextUi';
import { useTsePageTenant } from '@/features/tse-shared/hooks/useTsePageTenant';
import { aggregateTseLogs, searchTseLogs } from '@/features/tse-logs/api/logs';
import type { TseLogEntry } from '@/features/tse-logs/types';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const LOGS_KEY = ['admin', 'tse-logs'] as const;

type DayRange = [Dayjs, Dayjs];

function defaultRange(): DayRange {
  return [dayjs().subtract(7, 'day').startOf('day'), dayjs().endOf('day')];
}

export default function TseLogsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const { tenantId, isReady } = useTsePageTenant();

  const [range, setRange] = useState<DayRange>(defaultRange);
  const [level, setLevel] = useState<string | undefined>();
  const [source, setSource] = useState('');

  const fromUtc = range[0].toISOString();
  const toUtc = range[1].toISOString();
  const sourceFilter = source.trim();

  const aggregateQuery = useQuery({
    queryKey: [...LOGS_KEY, 'aggregate', tenantId, fromUtc, toUtc],
    queryFn: ({ signal }) => aggregateTseLogs(tenantId!, fromUtc, toUtc, signal),
    enabled: allowed && !!tenantId,
  });

  const searchQuery = useQuery({
    queryKey: [...LOGS_KEY, 'search', tenantId, fromUtc, toUtc, level ?? '', sourceFilter],
    queryFn: ({ signal }) =>
      searchTseLogs(
        {
          tenantId: tenantId!,
          fromUtc,
          toUtc,
          ...(level ? { level } : {}),
          ...(sourceFilter ? { source: sourceFilter } : {}),
          skip: 0,
          take: 100,
        },
        signal
      ),
    enabled: allowed && !!tenantId,
  });

  useEffect(() => {
    if (!aggregateQuery.isError) return;
    notify.apiError(aggregateQuery.error, {
      logContext: 'TseLogs.aggregate',
      fallbackKey: 'tseLogs.error',
    });
  }, [aggregateQuery.isError, aggregateQuery.error, aggregateQuery.errorUpdatedAt, notify]);

  useEffect(() => {
    if (!searchQuery.isError) return;
    notify.apiError(searchQuery.error, {
      logContext: 'TseLogs.search',
      fallbackKey: 'tseLogs.error',
    });
  }, [searchQuery.isError, searchQuery.error, searchQuery.errorUpdatedAt, notify]);

  const columns: ColumnsType<TseLogEntry> = useMemo(
    () => [
      {
        title: t('tseLogs.table.time'),
        dataIndex: 'timestamp',
        key: 'timestamp',
        width: 180,
        render: (value: string) => dayjs(value).format('YYYY-MM-DD HH:mm:ss'),
      },
      {
        title: t('tseLogs.table.level'),
        dataIndex: 'level',
        key: 'level',
        width: 120,
      },
      {
        title: t('tseLogs.table.source'),
        dataIndex: 'source',
        key: 'source',
        width: 180,
      },
      {
        title: t('tseLogs.table.message'),
        dataIndex: 'message',
        key: 'message',
      },
    ],
    [t]
  );

  if (!allowed) {
    return <Alert type="error" showIcon title={t('tseLogs.forbidden')} />;
  }

  const aggregate = aggregateQuery.data;
  const totalLogs = aggregate?.totalLogs ?? 0;
  const errorRate = totalLogs > 0 ? (aggregate!.errorLogs / totalLogs) * 100 : 0;
  const loadFailed = aggregateQuery.isError || searchQuery.isError;

  return (
    <>
      <AdminPageHeader
        title={t('tseLogs.title')}
        subtitle={t('tseLogs.subtitle')}
        breadcrumbs={buildPlatformAdminBreadcrumbs(t, 'securityTse', { title: t('tseLogs.title') })}
        extra={
          <Space>
            <TseActiveTenantTag />
            <Button
              disabled={!tenantId}
              loading={aggregateQuery.isFetching || searchQuery.isFetching}
              onClick={() => {
                void aggregateQuery.refetch();
                void searchQuery.refetch();
              }}
            >
              {t('tseLogs.refresh')}
            </Button>
          </Space>
        }
      />

      {!isReady ? (
        <TseTenantRequiredAlert emptySelectKey="tseLogs.emptySelect" />
      ) : (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
          <Card size="small">
            <Space wrap>
              <DatePicker.RangePicker
                aria-label={t('tseLogs.filters.dateRange')}
                value={range}
                allowClear={false}
                onChange={(value) => {
                  if (value?.[0] && value[1]) {
                    setRange([value[0], value[1]]);
                  }
                }}
              />
              <Select
                allowClear
                aria-label={t('tseLogs.filters.level')}
                placeholder={t('tseLogs.filters.level')}
                style={{ minWidth: 160 }}
                value={level}
                onChange={(value) => setLevel(value)}
                options={[
                  { value: 'Error', label: t('tseLogs.errors') },
                  { value: 'Warning', label: t('tseLogs.warnings') },
                  { value: 'Info', label: t('tseLogs.info') },
                ]}
              />
              <Input
                allowClear
                aria-label={t('tseLogs.filters.source')}
                placeholder={t('tseLogs.filters.source')}
                style={{ width: 220 }}
                value={source}
                onChange={(event) => setSource(event.target.value)}
              />
            </Space>
          </Card>

          {loadFailed ? <Alert type="error" showIcon title={t('tseLogs.error')} /> : null}

          <Card title={t('tseLogs.aggregate.title')} loading={aggregateQuery.isLoading}>
            <Row gutter={16}>
              <Col xs={24} sm={12}>
                <Statistic title={t('tseLogs.aggregate.count')} value={totalLogs} />
              </Col>
              <Col xs={24} sm={12}>
                <Statistic
                  title={t('tseLogs.aggregate.errorRate')}
                  value={errorRate}
                  precision={1}
                  suffix="%"
                />
              </Col>
            </Row>
          </Card>

          <Table<TseLogEntry>
            rowKey="id"
            loading={searchQuery.isLoading}
            columns={columns}
            dataSource={searchQuery.data?.logs ?? []}
            pagination={{ pageSize: 20, showSizeChanger: false }}
            locale={{ emptyText: t('tseLogs.empty') }}
          />
        </Space>
      )}
    </>
  );
}
