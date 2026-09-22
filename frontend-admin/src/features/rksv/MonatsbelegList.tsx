'use client';

import { ReloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import Link from 'next/link';
import React, { useMemo, useState } from 'react';

import { CreateMonatsbelegModal } from '@/features/rksv/components/CreateMonatsbelegModal';
import { MissingPreviousMonatsbelegAlert } from '@/features/rksv/components/MissingPreviousMonatsbelegAlert';
import { useMonatsbelegStatus } from '@/features/rksv/hooks/useMonatsbeleg';
import { anyRegisterLastMonthMissing } from '@/features/rksv/utils/anyRegisterLastMonthMissing';
import { getViennaCalendarYearMonth } from '@/shared/utils/viennaCalendar';

import { TableSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { dateColumnRender } from '@/components/DateColumn';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import {
  fetchMonatsbelege,
  monatsbelegeListQueryKey,
  type MonatsbelegListRow,
  type MonatsbelegListStatusFilter,
} from '@/features/rksv/api/monatsbelege';
import { useI18n } from '@/i18n/I18nProvider';
import { ADMIN_NAV_GROUP_LABELS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

const DEP_STATUSES = new Set(['InDep', 'Missing']);
const FON_STATUSES = new Set([
  'NotRequired',
  'Pending',
  'Submitted',
  'Verified',
  'Failed',
  'ManualVerificationRequired',
]);
const ROW_STATUSES = new Set(['created', 'failed']);

function depColor(status: string): string {
  return status === 'InDep' ? 'green' : 'red';
}

function fonColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'verified') return 'green';
  if (s === 'notrequired') return 'default';
  if (s === 'submitted' || s === 'pending') return 'orange';
  return 'red';
}

export function MonatsbelegList() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const allowed =
    hasPermission(PERMISSIONS.FINANZONLINE_MANAGE) || hasPermission(PERMISSIONS.RKSV_MONATSBELEG_VIEW);

  const tp = (path: string, options?: Record<string, string | number>) =>
    t(`rksvHub.monatsbelegePage.${path}`, options);

  const currentYear = new Date().getFullYear();
  const [year, setYear] = useState(currentYear);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();
  const [status, setStatus] = useState<MonatsbelegListStatusFilter>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [forceCreateOpen, setForceCreateOpen] = useState(false);
  const canCreateMonatsbeleg = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);

  const registers = useAdminCashRegisterList({
    allowTenantScopedDefault: true,
    excludeDecommissioned: true,
    enabled: allowed,
  });

  const listParams = useMemo(
    () => ({
      year,
      cashRegisterId,
      status,
      pageNumber: page,
      pageSize,
    }),
    [year, cashRegisterId, status, page, pageSize]
  );

  const query = useQuery({
    queryKey: [...monatsbelegeListQueryKey, listParams],
    queryFn: ({ signal }) => fetchMonatsbelege(listParams, signal),
    enabled: allowed,
  });
  const statusOverview = useMonatsbelegStatus({ enabled: allowed });
  const missingPrevious = anyRegisterLastMonthMissing(statusOverview.data);

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

  const columns: ColumnsType<MonatsbelegListRow> = [
    {
      title: tp('colMonth'),
      dataIndex: 'period',
      key: 'period',
      width: 110,
    },
    {
      title: tp('colRegister'),
      key: 'register',
      ellipsis: true,
      render: (_, row) => {
        const loc = row.registerLocation?.trim();
        return loc ? `${row.registerNumber} — ${loc}` : row.registerNumber;
      },
    },
    {
      title: tp('colCreatedAt'),
      dataIndex: 'createdAtUtc',
      key: 'createdAtUtc',
      width: 180,
      render: dateColumnRender('datetimeSeconds'),
    },
    {
      title: tp('colCreatedBy'),
      dataIndex: 'createdBy',
      key: 'createdBy',
      width: 140,
    },
    {
      title: tp('colTse'),
      dataIndex: 'tseSignature',
      key: 'tseSignature',
      width: 180,
      ellipsis: true,
      render: (v: string) => v || '—',
    },
    {
      title: tp('colDep'),
      dataIndex: 'depStatus',
      key: 'depStatus',
      width: 110,
      render: (v: string) => (
        <Tag color={depColor(v)}>{DEP_STATUSES.has(v) ? tp(`dep.${v}`) : v || '—'}</Tag>
      ),
    },
    {
      title: tp('colFon'),
      dataIndex: 'fonStatus',
      key: 'fonStatus',
      width: 160,
      render: (v: string) => (
        <Tag color={fonColor(v)}>{FON_STATUSES.has(v) ? tp(`fon.${v}`) : v || '—'}</Tag>
      ),
    },
    {
      title: tp('colStatus'),
      dataIndex: 'status',
      key: 'status',
      width: 120,
      render: (v: string, row) => (
        <Space size={4}>
          <Tag color={v === 'failed' ? 'red' : 'green'}>
            {ROW_STATUSES.has(v) ? tp(`status.${v}`) : v}
          </Tag>
          {row.autoCreated ? <Tag>{tp('autoTag')}</Tag> : null}
        </Space>
      ),
    },
  ];

  const pagination: TablePaginationConfig = {
    current: page,
    pageSize,
    total: query.data?.total ?? 0,
    showSizeChanger: true,
    pageSizeOptions: ['20', '50', '100'],
    onChange: (p, ps) => {
      setPage(p);
      setPageSize(ps ?? 50);
    },
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
          <Space>
            <Link href="/rksv/sonderbelege?focus=monatsbeleg">{tp('createLink')}</Link>
            <Button icon={<ReloadOutlined />} onClick={() => void query.refetch()} loading={query.isFetching}>
              {tp('refresh')}
            </Button>
          </Space>
        }
      />
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
              style={{ width: 180 }}
              value={status}
              options={[
                { value: 'all', label: tp('status.all') },
                { value: 'created', label: tp('status.created') },
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
          <Table<MonatsbelegListRow>
            rowKey={(row) => row.paymentId || `${row.cashRegisterId}-${row.period}-${row.status}`}
            columns={columns}
            dataSource={query.data?.items ?? []}
            pagination={pagination}
            locale={{ emptyText: <Typography.Text type="secondary">{tp('empty')}</Typography.Text> }}
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
