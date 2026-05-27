'use client';

import React, { Suspense, useCallback, useMemo, useState } from 'react';
import { Card, Typography, Tag, Space, Button, message, Alert, Spin, Tooltip } from 'antd';
import { CalendarOutlined, DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import type { AuditLogEntryDto } from '@/api/generated/model';
import type { Dayjs } from 'dayjs';
import Link from 'next/link';
import { useGetApiAuditLog } from '@/api/generated/audit-log/audit-log';
import dayjs from 'dayjs';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { formatNumber } from '@/i18n/formatting';
import { useAuditLogSearchParams } from '@/features/audit-logs/hooks/useAuditLogSearchParams';
import { toAuditLogStatusApiParam, type AuditLogStatusFilter } from '@/features/audit-logs/constants/auditLogFilters';
import { AuditFilterBar } from '@/features/audit/components/AuditFilterBar';
import { AuditDetailModal } from '@/features/audit/components/AuditDetailModal';
import { AuditExportModal } from '@/features/audit/components/AuditExportModal';
import { AuditRetentionPanel } from '@/features/audit/components/AuditRetentionPanel';
import { ScheduleReportModal } from '@/features/audit/components/ScheduleReportModal';
import { AuditLogTable } from '@/features/audit-logs/components/AuditLogTable';
import { useAuditLogUserFilterOptions } from '@/features/audit-logs/hooks/useAuditLogUserFilterOptions';
import { getAuditActionLabelKey } from '@/features/audit-logs/utils/auditActionLabels';

function getAuditListErrorMessage(error: unknown, translate: (key: string) => string): string {
    if (error instanceof Error) return error.message;
    return translate('common.messages.noTechnicalDetail');
}

function AuditLogsPageContent() {
    const { t, formatLocale } = useI18n();
    const { params, setParams, resetFilters } = useAuditLogSearchParams();
    const [exportOpen, setExportOpen] = useState(false);
    const [scheduleOpen, setScheduleOpen] = useState(false);
    const [detailRecord, setDetailRecord] = useState<AuditLogEntryDto | null>(null);

    const dateRange = useMemo((): [Dayjs | null, Dayjs | null] | null => {
        if (!params.startDate && !params.endDate) return null;
        return [params.startDate ? dayjs(params.startDate) : null, params.endDate ? dayjs(params.endDate) : null];
    }, [params.startDate, params.endDate]);

    const queryParams = useMemo(
        () => ({
            page: params.page,
            pageSize: params.pageSize,
            startDate: params.startDate ? dayjs(params.startDate).startOf('day').toISOString() : undefined,
            endDate: params.endDate ? dayjs(params.endDate).endOf('day').toISOString() : undefined,
            action: params.action,
            userId: params.userId,
            targetUserId: params.targetUserId,
            entityType: params.entityType,
            entityId: params.entityId,
            ipAddress: params.ipAddress,
            status: toAuditLogStatusApiParam(params.status) as never,
            statusOutcome: params.statusOutcome,
            hasChanges: params.hasChanges,
        }),
        [params],
    );

    const { data, isLoading, isFetching, isError, error, refetch } = useGetApiAuditLog(queryParams);

    const { resolveLabel: resolveUserLabel } = useAuditLogUserFilterOptions();

    const dateRangeIncomplete = Boolean(
        dateRange && ((dateRange[0] && !dateRange[1]) || (!dateRange[0] && dateRange[1])),
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
            (params.startDate && params.endDate),
    );

    const actionOptionLabel = useCallback(
        (value: string) => {
            const labelKey = getAuditActionLabelKey(value);
            return labelKey ? t(labelKey as 'common.auditLogs.actionLabels.login') : value;
        },
        [t],
    );

    const statusLabel = useCallback(
        (status: AuditLogStatusFilter) =>
            t(`common.auditLogs.statusLabels.${status}` as 'common.auditLogs.statusLabels.Success'),
        [t],
    );

    const scopeSummary = useMemo(() => {
        const parts: string[] = [
            t('common.auditLogs.scopePage', { page: String(params.page) }),
            t('common.auditLogs.scopeRowsPerRequest', { pageSize: String(params.pageSize) }),
            data?.totalCount != null
                ? t('common.auditLogs.scopeTotalEntries', {
                      total: formatNumber(data.totalCount, formatLocale, { maximumFractionDigits: 0 }),
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
        if (params.status) parts.push(t('common.auditLogs.scopeStatusIs', { status: statusLabel(params.status) }));
        if (params.startDate && params.endDate) {
            parts.push(
                `${dayjs(params.startDate).format('DD.MM.YYYY')}–${dayjs(params.endDate).format('DD.MM.YYYY')}`,
            );
        } else {
            parts.push(t('common.auditLogs.scopeNoDateFilter'));
        }
        parts.push(t('common.auditLogs.scopeApiPageNote'));
        return parts.join(' · ');
    }, [params, data?.totalCount, formatLocale, t, statusLabel, actionOptionLabel]);

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
                        <Button icon={<DownloadOutlined />} onClick={() => setExportOpen(true)} disabled={isLoading}>
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
                    <strong>{t('common.auditLogs.introStrongTable')}</strong> {t('common.auditLogs.introTrail')}
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12, maxWidth: 720 }}>
                    {t('common.auditLogs.forensicsHint')}{' '}
                    <Link href="/rksv/verifications">{t('common.auditLogs.forensicsLinkVerifications')}</Link>
                </Typography.Paragraph>
            </AdminPageHeader>

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
                    onStatusOutcomeChange={(statusOutcome) =>
                        setParams({ statusOutcome, status: undefined })
                    }
                    onHasChangesChange={(hasChanges) => setParams({ hasChanges })}
                    onDateRangeChange={(startDate, endDate) => setParams({ startDate, endDate })}
                    onClearFilters={resetFilters}
                />
            </Card>

            {dateRangeIncomplete ? (
                <Alert
                    type="warning"
                    showIcon
                    style={{ marginTop: 8 }}
                    message={t('common.auditLogs.dateRangeIncompleteTitle')}
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
                                <Tag
                                    closable
                                    onClose={() => setParams({ startDate: undefined, endDate: undefined })}
                                >
                                    {t('common.auditLogs.tagDateRangePrefix')}{' '}
                                    {dayjs(params.startDate).format('DD.MM.YYYY')} –{' '}
                                    {dayjs(params.endDate).format('DD.MM.YYYY')}
                                </Tag>
                            ) : null}
                            <Button type="link" size="small" onClick={resetFilters}>
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
                    message={t('common.auditLogs.errorTitle')}
                    description={getAuditListErrorMessage(error, t)}
                    showIcon
                    action={
                        <Space direction="vertical" size="small">
                            <Button size="small" onClick={() => refetch()}>
                                {t('common.buttons.retry')}
                            </Button>
                            <Button size="small" type="link" onClick={resetFilters} style={{ padding: 0, height: 'auto' }}>
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
                    page={params.page}
                    pageSize={params.pageSize}
                    total={data?.totalCount ?? 0}
                    hasActiveFilters={hasActiveFilters}
                    onPageChange={(p, s) => setParams({ page: p, pageSize: s })}
                    onRowClick={setDetailRecord}
                />
            ) : null}

            <div style={{ marginTop: 16 }}>
                <AuditRetentionPanel />
            </div>

            <AuditExportModal open={exportOpen} params={params} onClose={() => setExportOpen(false)} />
            <ScheduleReportModal open={scheduleOpen} params={params} onClose={() => setScheduleOpen(false)} />
            <AuditDetailModal
                open={!!detailRecord}
                record={detailRecord}
                onClose={() => setDetailRecord(null)}
            />
        </AdminPageShell>
    );
}

function AuditLogsLoadingFallback() {
    const { t } = useI18n();
    return (
        <div style={{ padding: 80, textAlign: 'center' }}>
            <Spin size="large" tip={t('common.loading.data')} />
        </div>
    );
}

export default function AuditLogsPage() {
    return (
        <Suspense fallback={<AuditLogsLoadingFallback />}>
            <AuditLogsPageContent />
        </Suspense>
    );
}
