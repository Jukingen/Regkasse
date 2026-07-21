'use client';

/**
 * Backoffice Operations Center: Belegsuche, Ausnahme-Signale, mehrstufiger Nachdruck (Audit) und Querverweise.
 */
import {
  CalendarOutlined,
  DashboardOutlined,
  FileSearchOutlined,
  LinkOutlined,
  PrinterOutlined,
  SafetyOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Col, Row, Space, Statistic, Typography } from 'antd';
import type { TablePaginationConfig } from 'antd/es/table';
import type { FilterValue, SorterResult } from 'antd/es/table/interface';
import Link from 'next/link';
import React, { useState } from 'react';

import { getApiAdminOperationsSummary } from '@/api/generated/admin/admin';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import ReceiptReprintWizard from '@/features/operations-center/components/ReceiptReprintWizard';
import ReceiptsFilterBar from '@/features/receipts/components/ReceiptsFilterBar';
import ReceiptsTable from '@/features/receipts/components/ReceiptsTable';
import { useReceiptListQuery } from '@/features/receipts/hooks/useReceiptListQuery';
import type { ReceiptListItemDto, ReceiptListParams } from '@/features/receipts/types/receipts';
import { RECEIPT_LIST_DEFAULTS } from '@/features/receipts/types/receipts';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

