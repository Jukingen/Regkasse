'use client';

import React, { Suspense, useCallback, useMemo } from 'react';
import {
    Table,
    Card,
    Typography,
    Tag,
    Space,
    Button,
    Select,
    DatePicker,
    message,
    Alert,
    Empty,
    Row,
    Col,
    Tooltip,
    Spin,
} from 'antd';
import { ClearOutlined, ReloadOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import Link from 'next/link';
import { useGetApiAuditLog } from '@/api/generated/audit-log/audit-log';
import type { AuditLogEntryDto } from '@/api/generated/model';
import dayjs from 'dayjs';
import { viewAuditLogStatusPresentation } from '@/shared/verificationsAuditView';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { formatNumber } from '@/i18n/formatting';
import { useAuditLogSearchParams } from '@/features/audit-logs/hooks/useAuditLogSearchParams';
import { adminTableScrollXy, shouldUseAdminTableVirtual } from '@/components/ui/adminTableVirtual';
import {
    AUDIT_ACTION_FILTER_VALUES,
    toAuditLogStatusApiParam,
    type AuditLogStatusFilter,
} from '@/features/audit-logs/constants/auditLogFilters';
import { buildAuditLogExportQuery } from '@/features/audit-logs/utils/buildAuditLogExportQuery';
import { downloadAuditLogExport } from '@/features/audit-logs/utils/exportAuditLogs';
import { UserFilterSelect } from '@/features/audit-logs/components/UserFilterSelect';
import { EntityTypeFilterSelect } from '@/features/audit-logs/components/EntityTypeFilterSelect';
import { StatusFilterSelect } from '@/features/audit-logs/components/StatusFilterSelect';
import { useAuditLogUserFilterOptions } from '@/features/audit-logs/hooks/useAuditLogUserFilterOptions';
import { formatAuditLogDescription } from '@/features/audit-logs/utils/formatAuditLogDescription';

const { RangePicker } = DatePicker;

function getAuditListErrorMessage(error: unknown, translate: (key: string) => string): string {
    if (error instanceof Error) return error.message;
    return translate('common.messages.noTechnicalDetail');
}

function AuditLogsPageContent() {
    const { t, formatLocale } = useI18n();
    const { params, setParams, resetFilters } = useAuditLogSearchParams();

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
            entityType: params.entityType,
            entityId: params.entityId,
            status: toAuditLogStatusApiParam(params.status) as never,
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
            params.entityType ||
            params.status ||
            (params.startDate && params.endDate),
    );

    const actionFilterOptions = useMemo(
        () =>
            AUDIT_ACTION_FILTER_VALUES.map((value) => ({
                value,
                label: t(
                    value === 'Login'
                        ? 'common.auditLogs.actionLabels.login'
                        : value === 'CreateInvoice'
                          ? 'common.auditLogs.actionLabels.createInvoice'
                          : 'common.auditLogs.actionLabels.payment',
                ),
            })),
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
        if (params.action) parts.push(t('common.auditLogs.scopeActionIs', { action: params.action }));
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
    }, [params, data?.totalCount, formatLocale, t, statusLabel]);

    const actionOptionLabel = useCallback(
        (value: string) => {
            const opt = actionFilterOptions.find((o) => o.value === value);
            return opt?.label ?? value;
        },
        [actionFilterOptions],
    );

    const handleExport = useCallback(
        async (format: 'json' | 'csv') => {
            try {
                await downloadAuditLogExport(format, buildAuditLogExportQuery(params), {
                    exportFailedMessage: t('common.auditLogs.exportFailed'),
                });
                message.success(t('common.auditLogs.exportStarted'));
            } catch (e) {
                const msg = e instanceof Error ? e.message : t('common.auditLogs.exportFailed');
                message.error(msg);
            }
        },
        [params, t],
    );

    const columns = useMemo(
        () => [
            {
                title: t('common.auditLogs.table.time'),
                dataIndex: 'timestamp',
                key: 'timestamp',
                width: 168,
                render: (ts: string | undefined) => (
                    <Typography.Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
                        {ts ? dayjs(ts).format('DD.MM.YYYY HH:mm:ss') : '—'}
                    </Typography.Text>
                ),
            },
            {
                title: t('common.auditLogs.table.correlation'),
                key: 'correlationId',
                width: 112,
                ellipsis: true,
                render: (_: unknown, record: AuditLogEntryDto) => {
                    const c = record.correlationId?.trim();
                    if (!c) return <Typography.Text type="secondary">—</Typography.Text>;
                    return (
                        <Typography.Text code copyable={{ text: c }} ellipsis style={{ fontSize: 11, maxWidth: 104 }}>
                            {c.length > 14 ? `${c.slice(0, 12)}…` : c}
                        </Typography.Text>
                    );
                },
            },
            {
                title: t('common.auditLogs.table.user'),
                key: 'userName',
                width: 140,
                ellipsis: true,
                render: (_: unknown, record: AuditLogEntryDto) => (
                    <Typography.Text type="secondary" ellipsis={{ tooltip: true }}>
                        {record.actorDisplayName ?? record.createdBy ?? record.userId ?? '—'}
                    </Typography.Text>
                ),
            },
            {
                title: t('common.auditLogs.table.action'),
                dataIndex: 'action',
                key: 'action',
                width: 200,
                ellipsis: true,
                render: (action: string | null | undefined) => <Tag color="blue">{action ?? '—'}</Tag>,
            },
            {
                title: t('common.auditLogs.table.entity'),
                key: 'entity',
                width: 200,
                render: (_: unknown, record: AuditLogEntryDto) => {
                    const type = record.entityType?.trim() || '—';
                    const id = record.entityId?.trim();
                    if (!id) {
                        return <Typography.Text strong>{type}</Typography.Text>;
                    }
                    return (
                        <Space direction="vertical" size={0} style={{ maxWidth: 220 }}>
                            <Typography.Text strong ellipsis style={{ display: 'block' }}>
                                {type}
                            </Typography.Text>
                            <Typography.Text
                                type="secondary"
                                ellipsis={{ tooltip: true }}
                                copyable={{ text: id }}
                                style={{ display: 'block', fontSize: 12, fontFamily: 'monospace' }}
                            >
                                {id}
                            </Typography.Text>
                        </Space>
                    );
                },
            },
            {
                title: t('common.auditLogs.table.details'),
                dataIndex: 'description',
                key: 'description',
                ellipsis: true,
                render: (text: string | null | undefined, record: AuditLogEntryDto) => {
                    const detailText = formatAuditLogDescription(record, t) || text?.trim();
                    if (!detailText) return <Typography.Text type="secondary">—</Typography.Text>;
                    return (
                        <Tooltip title={detailText}>
                            <Typography.Text ellipsis style={{ maxWidth: 320, display: 'block' }}>
                                {detailText}
                            </Typography.Text>
                        </Tooltip>
                    );
                },
            },
            {
                title: t('common.auditLogs.table.status'),
                dataIndex: 'status',
                key: 'status',
                width: 120,
                align: 'center' as const,
                render: (_: unknown, record: AuditLogEntryDto) => {
                    const p = viewAuditLogStatusPresentation(record.status);
                    return <Tag color={p.antColor}>{p.label}</Tag>;
                },
            },
        ],
        [t],
    );

    const rows = data?.auditLogs ?? [];
    const emptyList = !isLoading && !isError && rows.length === 0;

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
                        <Button onClick={() => handleExport('json')} disabled={isLoading}>
                            {t('common.auditLogs.exportJson')}
                        </Button>
                        <Button onClick={() => handleExport('csv')} disabled={isLoading}>
                            {t('common.auditLogs.exportCsv')}
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
                <Row gutter={[12, 12]} align="middle">
                    <Col xs={24} sm={12} md={6} lg={5}>
                        <UserFilterSelect
                            value={params.userId}
                            onChange={(userId) => setParams({ userId })}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={6} lg={5}>
                        <EntityTypeFilterSelect
                            value={params.entityType}
                            onChange={(entityType) => setParams({ entityType })}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={6} lg={4}>
                        <StatusFilterSelect
                            value={params.status}
                            onChange={(status) => setParams({ status })}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={6} lg={5}>
                        <Select
                            placeholder={t('common.auditLogs.actionPlaceholder')}
                            style={{ width: '100%' }}
                            allowClear
                            value={params.action}
                            onChange={(value) => setParams({ action: value ?? undefined })}
                            options={actionFilterOptions}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={8} lg={5}>
                        <RangePicker
                            style={{ width: '100%' }}
                            value={dateRange ?? undefined}
                            onChange={(dates) =>
                                setParams({
                                    startDate: dates?.[0]?.format('YYYY-MM-DD'),
                                    endDate: dates?.[1]?.format('YYYY-MM-DD'),
                                })
                            }
                            format="DD.MM.YYYY"
                        />
                    </Col>
                    <Col xs={24} sm={12} md={4} lg={24} style={{ textAlign: 'right' }}>
                        <Button icon={<ClearOutlined />} onClick={resetFilters}>
                            {t('common.auditLogs.clearFilters')}
                        </Button>
                    </Col>
                </Row>
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
                <Table<AuditLogEntryDto>
                    columns={columns}
                    dataSource={rows}
                    loading={isLoading}
                    virtual={shouldUseAdminTableVirtual(rows.length)}
                    rowKey={(r) => r.id ?? `${r.timestamp ?? ''}-${r.action ?? ''}`}
                    size="middle"
                    scroll={adminTableScrollXy(1240, rows.length)}
                    pagination={{
                        current: params.page,
                        pageSize: params.pageSize,
                        total: data?.totalCount ?? 0,
                        showSizeChanger: true,
                        pageSizeOptions: ['10', '25', '50', '100'],
                        showTotal: (total, range) => {
                            if (total <= 0) return t('common.auditLogs.paginationZero');
                            return t('common.auditLogs.paginationRangeOfTotal', {
                                from: String(range[0] ?? 0),
                                to: String(range[1] ?? 0),
                                total: formatNumber(total, formatLocale, { maximumFractionDigits: 0 }),
                            });
                        },
                        hideOnSinglePage: false,
                        onChange: (p, s) => {
                            setParams({ page: p, pageSize: s ?? params.pageSize });
                        },
                    }}
                    locale={{
                        emptyText: emptyList ? (
                            <Empty
                                description={
                                    hasActiveFilters
                                        ? t('common.auditLogs.emptyFiltered')
                                        : t('common.auditLogs.emptyNoRows')
                                }
                                image={Empty.PRESENTED_IMAGE_SIMPLE}
                            />
                        ) : (
                            <Empty
                                description={t('common.auditLogs.emptyNoRows')}
                                image={Empty.PRESENTED_IMAGE_SIMPLE}
                            />
                        ),
                    }}
                />
            ) : null}
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
