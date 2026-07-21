'use client';

import { CalendarOutlined, DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import { keepPreviousData } from '@tanstack/react-query';
import { Alert, Button, Card, Space, Tag, Tooltip, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import Link from 'next/link';
import React, { Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { useGetApiAuditLog } from '@/api/generated/audit-log/audit-log';
import type { AuditLogEntryDto, GetApiAuditLogParams } from '@/api/generated/model';
import { TableSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageScopeSummary, AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { AuditLogTable } from '@/features/audit-logs/components/AuditLogTable';
import {
  type AuditLogStatusFilter,
  toAuditLogStatusApiParam,
} from '@/features/audit-logs/constants/auditLogFilters';
import { useAuditLogSearchParams } from '@/features/audit-logs/hooks/useAuditLogSearchParams';
import { useAuditLogUserFilterOptions } from '@/features/audit-logs/hooks/useAuditLogUserFilterOptions';
import { getAuditActionLabelKey } from '@/features/audit-logs/utils/auditActionLabels';
import { AuditDetailModal } from '@/features/audit/components/AuditDetailModal';
import { AuditExportModal } from '@/features/audit/components/AuditExportModal';
import { AuditFilterBar } from '@/features/audit/components/AuditFilterBar';
import { AuditLogsSubNav } from '@/features/audit/components/AuditLogsSubNav';
import { AuditRetentionPanel } from '@/features/audit/components/AuditRetentionPanel';
import { ManagerAuditLogsDefaultTab } from '@/features/audit/components/ManagerAuditLogsDefaultTab';
import { ScheduleReportModal } from '@/features/audit/components/ScheduleReportModal';
import { useI18n } from '@/i18n';
import { formatDate, formatNumber } from '@/i18n/formatting';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useKeysetCursors } from '@/shared/pagination/useKeysetPageStack';

function getAuditListErrorMessage(error: unknown, translate: (key: string) => string): string {
  if (error instanceof Error) return error.message;
  return translate('common.messages.noTechnicalDetail');
}

function AuditLogsPageContent() {
  const { t, formatLocale } = useI18n();
  const { params, setParams, resetFilters } = useAuditLogSearchParams();
  const { getAfterCursor, shouldIncludeTotalCount, cachedTotal, ingestPageMeta, resetCursors } =
    useKeysetCursors();
  const filterFingerprint = useMemo(
    () =>
      JSON.stringify({
        startDate: params.startDate,
        endDate: params.endDate,
        action: params.action,
        userId: params.userId,
        targetUserId: params.targetUserId,
        entityType: params.entityType,
        entityId: params.entityId,
        ipAddress: params.ipAddress,
        status: params.status,
        statusOutcome: params.statusOutcome,
        hasChanges: params.hasChanges,
        pageSize: params.pageSize,
      }),
    [params]
  );
  const prevFilterFingerprint = useRef(filterFingerprint);
  useEffect(() => {
    if (prevFilterFingerprint.current !== filterFingerprint) {
      resetCursors();
      prevFilterFingerprint.current = filterFingerprint;
    }
  }, [filterFingerprint, resetCursors]);

  const afterCursor = getAfterCursor(params.page);
  const queryParams = useMemo(
    (): GetApiAuditLogParams => ({
      page: params.page,
      pageSize: params.pageSize,
      afterCursor,
      includeTotalCount: shouldIncludeTotalCount(params.page),
      startDate: params.startDate
        ? dayjs(params.startDate).startOf('day').toISOString()
        : undefined,
      endDate: params.endDate ? dayjs(params.endDate).endOf('day').toISOString() : undefined,
      action: params.action,
      userId: params.userId,
      targetUserId: params.targetUserId,
      entityType: params.entityType,
      entityId: params.entityId,
      ipAddress: params.ipAddress,
      status: toAuditLogStatusApiParam(params.status),
      statusOutcome: params.statusOutcome,
      hasChanges: params.hasChanges,
    }),
    [params, afterCursor, shouldIncludeTotalCount]
  );

  const { data, isLoading, isFetching, isPlaceholderData, isError, error, refetch } =
    useGetApiAuditLog(queryParams, { query: { placeholderData: keepPreviousData } });

  useEffect(() => {
    if (!data) return;
    ingestPageMeta(params.page, {
      nextCursor: data.nextCursor,
      hasMore: data.hasMore,
      totalCount: data.totalCount,
    });
  }, [data, params.page, ingestPageMeta]);

  const handleResetFilters = useCallback(() => {
    resetCursors();
    resetFilters();
  }, [resetCursors, resetFilters]);

  const listTotal = cachedTotal ?? data?.totalCount ?? 0;

  const [exportOpen, setExportOpen] = useState(false);
  const [scheduleOpen, setScheduleOpen] = useState(false);
  const [detailRecord, setDetailRecord] = useState<AuditLogEntryDto | null>(null);

  const dateRange = useMemo((): [Dayjs | null, Dayjs | null] | null => {
    if (!params.startDate && !params.endDate) return null;
    return [
      params.startDate ? dayjs(params.startDate) : null,
      params.endDate ? dayjs(params.endDate) : null,
    ];
  }, [params.startDate, params.endDate]);

  const { resolveLabel: resolveUserLabel } = useAuditLogUserFilterOptions();

  const dateRangeIncomplete = Boolean(
    dateRange && ((dateRange[0] && !dateRange[1]) || (!dateRange[0] && dateRange[1]))
  );

  const hasActiveFilters = Boolean(
    params.action ||
    params.userId ||
    params.targetUserId ||
    params.entityType ||
    params.entityId ||
    params.ipAddress ||
    params.status ||
    params.statusOutcome ||
    params.hasChanges ||
    (params.startDate && params.endDate)
  );

  const actionOptionLabel = useCallback(
    (value: string) => {
      const labelKey = getAuditActionLabelKey(value);
      return labelKey ? t(labelKey as 'common.auditLogs.actionLabels.login') : value;
    },
    [t]
  );

  const statusLabel = useCallback(
    (status: AuditLogStatusFilter) =>
      t(`common.auditLogs.statusLabels.${status}` as 'common.auditLogs.statusLabels.Success'),
    [t]
  );

  const scopeSummary = useMemo(() => {
    const parts: string[] = [
      t('common.auditLogs.scopePage', { page: String(params.page) }),
      t('common.auditLogs.scopeRowsPerRequest', { pageSize: String(params.pageSize) }),
      data?.totalCount != null || cachedTotal != null
        ? t('common.auditLogs.scopeTotalEntries', {
            total: formatNumber(listTotal, formatLocale, { maximumFractionDigits: 0 }),
          })
        : t('common.auditLogs.scopeTotalLoading'),
    ];
    if (params.action) {
      parts.push(t('common.auditLogs.scopeActionIs', { action: actionOptionLabel(params.action) }));
    }
    if (params.userId) parts.push(t('common.auditLogs.scopeUserIs'));
    if (params.entityType) {
      parts.push(t('common.auditLogs.scopeEntityTypeIs', { entityType: params.entityType }));
    }
    if (params.status)
      parts.push(t('common.auditLogs.scopeStatusIs', { status: statusLabel(params.status) }));
    if (params.startDate && params.endDate) {
      parts.push(`${formatDate(params.startDate, '')}–${formatDate(params.endDate, '')}`);
    } else {
      parts.push(t('common.auditLogs.scopeNoDateFilter'));
    }
    parts.push(t('common.auditLogs.scopeApiPageNote'));
    return parts.join(' · ');
  }, [
    params,
    listTotal,
    formatLocale,
    t,
    statusLabel,
    actionOptionLabel,
    cachedTotal,
    data?.totalCount,
  ]);

  const rows = data?.auditLogs ?? [];

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t(ADMIN_NAV_LABEL_KEYS.auditLogs)}
        breadcrumbs={[adminOverviewCrumb(t), { title: t(ADMIN_NAV_LABEL_KEYS.auditLogs) }]}
        actions={
          <Space wrap>
            <Tooltip title={t('common.toolbar.refetchHint')}>
              <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching}>
                {t('common.buttons.refresh')}
              </Button>
            </Tooltip>
            <Button
              icon={<DownloadOutlined />}
              onClick={() => setExportOpen(true)}
              disabled={isLoading}
            >
              {t('common.auditLogs.exportButton')}
            </Button>
            <Button icon={<CalendarOutlined />} onClick={() => setScheduleOpen(true)}>
              {t('common.auditLogs.scheduleButton')}
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8, maxWidth: 720 }}>
          {t('common.auditLogs.introLead')}{' '}
          <strong>{t('common.auditLogs.introStrongTable')}</strong>{' '}
          {t('common.auditLogs.introTrail')}
        </Typography.Paragraph>
        <Typography.Paragraph
          type="secondary"
          style={{ marginBottom: 0, fontSize: 12, maxWidth: 720 }}
        >
          {t('common.auditLogs.forensicsHint')}{' '}
          <Link href="/rksv/verifications">{t('common.auditLogs.forensicsLinkVerifications')}</Link>
        </Typography.Paragraph>
      </AdminPageHeader>

      <AuditLogsSubNav />

      <Card size="small" title={t('common.auditLogs.filterCardTitle')}>
        <AuditFilterBar
          action={params.action}
          userId={params.userId}
          targetUserId={params.targetUserId}
          entityType={params.entityType}
          entityId={params.entityId}
          ipAddress={params.ipAddress}
          status={params.status}
          statusOutcome={params.statusOutcome}
          hasChanges={params.hasChanges}
          dateRange={dateRange}
          onActionChange={(action) => setParams({ action })}
          onUserIdChange={(userId) => setParams({ userId })}
          onTargetUserIdChange={(targetUserId) => setParams({ targetUserId })}
          onEntityTypeChange={(entityType) => setParams({ entityType })}
          onEntityIdChange={(entityId) => setParams({ entityId })}
          onIpAddressChange={(ipAddress) => setParams({ ipAddress })}
          onStatusChange={(status) => setParams({ status, statusOutcome: undefined })}
          onStatusOutcomeChange={(statusOutcome) => setParams({ statusOutcome, status: undefined })}
          onHasChangesChange={(hasChanges) => setParams({ hasChanges })}
          onDateRangeChange={(startDate, endDate) => setParams({ startDate, endDate })}
          onClearFilters={handleResetFilters}
        />
      </Card>

      {dateRangeIncomplete ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginTop: 8 }}
          title={t('common.auditLogs.dateRangeIncompleteTitle')}
          description={t('common.auditLogs.dateRangeIncompleteDescription')}
        />
      ) : null}

      {hasActiveFilters ? (
        <div style={{ marginTop: 8 }}>
          <Space wrap size={[8, 8]} align="center">
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('common.auditLogs.activeFiltersLabel')}
            </Typography.Text>
            {params.userId ? (
              <Tag closable onClose={() => setParams({ userId: undefined })}>
                {t('common.auditLogs.tagUserPrefix')} {resolveUserLabel(params.userId)}
              </Tag>
            ) : null}
            {params.entityType ? (
              <Tag closable onClose={() => setParams({ entityType: undefined })}>
                {t('common.auditLogs.tagEntityTypePrefix')} {params.entityType}
              </Tag>
            ) : null}
            {params.status ? (
              <Tag closable onClose={() => setParams({ status: undefined })}>
                {t('common.auditLogs.tagStatusPrefix')} {statusLabel(params.status)}
              </Tag>
            ) : null}
            {params.action ? (
              <Tag closable onClose={() => setParams({ action: undefined })}>
                {t('common.auditLogs.tagActionPrefix')} {actionOptionLabel(params.action)}
              </Tag>
            ) : null}
            {params.startDate && params.endDate ? (
              <Tag closable onClose={() => setParams({ startDate: undefined, endDate: undefined })}>
                {t('common.auditLogs.tagDateRangePrefix')} {formatDate(params.startDate, '')} –{' '}
                {formatDate(params.endDate, '')}
              </Tag>
            ) : null}
            <Button type="link" size="small" onClick={handleResetFilters}>
              {t('common.auditLogs.clearAllFilters')}
            </Button>
          </Space>
        </div>
      ) : null}

      <AdminPageScopeSummary label={t('common.auditLogs.activeViewLabel')}>
        {scopeSummary}
        {isFetching && !isLoading && !isError ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {' '}
            {t('common.auditLogs.refreshingSuffix')}
          </Typography.Text>
        ) : null}
      </AdminPageScopeSummary>

      {isError ? (
        <Alert
          type="error"
          title={t('common.auditLogs.errorTitle')}
          description={getAuditListErrorMessage(error, t)}
          showIcon
          action={
            <Space orientation="vertical" size="small">
              <Button size="small" onClick={() => refetch()}>
                {t('common.buttons.retry')}
              </Button>
              <Button
                size="small"
                type="link"
                onClick={handleResetFilters}
                style={{ padding: 0, height: 'auto' }}
              >
                {t('common.auditLogs.clearAllFilters')}
              </Button>
            </Space>
          }
        />
      ) : null}

      {!isError ? (
        <AuditLogTable
          rows={rows}
          loading={isLoading}
          isPlaceholderData={isPlaceholderData}
          page={params.page}
          pageSize={params.pageSize}
          total={listTotal}
          hasMore={(data as { hasMore?: boolean } | undefined)?.hasMore}
          hasActiveFilters={hasActiveFilters}
          onPageChange={(p, s) => setParams({ page: p, pageSize: s })}
          onRowClick={setDetailRecord}
        />
      ) : null}

      <div style={{ marginTop: 16 }}>
        <AuditRetentionPanel />
      </div>

      <AuditExportModal open={exportOpen} params={params} onClose={() => setExportOpen(false)} />
      <ScheduleReportModal
        open={scheduleOpen}
        params={params}
        onClose={() => setScheduleOpen(false)}
      />
      <AuditDetailModal
        open={!!detailRecord}
        record={detailRecord}
        onClose={() => setDetailRecord(null)}
      />
    </AdminPageShell>
  );
}

function AuditLogsLoadingFallback() {
  return <TableSkeleton rows={8} cols={5} />;
}

export default function AuditLogsPage() {
  return (
    <ManagerAuditLogsDefaultTab fallback={<AuditLogsLoadingFallback />}>
      <Suspense fallback={<AuditLogsLoadingFallback />}>
        <AuditLogsPageContent />
      </Suspense>
    </ManagerAuditLogsDefaultTab>
  );
}