export default function OperationsCenterView() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const [listParams, setListParams] = useState<ReceiptListParams>(RECEIPT_LIST_DEFAULTS);
  const {
    data: listData,
    isLoading,
    isPlaceholderData,
    isError,
    error,
  } = useReceiptListQuery(listParams);

  const canReprint = hasPermission(PERMISSIONS.RECEIPT_REPRINT);
  const canSeeOpsSummary = hasPermission(PERMISSIONS.REPORT_VIEW);
  const canSeeExportDiag = hasPermission(PERMISSIONS.REPORT_EXPORT);

  const [reprintOpen, setReprintOpen] = useState(false);
  const [reprintPaymentId, setReprintPaymentId] = useState('');
  const [reprintReceiptNumberHint, setReprintReceiptNumberHint] = useState<string | undefined>();

  const summaryQuery = useQuery({
    queryKey: ['operations-center', 'admin-operations-summary', 24],
    queryFn: () => getApiAdminOperationsSummary({ windowHours: 24 }),
    enabled: canSeeOpsSummary,
    staleTime: 60_000,
  });

  const parsedSort = listParams.sort?.split(':') ?? [];
  const sortField = parsedSort[0];
  const sortOrder = parsedSort[1] === 'asc' ? ('ascend' as const) : ('descend' as const);

  const handleTableChange = (
    pagination: TablePaginationConfig,
    _filters: Record<string, FilterValue | null>,
    sorter: SorterResult<ReceiptListItemDto> | SorterResult<ReceiptListItemDto>[]
  ) => {
    const single = Array.isArray(sorter) ? sorter[0] : sorter;
    const updates: Partial<ReceiptListParams> = {
      page: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 25,
    };
    if (single?.field && single?.order) {
      const dir = single.order === 'ascend' ? 'asc' : 'desc';
      updates.sort = `${String(single.field)}:${dir}`;
    }
    setListParams((p) => ({ ...p, ...updates }));
  };

  const handleFilterChange = (values: Partial<ReceiptListParams>) => {
    setListParams((p) => ({ ...p, ...values, page: 1 }));
  };

  const resetFilters = () => {
    setListParams(RECEIPT_LIST_DEFAULTS);
  };

  const title = t('nav.operationsCenter');
  const emptyListHint =
    !isError && !isLoading && !listData?.items?.length
      ? t('adminShell.operationsCenter.emptyReceipts')
      : undefined;

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <ReceiptReprintWizard
        open={reprintOpen}
        onClose={() => setReprintOpen(false)}
        paymentId={reprintPaymentId}
        receiptNumberHint={reprintReceiptNumberHint}
      />

      <AdminPageHeader title={title} breadcrumbs={[adminOverviewCrumb(t), { title }]}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('adminShell.operationsCenter.intro')}
        </Typography.Paragraph>
      </AdminPageHeader>

      <Card title={t('adminShell.operationsCenter.shortcutsTitle')}>
        <Space wrap size="middle">
          <Link href="/tagesabschluss">
            <Button icon={<CalendarOutlined />}>{t('nav.tagesabschluss')}</Button>
          </Link>
          <Link href="/receipts">
            <Button icon={<FileSearchOutlined />}>{t('nav.receipts')}</Button>
          </Link>
          <Link href="/audit-logs">
            <Button icon={<SafetyOutlined />}>{t('nav.auditLogs')}</Button>
          </Link>
          <Link href="/dashboard">
            <Button icon={<DashboardOutlined />}>
              {t('adminShell.operationsCenter.dashboardReports')}
            </Button>
          </Link>
          <Link href="/rksv">
            <Button icon={<LinkOutlined />}>{t('nav.rksvOperationsOverview')}</Button>
          </Link>
        </Space>
      </Card>

      <Row gutter={[16, 16]}>
        {canSeeOpsSummary ? (
          <Col xs={24} lg={12}>
            <Card
              title={t('adminShell.operationsCenter.exceptionQueueTitle')}
              loading={summaryQuery.isLoading}
            >
              {summaryQuery.isError ? (
                <Alert
                  type="error"
                  title={t('adminShell.operationsCenter.exceptionQueueLoadError')}
                  showIcon
                />
              ) : (
                <Row gutter={16}>
                  <Col span={12}>
                    <Statistic
                      title={t('adminShell.operationsCenter.replayBacklog')}
                      value={summaryQuery.data?.replayBacklogCount ?? 0}
                      prefix={<WarningOutlined />}
                    />
                  </Col>
                  <Col span={12}>
                    <Statistic
                      title={t('adminShell.operationsCenter.exportRisk')}
                      value={summaryQuery.data?.exportRisk?.totalRiskCount ?? 0}
                    />
                  </Col>
                </Row>
              )}
              <div style={{ marginTop: 12 }}>
                <Link href="/rksv/replay-batch">{t('adminShell.operationsCenter.openReplay')}</Link>
                {' · '}
                <Link href="/rksv/incident">{t('adminShell.operationsCenter.openIncident')}</Link>
                {canSeeExportDiag ? (
                  <>
                    {' · '}
                    <Link href="/rksv/fiscal-export-diagnostics">
                      {t('adminShell.operationsCenter.openFiscalExport')}
                    </Link>
                  </>
                ) : null}
              </div>
            </Card>
          </Col>
        ) : null}

        <Col xs={24} lg={canSeeOpsSummary ? 12 : 24}>
          <Card title={t('adminShell.operationsCenter.reportsQuickTitle')}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
              {t('adminShell.operationsCenter.reportsQuickHint')}
            </Typography.Paragraph>
            <Space wrap>
              <Link href="/dashboard">
                <Button type="primary">
                  {t('adminShell.operationsCenter.openDashboardReports')}
                </Button>
              </Link>
              <Link href="/payments">
                <Button>{t('nav.payments')}</Button>
              </Link>
            </Space>
            <Alert
              style={{ marginTop: 12 }}
              type="info"
              showIcon
              title={t('adminShell.operationsCenter.xzHint')}
            />
          </Card>
        </Col>
      </Row>

      <Card
        title={t('adminShell.operationsCenter.advancedSearchTitle')}
        extra={
          <Link href="/receipts">
            <Button type="link">{t('adminShell.operationsCenter.fullReceiptSearch')}</Button>
          </Link>
        }
      >
        {isError ? (
          <Alert
            type="error"
            showIcon
            style={{ marginBottom: 12 }}
            title={t('receipts.list.loadErrorFallback')}
            description={
              error ? (
                <ApiErrorAlertDescription
                  t={t}
                  error={error}
                  logContext="OperationsCenterView.receipts"
                  fallbackKey="receipts.list.loadErrorFallback"
                />
              ) : (
                t('receipts.list.loadErrorFallback')
              )
            }
          />
        ) : null}
        <ReceiptsFilterBar
          initialValues={listParams}
          onFilterChange={handleFilterChange}
          onReset={resetFilters}
          loading={isLoading}
        />
        {canReprint ? (
          <Alert
            style={{ marginBottom: 12 }}
            type="info"
            showIcon
            title={t('adminShell.operationsCenter.reprintWorkflowBanner')}
          />
        ) : null}
        <ReceiptsTable
          data={listData?.items ?? []}
          loading={isLoading}
          isPlaceholderData={isPlaceholderData}
          pagination={{
            current: listData?.page ?? listParams.page,
            pageSize: listData?.pageSize ?? listParams.pageSize,
            total: listData?.totalCount ?? 0,
          }}
          sortField={sortField}
          sortOrder={sortOrder}
          emptyText={emptyListHint}
          onTableChange={handleTableChange}
          reprintEnabled={canReprint}
          reprintActionLabel={t('adminShell.operationsCenter.reprintWorkflowAction')}
          onStartReprint={(row) => {
            setReprintPaymentId(row.paymentId?.trim() ?? '');
            setReprintReceiptNumberHint(row.receiptNumber);
            setReprintOpen(true);
          }}
        />
      </Card>

      {canReprint ? (
        <Card title={t('adminShell.operationsCenter.reprintTitle')} extra={<PrinterOutlined />}>
          <Typography.Paragraph type="secondary">
            {t('adminShell.operationsCenter.reprintHint')}
          </Typography.Paragraph>
          <Typography.Paragraph style={{ marginBottom: 0 }}>
            <Button
              type="primary"
              onClick={() => {
                setReprintPaymentId('');
                setReprintReceiptNumberHint(undefined);
                setReprintOpen(true);
              }}
            >
              {t('adminShell.operationsCenter.reprintOpenWorkflow')}
            </Button>
          </Typography.Paragraph>
        </Card>
      ) : (
        <Alert
          type="warning"
          showIcon
          title={t('adminShell.operationsCenter.reprintNoPermission')}
        />
      )}
    </Space>
  );
}
