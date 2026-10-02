'use client';

import { DownloadOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Empty, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import Link from 'next/link';
/**
 * Formal Tagesbericht: Liste, Filter, CSV, Sammelfinalisierung.
 */
import React, { Suspense, useMemo, useState } from 'react';

import { type ReportFilterValues, ReportFilters } from '@/components/ReportFilters';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { FormalReportLanguageNotice } from '@/components/reporting/FormalReportLanguageNotice';
import { ExportTemplateApplyBanner } from '@/features/exports/components/ExportTemplateApplyBanner';
import { buildTagesberichtCsv, downloadTextFile } from '@/features/reporting/tagesbericht/tagesberichtCsv';
import { TagesberichtBulkFinalizeModal } from '@/features/reporting/tagesbericht/TagesberichtBulkFinalizeModal';
import {
  isProvisionalStatus,
  matchesTagesberichtStatusFilter,
  reportStatusTagColor,
  type TagesberichtListRow,
  type TagesberichtStatusFilter,
} from '@/features/reporting/tagesbericht/tagesberichtStatus';
import { useAntdApp } from '@/hooks/useAntdApp';
import { dateColumnRender } from '@/components/DateColumn';
import { useI18n } from '@/i18n';
import { formatNumber } from '@/i18n/formatting';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { getEffectiveTenantSlug } from '@/features/auth/services/devTenant';
import { buildReportFileName } from '@/features/reports/utils/reportExportFileName';
import { useFiscalReportText } from '@/shared/reporting/useFiscalReportText';

function reportStatusTagLabel(s: string, t: (k: string) => string): string {
  if (s === 'Finalized' || s === 'Provisional' || s === 'Corrected') {
    return t(`reporting.listShared.reportStatus.${s}`);
  }
  if (s === 'Superseded') return t('reporting.listShared.reportStatus.Corrected');
  return s;
}

export default function TagesberichtListPage() {
  const { message } = useAntdApp();
  const { t, formatLocale } = useI18n();
  const { fiscalTooltip, resolveFiscal } = useFiscalReportText();
  const qc = useQueryClient();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);

  const [range, setRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
    dayjs().startOf('month'),
    dayjs().endOf('month'),
  ]);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();
  const [statusFilter, setStatusFilter] = useState<TagesberichtStatusFilter>('all');
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [bulkOpen, setBulkOpen] = useState(false);

  const fromDate = range[0].format('YYYY-MM-DD');
  const toDate = range[1].format('YYYY-MM-DD');

  const applyFilters = (values: Partial<ReportFilterValues>) => {
    if (values.dateRange?.[0] && values.dateRange[1]) {
      setRange(values.dateRange);
    }
    if (Object.prototype.hasOwnProperty.call(values, 'registerId')) {
      setCashRegisterId(values.registerId);
    }
  };

  const handleFilterGenerate = (values: ReportFilterValues) => {
    applyFilters(values);
    void qc.invalidateQueries({ queryKey: ['tagesbericht', 'list'] });
  };

  const listQ = useQuery({
    queryKey: ['tagesbericht', 'list', fromDate, toDate, cashRegisterId],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<TagesberichtListRow[]>('/api/reports/tagesbericht', {
        params: {
          fromDate,
          toDate,
          cashRegisterId,
        },
      });
      return data;
    },
  });

  const rows = useMemo(
    () => (listQ.data ?? []).filter((row) => matchesTagesberichtStatusFilter(row, statusFilter)),
    [listQ.data, statusFilter]
  );

  const generateMut = useMutation({
    mutationFn: async () => {
      if (!cashRegisterId) {
        message.warning(t('reporting.listShared.selectRegister'));
        throw new Error('no register');
      }
      const { data } = await AXIOS_INSTANCE.post('/api/reports/tagesbericht/generate', {
        viennaBusinessDate: range[1].format('YYYY-MM-DD'),
        cashRegisterId,
        operatorUserIdScope: null,
        forceNewProvisional: false,
      });
      return data as { id: string };
    },
    onSuccess: (d) => {
      message.success(t('reporting.tagesbericht.list.messages.generateSuccess'));
      qc.invalidateQueries({ queryKey: ['tagesbericht'] });
      if (d?.id) window.location.href = `/reporting/tagesbericht/${d.id}`;
    },
    onError: () => message.error(t('reporting.tagesbericht.error.generate')),
  });

  const bulkFinalizeMut = useMutation({
    mutationFn: async (ids: string[]) => {
      for (const reportId of ids) {
        await AXIOS_INSTANCE.post('/api/reports/tagesbericht/finalize', { reportId, note: null });
      }
    },
    onSuccess: () => {
      message.success(t('reporting.tagesbericht.list.messages.bulkFinalizeSuccess'));
      setSelectedIds([]);
      setBulkOpen(false);
      qc.invalidateQueries({ queryKey: ['tagesbericht'] });
    },
    onError: () => message.error(t('reporting.tagesbericht.error.bulkFinalize')),
  });

  const handleGenerate = () => {
    if (!cashRegisterId) {
      message.warning(t('reporting.listShared.selectRegister'));
      return;
    }
    generateMut.mutate();
  };

  const handleExportCsv = () => {
    if (!canExport) return;
    try {
      const csv = buildTagesberichtCsv(rows, {
        date: t('reporting.tagesbericht.list.csv.date'),
        register: t('reporting.tagesbericht.list.csv.register'),
        status: t('reporting.tagesbericht.list.csv.status'),
        gross: t('reporting.tagesbericht.list.csv.gross'),
        submission: t('reporting.tagesbericht.list.csv.submission'),
      });
      downloadTextFile(
        csv,
        buildReportFileName({
          reportType: 'tagesbericht',
          tenantSlug: getEffectiveTenantSlug(),
          period: `${fromDate}_${toDate}`,
          extension: 'csv',
        })
      );
      message.success(t('reporting.tagesbericht.list.messages.csvSuccess'));
    } catch {
      message.error(t('reporting.tagesbericht.error.csv'));
    }
  };

  const selectedProvisional = rows.filter(
    (row) => selectedIds.includes(row.id) && isProvisionalStatus(row.reportStatus)
  );

  const openBulkFinalize = () => {
    if (selectedProvisional.length === 0) {
      message.warning(t('reporting.tagesbericht.list.messages.bulkFinalizeNone'));
      return;
    }
    setBulkOpen(true);
  };

  const columns: ColumnsType<TagesberichtListRow> = useMemo(
    () => [
      {
        title: t('reporting.tagesbericht.list.columnDate'),
        dataIndex: 'viennaBusinessDate',
        render: dateColumnRender('short'),
      },
      {
        title: t('reporting.listShared.columns.register'),
        dataIndex: 'registerNumber',
        render: (v, r) => v ?? r.cashRegisterId.slice(0, 8),
      },
      {
        title: t('reporting.listShared.columns.status'),
        dataIndex: 'reportStatus',
        render: (s: string) => (
          <Tag color={reportStatusTagColor(s)}>{reportStatusTagLabel(s, t)}</Tag>
        ),
      },
      {
        title: t('reporting.listShared.columns.foSubmission'),
        dataIndex: 'submission',
        render: (_: unknown, row) => {
          const hint = resolveFiscal(row.submission.operatorHintDe, row.submission.operatorHintEn);
          return (
            <Space orientation="vertical" size={0}>
              <Tag>{row.submission.lifecycle}</Tag>
              {hint ? (
                <Typography.Text
                  type="secondary"
                  style={{ fontSize: 12 }}
                  title={fiscalTooltip(hint.contentLang)}
                >
                  {hint.text}
                </Typography.Text>
              ) : null}
            </Space>
          );
        },
      },
      {
        title: t('reporting.listShared.columns.gross'),
        dataIndex: 'grossSalesAmount',
        render: (v: number) =>
          formatNumber(v, formatLocale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
      },
      {
        title: '',
        key: 'a',
        render: (_, r) => (
          <Link href={`/reporting/tagesbericht/${r.id}`}>{t('reporting.listShared.details')}</Link>
        ),
      },
    ],
    [t, formatLocale, fiscalTooltip, resolveFiscal]
  );

  return (
    <>
      <AdminPageHeader
        title={t('reporting.tagesbericht.list.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('reporting.tagesbericht.list.breadcrumb'), href: '/reporting/tagesbericht' },
        ]}
      />
      <Suspense fallback={null}>
        <ExportTemplateApplyBanner
          expectedKind="tagesbericht"
          onTagesberichtRange={(next) => setRange(next)}
        />
      </Suspense>
      <FormalReportLanguageNotice />
      <Card style={{ marginBottom: 16 }} title={t('adminShell.reporting.filtersTitle')}>
        <ReportFilters
          onGenerate={handleFilterGenerate}
          onValuesChange={applyFilters}
          loading={listQ.isFetching}
          registerRequired={false}
          registerAllowClear
          includeDecommissioned
          extra={
            <Select
              value={statusFilter}
              onChange={(v) => setStatusFilter(v)}
              style={{ minWidth: 180 }}
              aria-label={t('reporting.tagesbericht.list.statusFilter')}
              options={[
                { value: 'all', label: t('reporting.tagesbericht.list.statusAll') },
                { value: 'Provisional', label: t('reporting.listShared.reportStatus.Provisional') },
                { value: 'Finalized', label: t('reporting.listShared.reportStatus.Finalized') },
                { value: 'Corrected', label: t('reporting.listShared.reportStatus.Corrected') },
              ]}
            />
          }
          initialValues={{ dateRange: range }}
        />
        <Space wrap style={{ marginBottom: 12 }}>
          {canExport ? (
            <Button type="primary" loading={generateMut.isPending} onClick={handleGenerate}>
              {t('reporting.tagesbericht.list.generateButton')}
            </Button>
          ) : null}
          {canExport ? (
            <Button icon={<DownloadOutlined />} onClick={handleExportCsv} disabled={rows.length === 0}>
              {t('reporting.tagesbericht.list.exportCsv')}
            </Button>
          ) : null}
          {canExport ? (
            <Button
              data-testid="tagesbericht-bulk-finalize"
              onClick={openBulkFinalize}
              disabled={selectedProvisional.length === 0}
            >
              {t('reporting.tagesbericht.list.bulkFinalize')}
            </Button>
          ) : null}
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('reporting.tagesbericht.list.intro')}
        </Typography.Paragraph>
      </Card>
      <Card>
        {listQ.isError ? (
          <Alert
            type="error"
            showIcon
            title={t('reporting.tagesbericht.error.list')}
            style={{ marginBottom: 16 }}
          />
        ) : null}
        <Table<TagesberichtListRow>
          rowKey="id"
          data-testid="tagesbericht-list-table"
          loading={listQ.isLoading}
          dataSource={rows}
          columns={columns}
          pagination={{ pageSize: 20 }}
          rowSelection={
            canExport
              ? {
                  selectedRowKeys: selectedIds,
                  onChange: (keys) => setSelectedIds(keys.map(String)),
                  getCheckboxProps: (row) => ({
                    disabled: !isProvisionalStatus(row.reportStatus),
                    name: row.id,
                  }),
                }
              : undefined
          }
          locale={{
            emptyText: (
              <Empty
                description={
                  <Space orientation="vertical">
                    <Typography.Text strong>
                      {t('reporting.tagesbericht.list.emptyTitle')}
                    </Typography.Text>
                    <Typography.Text type="secondary">
                      {t('reporting.tagesbericht.list.emptyDescription')}
                    </Typography.Text>
                    {canExport ? (
                      <Button type="primary" onClick={handleGenerate}>
                        {t('reporting.tagesbericht.list.emptyGenerateCta')}
                      </Button>
                    ) : null}
                  </Space>
                }
              />
            ),
          }}
        />
      </Card>
      <TagesberichtBulkFinalizeModal
        open={bulkOpen}
        confirmTitle={t('reporting.tagesbericht.list.bulkFinalizeConfirmTitle')}
        confirmBody={t('reporting.tagesbericht.list.bulkFinalizeConfirmBody', {
          count: selectedProvisional.length,
        })}
        okLabel={t('common.buttons.confirm')}
        cancelLabel={t('common.buttons.cancel')}
        loading={bulkFinalizeMut.isPending}
        onOk={() => bulkFinalizeMut.mutate(selectedProvisional.map((r) => r.id))}
        onCancel={() => setBulkOpen(false)}
      />
    </>
  );
}
