'use client';

import { ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Select, Space, Tabs, Typography } from 'antd';
import type { TablePaginationConfig } from 'antd/es/table';
import Link from 'next/link';
import React, { useMemo, useState } from 'react';

import { CreateMonatsbelegModal } from '@/features/rksv/components/CreateMonatsbelegModal';
import { MissingPreviousMonatsbelegAlert } from '@/features/rksv/components/MissingPreviousMonatsbelegAlert';
import { MonatsbelegOpsTable } from '@/features/rksv/components/MonatsbelegOpsTable';
import { MonatsbelegPolicyInlineEditor } from '@/features/rksv/components/MonatsbelegPolicyInlineEditor';
import { useCreateMonatsbeleg } from '@/features/rksv/hooks/useCreateMonatsbeleg';
import { useMonatsbelegStatus } from '@/features/rksv/hooks/useMonatsbeleg';
import { anyRegisterLastMonthMissing } from '@/features/rksv/utils/anyRegisterLastMonthMissing';
import { collectPastMissingMonatsbelege } from '@/features/rksv/utils/monatsbelegMissingMonths';
import { isRksvRegisterDecommissionedError } from '@/features/rksv/utils/rksvRegisterDecommissionedError';
import {
  buildMonatsbelegOpsRows,
  filterMonatsbelegOpsRows,
  formatJahresbelegFonDeadline,
  isJahresbelegFonDeadlinePassed,
  isJahresbelegOpsRow,
  jahresbelegNeedsFonAttention,
  type MonatsbelegOpsRow,
} from '@/features/rksv/utils/monatsbelegOpsPresentation';
import { getViennaCalendarYearMonth } from '@/shared/utils/viennaCalendar';

import { TableSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { getEffectiveTenantSlug } from '@/features/auth/services/devTenant';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import { isPermanentlyClosedRegister } from '@/features/cash-registers/utils/registerStatus';
import { PeriodSalesSummaryCard } from '@/features/reporting/PeriodSalesSummaryCard';
import { downloadReportPdf, triggerReportPdfBlobDownload } from '@/features/reports/api/reportPdfApi';
import { buildReportFileName } from '@/features/reports/utils/reportExportFileName';
import {
  fetchMonatsbelegSalesSummary,
  fetchMonatsbelege,
  monatsbelegeListQueryKey,
} from '@/features/rksv/api/monatsbelege';
import {
  fetchMonatsbelegPolicy,
  monatsbelegPolicyQueryKey,
} from '@/features/settings/api/monatsbelegPolicy';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { ADMIN_NAV_GROUP_LABELS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

type OpsTab = 'monats' | 'jahres';

export function MonatsbelegList() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal, message } = useAntdApp();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed =
    hasPermission(PERMISSIONS.FINANZONLINE_MANAGE) || hasPermission(PERMISSIONS.RKSV_MONATSBELEG_VIEW);

  const tp = (path: string, options?: Record<string, string | number>) =>
    t(`rksvHub.monatsbelegePage.${path}`, options);

  const currentYear = new Date().getFullYear();
  const [year, setYear] = useState(currentYear);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [tab, setTab] = useState<OpsTab>('monats');
  const [forceCreateOpen, setForceCreateOpen] = useState(false);
  const [bulkBusy, setBulkBusy] = useState(false);
  const canCreateMonatsbeleg = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);
  const createMonatsbeleg = useCreateMonatsbeleg();

  const registers = useAdminCashRegisterList({
    allowTenantScopedDefault: true,
    excludeDecommissioned: false,
    enabled: allowed,
  });
  const selectableRegisters = useMemo(
    () => (registers.registers ?? []).filter((row) => !isPermanentlyClosedRegister(row)),
    [registers.registers]
  );
  const closedRegisterIds = useMemo(() => {
    const ids = new Set<string>();
    for (const row of registers.registers ?? []) {
      if (row.id && isPermanentlyClosedRegister(row)) ids.add(String(row.id));
    }
    return ids;
  }, [registers.registers]);
  const selectedRegisterIsClosed = Boolean(cashRegisterId && closedRegisterIds.has(cashRegisterId));

  const listParams = useMemo(
    () => ({
      year,
      cashRegisterId,
      pageNumber: 1,
      pageSize: 200,
    }),
    [year, cashRegisterId]
  );

  const query = useQuery({
    queryKey: [...monatsbelegeListQueryKey, listParams],
    queryFn: ({ signal }) => fetchMonatsbelege(listParams, signal),
    enabled: allowed,
  });
  const policyQuery = useQuery({
    queryKey: monatsbelegPolicyQueryKey,
    queryFn: ({ signal }) => fetchMonatsbelegPolicy(signal),
    enabled: allowed,
  });
  const statusOverview = useMonatsbelegStatus({ enabled: allowed });
  const missingPrevious = anyRegisterLastMonthMissing(statusOverview.data);
  const pastMissing = useMemo(
    () => collectPastMissingMonatsbelege(statusOverview.data),
    [statusOverview.data]
  );

  const opsRows = useMemo(
    () =>
      buildMonatsbelegOpsRows({
        items: query.data?.items ?? [],
        missing: pastMissing,
        year,
        cashRegisterId,
        registers: selectableRegisters.map((r) => ({
          id: r.id,
          registerNumber: r.registerNumber,
          location: r.location,
        })),
        autoEnabled: policyQuery.data?.autoMonatsbelegEnabled ?? true,
        salesGateMode: policyQuery.data?.blockingMode ?? 'Strict',
      }),
    [
      cashRegisterId,
      pastMissing,
      policyQuery.data?.autoMonatsbelegEnabled,
      policyQuery.data?.blockingMode,
      query.data?.items,
      selectableRegisters,
      year,
    ]
  );

  const tabRows = useMemo(() => {
    const scoped = tab === 'jahres' ? opsRows.filter(isJahresbelegOpsRow) : opsRows;
    return filterMonatsbelegOpsRows(scoped, status);
  }, [opsRows, status, tab]);

  const pageRows = useMemo(() => {
    const start = (page - 1) * pageSize;
    return tabRows.slice(start, start + pageSize);
  }, [page, pageSize, tabRows]);

  const bulkTargets = useMemo(() => {
    const source = tab === 'jahres' ? opsRows.filter(isJahresbelegOpsRow) : opsRows;
    return source.filter(
      (row) =>
        (row.displayStatus === 'Missing' || row.displayStatus === 'Overdue') &&
        !closedRegisterIds.has(row.cashRegisterId)
    );
  }, [closedRegisterIds, opsRows, tab]);

  const previewMonth = useMemo(() => {
    if (!cashRegisterId || tab === 'jahres') return undefined;
    const created = (query.data?.items ?? [])
      .filter(
        (row) =>
          row.cashRegisterId === cashRegisterId && row.status === 'created' && !row.isJahresbeleg
      )
      .sort((a, b) => b.month - a.month);
    if (created[0]) return created[0].month;
    const vienna = getViennaCalendarYearMonth();
    if (year < vienna.year) return 12;
    if (year > vienna.year) return 1;
    return Math.max(1, vienna.month - 1);
  }, [cashRegisterId, query.data?.items, tab, year]);

  const salesQuery = useQuery({
    queryKey: ['admin', 'rksv', 'monatsbelege', 'sales-summary', cashRegisterId, year, previewMonth, tab],
    queryFn: ({ signal }) =>
      fetchMonatsbelegSalesSummary(
        {
          cashRegisterId: cashRegisterId ?? '',
          year,
          month: tab === 'jahres' ? undefined : previewMonth,
        },
        signal
      ),
    enabled: allowed && Boolean(cashRegisterId),
  });

  const downloadPreviewPdf = async () => {
    const paymentId = salesQuery.data?.paymentId?.trim();
    if (!paymentId) {
      message.info(t('reporting.periodSales.noPdf'));
      return;
    }
    const reportType = tab === 'jahres' ? 'jahresbeleg' : 'monatsbeleg';
    try {
      const blob = await downloadReportPdf(reportType, paymentId);
      triggerReportPdfBlobDownload(
        blob,
        buildReportFileName({
          reportType,
          tenantSlug: getEffectiveTenantSlug(),
          period: salesQuery.data?.periodLabel,
        })
      );
      message.success(t('reporting.storedPdf.success'));
    } catch {
      message.error(t('reporting.storedPdf.error'));
    }
  };

  const jahresRows = useMemo(() => opsRows.filter(isJahresbelegOpsRow), [opsRows]);
  const showFonOverdueReminder =
    tab === 'jahres' &&
    isJahresbelegFonDeadlinePassed(year) &&
    jahresRows.some(jahresbelegNeedsFonAttention);
  const showFonReminder = tab === 'jahres';

  const forceTarget = useMemo(() => {
    const failed = (query.data?.items ?? []).find((r) => r.status === 'failed');
    const vienna = getViennaCalendarYearMonth();
    const prevMonth = vienna.month === 1 ? 12 : vienna.month - 1;
    const prevYear = vienna.month === 1 ? vienna.year - 1 : vienna.year;
    const firstRegister = selectableRegisters[0];
    const failedId = failed?.cashRegisterId;
    const failedIsClosed = Boolean(failedId && closedRegisterIds.has(failedId));
    const selectedIsClosed = Boolean(cashRegisterId && closedRegisterIds.has(cashRegisterId));
    const chosenId =
      !failedIsClosed && failedId
        ? failedId
        : !selectedIsClosed && cashRegisterId
          ? cashRegisterId
          : firstRegister?.id ?? '';
    const chosenRegister = selectableRegisters.find((row) => row.id === chosenId) ?? firstRegister;
    const registerLabel =
      !failedIsClosed && failed
        ? failed.registerLocation
          ? `${failed.registerNumber} — ${failed.registerLocation}`
          : failed.registerNumber
        : chosenRegister
          ? chosenRegister.location
            ? `${chosenRegister.registerNumber} — ${chosenRegister.location}`
            : chosenRegister.registerNumber
          : undefined;
    return {
      cashRegisterId: chosenId,
      cashRegisterLabel: registerLabel,
      year: failed?.year ?? prevYear,
      month: failed?.month ?? prevMonth,
    };
  }, [cashRegisterId, closedRegisterIds, query.data?.items, selectableRegisters]);

  const yearOptions = useMemo(() => {
    const years: number[] = [];
    for (let y = currentYear; y >= currentYear - 7; y -= 1) years.push(y);
    return years.map((y) => ({ value: y, label: String(y) }));
  }, [currentYear]);

  const pagination: TablePaginationConfig = {
    current: page,
    pageSize,
    total: tabRows.length,
    showSizeChanger: true,
    pageSizeOptions: ['20', '50', '100'],
    onChange: (p, ps) => {
      setPage(p);
      setPageSize(ps ?? 50);
    },
  };

  const runBulkCreate = async (targets: MonatsbelegOpsRow[]) => {
    setBulkBusy(true);
    let ok = 0;
    let failed = 0;
    try {
      for (const row of targets) {
        try {
          await createMonatsbeleg.mutateAsync({
            data: {
              cashRegisterId: row.cashRegisterId,
              year: row.year,
              month: row.month,
              reason: 'FA Monatsbelege: create missing',
            },
            force: true,
          });
          ok += 1;
        } catch (error: unknown) {
          if (isRksvRegisterDecommissionedError(error)) {
            notify.errorKey('rksvHub.sonderbelege.decommissionedHint');
          }
          failed += 1;
        }
      }
      await queryClient.invalidateQueries({ queryKey: monatsbelegeListQueryKey });
      if (failed === 0) {
        notify.successKey('rksvHub.monatsbelegePage.bulkCreateSuccess', { ok, total: targets.length });
      } else {
        notify.warning('rksvHub.monatsbelegePage.bulkCreatePartial', {
          values: { ok, failed, total: targets.length },
        });
      }
      void query.refetch();
      void statusOverview.refetch?.();
    } finally {
      setBulkBusy(false);
    }
  };

  const onBulkCreateMissing = () => {
    if (!canCreateMonatsbeleg) return;
    if (bulkTargets.length === 0) {
      notify.info('rksvHub.monatsbelegePage.bulkCreateEmpty');
      return;
    }
    modal.confirm({
      title: tp('bulkCreateConfirmTitle'),
      content: tp('bulkCreateConfirmBody', { count: bulkTargets.length }),
      okText: tp('bulkCreateConfirmOk'),
      onOk: () => runBulkCreate(bulkTargets),
    });
  };

  if (!allowed) {
    return (
      <>
        <AdminPageHeader
          title={tp('title')}
          breadcrumbs={[
            adminOverviewCrumb(t),
            { title: ADMIN_NAV_GROUP_LABELS.rksv },
            { title: tp('title') },
          ]}
        />
        <Alert type="error" showIcon title={tp('forbiddenTitle')} description={tp('forbiddenDescription')} />
      </>
    );
  }

  return (
    <>
      <AdminPageHeader
        title={tp('title')}
        subtitle={tp('subtitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: ADMIN_NAV_GROUP_LABELS.rksv },
          { title: tp('title') },
        ]}
        extra={
          <Space wrap>
            {canCreateMonatsbeleg ? (
              <Button
                type="primary"
                onClick={onBulkCreateMissing}
                loading={bulkBusy}
                data-testid="monatsbeleg-bulk-create-missing"
              >
                {tp('bulkCreateMissing')}
              </Button>
            ) : null}
            <Link href="/rksv/sonderbelege?focus=monatsbeleg">{tp('createLink')}</Link>
            <Button icon={<ReloadOutlined />} onClick={() => void query.refetch()} loading={query.isFetching}>
              {tp('refresh')}
            </Button>
          </Space>
        }
      />
      <MonatsbelegPolicyInlineEditor />
      <MissingPreviousMonatsbelegAlert
        visible={missingPrevious}
        canCreate={canCreateMonatsbeleg}
        onCreateNow={() => setForceCreateOpen(true)}
      />
      {query.data?.hasFailedAutoCreates || query.data?.hasMissedAutoCreates ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title={query.data.hasMissedAutoCreates ? tp('missedAlertTitle') : tp('failedAlertTitle')}
          description={
            query.data.hasMissedAutoCreates ? tp('missedAlertBody') : tp('failedAlertBody')
          }
          action={
            canCreateMonatsbeleg ? (
              <Button
                type="primary"
                danger
                onClick={() => setForceCreateOpen(true)}
                data-testid="monatsbeleg-create-now-force">
                {tp('createNowForce')}
              </Button>
            ) : null
          }
        />
      ) : null}
      <Card>
        <Form layout="inline" style={{ marginBottom: 16 }}>
          <Form.Item label={tp('filterYear')}>
            <Select
              style={{ width: 120 }}
              value={year}
              options={yearOptions}
              onChange={(v) => {
                setYear(v);
                setPage(1);
              }}
            />
          </Form.Item>
          <Form.Item label={tp('filterRegister')}>
            <Select
              allowClear
              style={{ minWidth: 220 }}
              placeholder={tp('filterRegisterAll')}
              value={cashRegisterId}
              options={selectableRegisters.map((r) => ({
                value: r.id,
                label: r.location ? `${r.registerNumber} — ${r.location}` : r.registerNumber,
              }))}
              onChange={(v) => {
                setCashRegisterId(v);
                setPage(1);
              }}
            />
          </Form.Item>
          <Form.Item label={tp('filterStatus')}>
            <Select
              style={{ width: 220 }}
              value={status}
              options={[
                { value: 'all', label: tp('status.all') },
                { value: 'created', label: tp('status.created') },
                { value: 'autoCreated', label: tp('displayStatus.AutoCreated') },
                { value: 'missing', label: tp('displayStatus.Missing') },
                { value: 'overdue', label: tp('displayStatus.Overdue') },
                { value: 'failed', label: tp('status.failed') },
                { value: 'fonPending', label: tp('status.fonPending') },
              ]}
              onChange={(v) => {
                setStatus(v);
                setPage(1);
              }}
            />
          </Form.Item>
        </Form>
        {cashRegisterId ? (
          <Space orientation="vertical" size={8} style={{ display: 'flex', marginBottom: 16 }}>
            <PeriodSalesSummaryCard
              loading={salesQuery.isFetching}
              periodLabel={salesQuery.data?.periodLabel}
              receiptCount={salesQuery.data?.receiptCount ?? 0}
              totalRevenue={salesQuery.data?.totalRevenue ?? 0}
              zeroSales={salesQuery.data?.zeroSales ?? false}
            />
            <Button
              data-testid="monatsbeleg-pdf-download"
              disabled={!salesQuery.data?.paymentId}
              onClick={() => void downloadPreviewPdf()}
            >
              {t('reporting.periodSales.download')}
            </Button>
            {!salesQuery.isFetching && !salesQuery.data?.paymentId ? (
              <Typography.Text type="secondary">{t('reporting.periodSales.noPdf')}</Typography.Text>
            ) : null}
          </Space>
        ) : (
          <Typography.Paragraph type="secondary">
            {t('reporting.periodSales.selectRegister')}
          </Typography.Paragraph>
        )}
        {selectedRegisterIsClosed ? (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 16 }}
            data-testid="monatsbeleg-register-decommissioned"
            title={t('rksvHub.sonderbelege.decommissionedHint')}
          />
        ) : null}
        <Tabs
          activeKey={tab}
          onChange={(key) => {
            setTab(key as OpsTab);
            setPage(1);
          }}
          items={[
            { key: 'monats', label: tp('tabMonatsbelege') },
            { key: 'jahres', label: tp('tabJahresbelege') },
          ]}
        />
        {showFonReminder ? (
          <Alert
            type={showFonOverdueReminder ? 'warning' : 'info'}
            showIcon
            style={{ marginBottom: 16 }}
            data-testid="jahresbeleg-fon-reminder"
            title={
              showFonOverdueReminder ? tp('jahresbelegFonReminderOverdueTitle') : tp('jahresbelegFonReminderTitle')
            }
            description={tp(
              showFonOverdueReminder ? 'jahresbelegFonReminderOverdueBody' : 'jahresbelegFonReminderBody',
              { deadline: formatJahresbelegFonDeadline(year) }
            )}
          />
        ) : null}
        {query.isError ? (
          <Alert
            type="error"
            showIcon
            title={tp('loadErrorTitle')}
            description={
              <ApiErrorAlertDescription
                t={t}
                error={query.error}
                logContext="MonatsbelegList"
              />
            }
            action={
              <Button size="small" type="primary" onClick={() => void query.refetch()}>
                {tp('refresh')}
              </Button>
            }
          />
        ) : query.isLoading ? (
          <TableSkeleton rows={8} />
        ) : (
          <MonatsbelegOpsTable
            rows={pageRows}
            variant={tab}
            pagination={pagination}
            emptyText={tab === 'jahres' ? tp('jahresbelegEmpty') : tp('empty')}
          />
        )}
      </Card>
      {forceTarget.cashRegisterId ? (
        <CreateMonatsbelegModal
          open={forceCreateOpen}
          cashRegisterId={forceTarget.cashRegisterId}
          cashRegisterLabel={forceTarget.cashRegisterLabel}
          year={forceTarget.year}
          month={forceTarget.month}
          initialForce
          onClose={() => setForceCreateOpen(false)}
          onSuccess={() => {
            setForceCreateOpen(false);
            void query.refetch();
          }}
        />
      ) : null}
    </>
  );
}
