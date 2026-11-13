'use client';

import { ClearOutlined, DownloadOutlined, EyeOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Drawer,
  Empty,
  Flex,
  Input,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import React, { useCallback, useEffect, useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageScopeSummary, AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  type FiscalExportAuditExportTypeQuery,
  type FiscalExportAuditLogListItem,
  buildFiscalExportAuditCsvExportUrl,
  fetchFiscalExportAuditDetail,
  fetchFiscalExportAuditLogs,
} from '@/features/fiscal-export-audit/api/fiscalExportAuditApi';
import { FiscalRetentionNotice } from '@/features/fiscal-export-audit/components/FiscalRetentionNotice';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatNumber } from '@/i18n/formatting';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import { formatDate, formatDateTimeSeconds } from '@/lib/dateUtils';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

const { RangePicker } = DatePicker;

function prettyJson(value: string | null | undefined): string {
  const s = value?.trim();
  if (!s) return '—';
  try {
    return JSON.stringify(JSON.parse(s), null, 2);
  } catch {
    return s;
  }
}

function formatBytes(n: number | null | undefined, locale: string): string {
  if (n == null || Number.isNaN(n)) return '—';
  if (n < 1024) return `${formatNumber(n, locale, { maximumFractionDigits: 0 })} B`;
  const kb = n / 1024;
  if (kb < 1024) return `${formatNumber(kb, locale, { maximumFractionDigits: 1 })} KB`;
  const mb = kb / 1024;
  return `${formatNumber(mb, locale, { maximumFractionDigits: 1 })} MB`;
}

function buildClientCsvFromRows(rows: FiscalExportAuditLogListItem[]): string {
  const esc = (x: string) => `"${x.replace(/"/g, '""')}"`;
  const header = [
    'id',
    'downloadTimeUtc',
    'userId',
    'username',
    'ip',
    'exportType',
    'includesCsv',
    'periodFrom',
    'periodTo',
    'estimatedBytes',
    'success',
    'longRangeWarning',
  ];
  const lines = [header.join(',')];
  for (const r of rows) {
    lines.push(
      [
        esc(r.id),
        esc(r.downloadTimeUtc),
        esc(r.userId),
        esc(r.username),
        esc(r.ipAddress ?? ''),
        esc(r.exportTypeLabel),
        esc(r.includesCsvFragment ? 'true' : 'false'),
        esc(r.exportPeriodFromUtc ?? ''),
        esc(r.exportPeriodToUtc ?? ''),
        esc(r.estimatedFileSizeBytes != null ? String(r.estimatedFileSizeBytes) : ''),
        esc(r.success ? 'true' : 'false'),
        esc(r.longRangeBulkWarning ? 'true' : 'false'),
      ].join(',')
    );
  }
  return `\ufeff${lines.join('\r\n')}`;
}

