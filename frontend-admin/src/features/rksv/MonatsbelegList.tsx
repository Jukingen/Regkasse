'use client';

import { ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Select, Space, Tabs } from 'antd';
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
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import {
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
  const { modal } = useAntdApp();
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
    excludeDecommissioned: true,
    enabled: allowed,
  });

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
        registers: (registers.registers ?? []).map((r) => ({
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
      registers.registers,
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
      (row) => row.displayStatus === 'Missing' || row.displayStatus === 'Overdue'
    );
  }, [opsRows, tab]);

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
    const firstRegister = registers.registers?.[0];
    return {
      cashRegisterId: failed?.cashRegisterId ?? cashRegisterId ?? firstRegister?.id ?? '',
      cashRegisterLabel: failed
        ? failed.registerLocation
          ? `${failed.registerNumber} — ${failed.registerLocation}`
          : failed.registerNumber
        : firstRegister
          ? firstRegister.location
            ? `${firstRegister.registerNumber} — ${firstRegister.location}`
            : firstRegister.registerNumber
          : undefined,
      year: failed?.year ?? prevYear,
      month: failed?.month ?? prevMonth,
    };
  }, [cashRegisterId, query.data?.items, registers.registers]);

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
        } catch {
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
              options={(registers.registers ?? []).map((r) => ({
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