export default function FiscalExportAuditPage() {
  const { message } = useAntdApp();

  const { t, formatLocale } = useI18n();
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];
  const canServerExport = permissions.includes(PERMISSIONS.AUDIT_EXPORT);

  const [downloadRange, setDownloadRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [exportType, setExportType] = useState<FiscalExportAuditExportTypeQuery>('all');
  const [userSearchInput, setUserSearchInput] = useState('');
  const [debouncedUserSearch, setDebouncedUserSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [detailId, setDetailId] = useState<string | null>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedUserSearch(userSearchInput.trim()), 450);
    return () => window.clearTimeout(timer);
  }, [userSearchInput]);

  useEffect(() => {
    setPage(1);
  }, [debouncedUserSearch]);

  const listParams = useMemo(
    () => ({
      downloadFrom: downloadRange?.[0]?.toISOString(),
      downloadTo: downloadRange?.[1]?.toISOString(),
      userSearch: debouncedUserSearch ? debouncedUserSearch : undefined,
      exportType,
      page,
      pageSize,
    }),
    [downloadRange, debouncedUserSearch, exportType, page, pageSize]
  );

  const { data, isLoading, isFetching, isError, error, refetch } = useQuery({
    queryKey: ['fiscal-export-audit-list', listParams],
    queryFn: () => fetchFiscalExportAuditLogs(listParams),
  });

  const {
    data: detail,
    isLoading: detailLoading,
    isError: detailError,
  } = useQuery({
    queryKey: ['fiscal-export-audit-detail', detailId],
    queryFn: () => fetchFiscalExportAuditDetail(detailId!),
    enabled: Boolean(detailId),
  });

  const resetFilters = useCallback(() => {
    setDownloadRange(null);
    setExportType('all');
    setUserSearchInput('');
    setDebouncedUserSearch('');
    setPage(1);
  }, []);

  const handleServerCsvExport = useCallback(async () => {
    if (!canServerExport) {
      message.warning(t('fiscalExportAudit.actions.exportCsvNoPermission'));
      return;
    }
    try {
      const path = buildFiscalExportAuditCsvExportUrl({
        downloadFrom: listParams.downloadFrom,
        downloadTo: listParams.downloadTo,
        userSearch: listParams.userSearch,
        exportType: listParams.exportType,
        maxRows: 5000,
      });
      const res = await AXIOS_INSTANCE.get<Blob>(path, { responseType: 'blob' });
      const truncated =
        String(res.headers?.['x-fiscal-audit-export-truncated'] ?? '').toLowerCase() === 'true';
      const blob = res.data;
      const contentType = String(res.headers?.['content-type'] ?? '').toLowerCase();
      const maybeText = await blob.text();
      if (!contentType.includes('text/csv')) {
        try {
          const parsed = JSON.parse(maybeText) as { message?: string };
          message.error(
            typeof parsed?.message === 'string'
              ? parsed.message
              : t('fiscalExportAudit.export.failed')
          );
          return;
        } catch {
          message.error(t('fiscalExportAudit.export.failed'));
          return;
        }
      }
      const csvBlob = new Blob([maybeText], { type: 'text/csv;charset=utf-8' });
      const url = URL.createObjectURL(csvBlob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `fiscal_export_download_audit_${dayjs().format('YYYYMMDD_HHmmss')}.csv`;
      a.click();
      URL.revokeObjectURL(url);
      message.success(t('fiscalExportAudit.export.started'));
      if (truncated) message.warning(t('fiscalExportAudit.export.truncatedNotice'));
    } catch {
      message.error(t('fiscalExportAudit.export.failed'));
    }
  }, [canServerExport, listParams, t]);

  const handleClientPageCsv = useCallback(() => {
    const rows = data?.items ?? [];
    if (!rows.length) {
      message.info(t('fiscalExportAudit.table.empty'));
      return;
    }
    const raw = buildClientCsvFromRows(rows);
    const csvBlob = new Blob([raw], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(csvBlob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `fiscal_export_audit_visible_${dayjs().format('YYYYMMDD_HHmmss')}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    message.success(t('fiscalExportAudit.export.started'));
  }, [data?.items, t]);

  const exportTypeOptions = useMemo(
    () =>
      (['all', 'pdf', 'json', 'csv'] as const).map((v) => ({
        value: v,
        label: t(`fiscalExportAudit.exportType.${v}`),
      })),
    [t]
  );

  const columns: ColumnsType<FiscalExportAuditLogListItem> = useMemo(
    () => [
      {
        title: t('fiscalExportAudit.table.downloadTime'),
        dataIndex: 'downloadTimeUtc',
        key: 'downloadTimeUtc',
        width: 172,
        render: (ts: string) => (
          <Typography.Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
            {ts ? formatDateTimeSeconds(ts) : '—'}
          </Typography.Text>
        ),
      },
      {
        title: t('fiscalExportAudit.table.username'),
        dataIndex: 'username',
        key: 'username',
        ellipsis: true,
        width: 160,
      },
      {
        title: t('fiscalExportAudit.table.ip'),
        dataIndex: 'ipAddress',
        key: 'ipAddress',
        width: 132,
        ellipsis: true,
        render: (ip: string | null) => ip || '—',
      },
      {
        title: t('fiscalExportAudit.table.exportType'),
        dataIndex: 'exportTypeLabel',
        key: 'exportTypeLabel',
        width: 120,
      },
      {
        title: t('fiscalExportAudit.table.period'),
        key: 'period',
        width: 220,
        render: (_: unknown, r: FiscalExportAuditLogListItem) => {
          const from = r.exportPeriodFromUtc ? formatDate(r.exportPeriodFromUtc) : '—';
          const to = r.exportPeriodToUtc ? formatDate(r.exportPeriodToUtc) : '—';
          return (
            <Space size={4} wrap>
              <Typography.Text style={{ fontVariantNumeric: 'tabular-nums' }}>
                {from} – {to}
              </Typography.Text>
              {r.longRangeBulkWarning ? (
                <Tooltip title={t('fiscalExportAudit.warnings.bulkRange')}>
                  <Tag color="warning">{t('fiscalExportAudit.warnings.bulkRange')}</Tag>
                </Tooltip>
              ) : null}
            </Space>
          );
        },
      },
      {
        title: t('fiscalExportAudit.table.fileSize'),
        dataIndex: 'estimatedFileSizeBytes',
        key: 'estimatedFileSizeBytes',
        width: 120,
        align: 'right',
        render: (n: number | null) => formatBytes(n, formatLocale),
      },
      {
        title: t('fiscalExportAudit.table.status'),
        dataIndex: 'success',
        key: 'success',
        width: 96,
        align: 'center',
        render: (ok: boolean) => (
          <Tag color={ok ? 'success' : 'error'}>
            {ok ? t('fiscalExportAudit.table.success') : t('fiscalExportAudit.table.failed')}
          </Tag>
        ),
      },
      {
        title: t('fiscalExportAudit.table.actions'),
        key: 'actions',
        width: 96,
        align: 'center',
        render: (_: unknown, r: FiscalExportAuditLogListItem) => (
          <Tooltip title={t('fiscalExportAudit.table.openDetail')}>
            <Button
              type="link"
              size="small"
              icon={<EyeOutlined />}
              onClick={() => setDetailId(r.id)}
            />
          </Tooltip>
        ),
      },
    ],
    [formatLocale, t]
  );

  const scopeSummary = useMemo(() => {
    const parts: string[] = [
      t('common.auditLogs.scopePage', { page: String(page) }),
      t('common.auditLogs.scopeRowsPerRequest', { pageSize: String(pageSize) }),
      data?.totalCount != null
        ? t('common.auditLogs.scopeTotalEntries', {
            total: formatNumber(data.totalCount, formatLocale, { maximumFractionDigits: 0 }),
          })
        : t('common.auditLogs.scopeTotalLoading'),
    ];
    parts.push(
      `${t('fiscalExportAudit.filters.exportType')}: ${t(`fiscalExportAudit.exportType.${exportType}`)}`
    );
    if (downloadRange?.[0] && downloadRange[1]) {
      parts.push(
        `${formatDate(downloadRange[0])}–${formatDate(downloadRange[1])}`
      );
    } else {
      parts.push(t('common.auditLogs.scopeNoDateFilter'));
    }
    if (debouncedUserSearch) parts.push(debouncedUserSearch);
    parts.push(t('common.auditLogs.scopeApiPageNote'));
    return parts.join(' · ');
  }, [
    data?.totalCount,
    debouncedUserSearch,
    downloadRange,
    exportType,
    formatLocale,
    page,
    pageSize,
    t,
  ]);

  const rows = data?.items ?? [];
  const errMsg = error instanceof Error ? error.message : t('common.messages.noTechnicalDetail');

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('fiscalExportAudit.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_LABEL_KEYS.fiscalExportAuditLogs) },
        ]}
        actions={
          <Space wrap>
            <Tooltip title={t('common.toolbar.refetchHint')}>
              <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching}>
                {t('fiscalExportAudit.actions.refresh')}
              </Button>
            </Tooltip>
            <Tooltip
              title={
                canServerExport ? undefined : t('fiscalExportAudit.actions.exportCsvNoPermission')
              }
            >
              <Button
                icon={<DownloadOutlined />}
                onClick={handleServerCsvExport}
                disabled={!canServerExport}
              >
                {t('fiscalExportAudit.actions.exportCsv')}
              </Button>
            </Tooltip>
            <Button icon={<DownloadOutlined />} onClick={handleClientPageCsv}>
              {t('fiscalExportAudit.actions.clientExportPage')}
            </Button>
            <Button icon={<ClearOutlined />} onClick={resetFilters}>
              {t('fiscalExportAudit.actions.clearFilters')}
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 16, maxWidth: 900 }}>
          {t('fiscalExportAudit.intro')}
        </Typography.Paragraph>
      </AdminPageHeader>

      <FiscalRetentionNotice />

      <AdminPageScopeSummary label={t('common.auditLogs.activeViewLabel')}>
        {scopeSummary}
        {isFetching && !isLoading && !isError ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {' '}
            {t('common.auditLogs.refreshingSuffix')}
          </Typography.Text>
        ) : null}
      </AdminPageScopeSummary>

      <Card size="small" style={{ marginTop: 8 }}>
        <Flex wrap="wrap" gap={12}>
          <Space orientation="vertical" size={4}>
            <Typography.Text type="secondary">
              {t('fiscalExportAudit.filters.downloadRange')}
            </Typography.Text>
            <RangePicker
              format={`${DAYJS_DATE_FORMAT} HH:mm`}
              allowEmpty={[true, true]}
              value={downloadRange}
              onChange={(v) => {
                setDownloadRange(v);
                setPage(1);
              }}
              showTime
            />
          </Space>
          <Space orientation="vertical" size={4}>
            <Typography.Text type="secondary">
              {t('fiscalExportAudit.filters.exportType')}
            </Typography.Text>
            <Select<FiscalExportAuditExportTypeQuery>
              style={{ minWidth: 200 }}
              value={exportType}
              options={exportTypeOptions}
              onChange={(v) => {
                setExportType(v);
                setPage(1);
              }}
            />
          </Space>
          <Space orientation="vertical" size={4} style={{ minWidth: 260, flex: 1 }}>
            <Typography.Text type="secondary">
              {t('fiscalExportAudit.filters.userSearch')}
            </Typography.Text>
            <Input
              allowClear
              placeholder={t('fiscalExportAudit.filters.userSearchPlaceholder')}
              value={userSearchInput}
              onChange={(e) => setUserSearchInput(e.target.value)}
            />
          </Space>
        </Flex>
      </Card>

      {isError ? <Alert type="error" title={errMsg} showIcon style={{ marginTop: 8 }} /> : null}

      <Table<FiscalExportAuditLogListItem>
        rowKey="id"
        loading={isLoading || isFetching}
        columns={columns}
        dataSource={rows}
        style={{ marginTop: 16 }}
        pagination={{
          current: page,
          pageSize,
          total: data?.totalCount ?? 0,
          showSizeChanger: true,
          pageSizeOptions: [10, 25, 50, 100],
          onChange: (p, ps) => {
            setPage(p);
            setPageSize(ps);
          },
        }}
        locale={{ emptyText: <Empty description={t('fiscalExportAudit.table.empty')} /> }}
        scroll={{ x: 1100 }}
      />

      <Drawer
        size={720}
        title={t('fiscalExportAudit.detail.title')}
        open={Boolean(detailId)}
        onClose={() => setDetailId(null)}
        destroyOnHidden
      >
        {detailLoading ? (
          <Typography.Paragraph type="secondary">…</Typography.Paragraph>
        ) : detailError || !detail ? (
          <Typography.Paragraph type="danger">
            {t('common.messages.noTechnicalDetail')}
          </Typography.Paragraph>
        ) : (
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <div>
              <Typography.Text type="secondary">
                {t('fiscalExportAudit.detail.action')}
              </Typography.Text>
              <div>
                <Tag>{detail.action}</Tag>
              </div>
            </div>
            <div>
              <Typography.Text type="secondary">
                {t('fiscalExportAudit.detail.role')}
              </Typography.Text>
              <div>{detail.userRole}</div>
            </div>
            <div>
              <Typography.Text type="secondary">
                {t('fiscalExportAudit.detail.requestJson')}
              </Typography.Text>
              <pre
                style={{
                  maxHeight: 320,
                  overflow: 'auto',
                  fontSize: 12,
                  background: 'var(--ant-color-fill-quaternary, #f5f5f5)',
                  padding: 12,
                  borderRadius: 8,
                }}
              >
                {prettyJson(detail.requestDataJson)}
              </pre>
            </div>
            <div>
              <Typography.Text type="secondary">
                {t('fiscalExportAudit.detail.responseJson')}
              </Typography.Text>
              <pre
                style={{
                  maxHeight: 240,
                  overflow: 'auto',
                  fontSize: 12,
                  background: 'var(--ant-color-fill-quaternary, #f5f5f5)',
                  padding: 12,
                  borderRadius: 8,
                }}
              >
                {prettyJson(detail.responseDataJson)}
              </pre>
            </div>
            {detail.errorDetails ? (
              <div>
                <Typography.Text type="secondary">
                  {t('fiscalExportAudit.detail.errors')}
                </Typography.Text>
                <Typography.Paragraph>{detail.errorDetails}</Typography.Paragraph>
              </div>
            ) : null}
          </Space>
        )}
      </Drawer>
    </AdminPageShell>
  );
}
