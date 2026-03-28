'use client';

/**
 * FinanzOnline Outbox — operative Sicht auf die SOAP/Outbox-Pipeline (kein Payment-Zeilen-Legacy).
 * Liest nur freigegebene Felder; keine Roh-XML/Credentials (Backend redacted).
 */

import React, { useCallback, useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    DatePicker,
    Descriptions,
    Drawer,
    Empty,
    Input,
    Select,
    Space,
    Spin,
    Table,
    Tag,
    Tooltip,
    Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';
import utc from 'dayjs/plugin/utc';
import Link from 'next/link';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { getApiAdminFinanzonlineOutbox, getApiAdminFinanzonlineOutboxId } from '@/api/generated/admin/admin';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { FinanzOnlineOutboxItemDto } from '@/api/generated/model/finanzOnlineOutboxItemDto';
import type { GetApiAdminFinanzonlineOutboxParams } from '@/api/generated/model/getApiAdminFinanzonlineOutboxParams';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

dayjs.extend(utc);

function statusTagColor(status: string | null | undefined): string {
    if (!status) return 'default';
    switch (status) {
        case 'ProtocolSuccess':
            return 'success';
        case 'Pending':
            return 'blue';
        case 'Processing':
            return 'processing';
        case 'AwaitingProtocol':
            return 'cyan';
        case 'RetryableFailure':
            return 'orange';
        case 'ProtocolFailure':
        case 'PermanentFailure':
            return 'error';
        case 'ManualReviewRequired':
            return 'gold';
        case 'DeadLetter':
            return 'magenta';
        default:
            return 'default';
    }
}

export default function FinanzOnlineOutboxPage() {
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const [bucket, setBucket] = useState<string>('all');
    const [statusCsv, setStatusCsv] = useState<string>('');
    const [mode, setMode] = useState<string | undefined>(undefined);
    const [correlationId, setCorrelationId] = useState<string>('');
    const [businessKey, setBusinessKey] = useState<string>('');
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([null, null]);
    const [drawerOpen, setDrawerOpen] = useState(false);
    const [selectedId, setSelectedId] = useState<string | null>(null);
    const [selectedRow, setSelectedRow] = useState<FinanzOnlineOutboxItemDto | null>(null);

    const emDash = useMemo(() => t('finanzOnlineOutbox.format.emptyValue'), [t]);
    const utcSuffix = useMemo(() => t('finanzOnlineOutbox.format.utcSuffix'), [t]);

    const fmtUtc = useCallback(
        (iso: string | undefined | null) => {
            if (!iso) return emDash;
            const d = dayjs(iso);
            return d.isValid() ? d.utc().format('YYYY-MM-DD HH:mm:ss') + utcSuffix : emDash;
        },
        [emDash, utcSuffix]
    );

    const modeTag = useCallback(
        (modeVal: string | null | undefined) => {
            const m = (modeVal ?? 'TEST').toUpperCase();
            return <Tag color={m === 'PROD' ? 'red' : 'blue'}>{m}</Tag>;
        },
        []
    );

    const bucketOptions = useMemo(
        () => [
            { value: 'all', label: t('finanzOnlineOutbox.buckets.all') },
            { value: 'in_flight', label: t('finanzOnlineOutbox.buckets.inFlight') },
            { value: 'pending', label: t('finanzOnlineOutbox.buckets.pending') },
            { value: 'processing', label: t('finanzOnlineOutbox.buckets.processing') },
            { value: 'AwaitingProtocol', label: t('finanzOnlineOutbox.buckets.awaitingProtocol') },
            { value: 'RetryableFailure', label: t('finanzOnlineOutbox.buckets.retryableFailure') },
            { value: 'PermanentFailure', label: t('finanzOnlineOutbox.buckets.permanentFailure') },
            { value: 'ProtocolFailure', label: t('finanzOnlineOutbox.buckets.protocolFailure') },
            { value: 'ManualReviewRequired', label: t('finanzOnlineOutbox.buckets.manualReviewRequired') },
            { value: 'ProtocolSuccess', label: t('finanzOnlineOutbox.buckets.protocolSuccess') },
            { value: 'dead_letter', label: t('finanzOnlineOutbox.buckets.deadLetter') },
        ],
        [t]
    );

    const listParams: GetApiAdminFinanzonlineOutboxParams = useMemo(() => {
        const p: GetApiAdminFinanzonlineOutboxParams = { limit: 200 };
        const trimmedStatus = statusCsv.trim();
        if (trimmedStatus) {
            p.status = trimmedStatus;
        } else if (bucket && bucket !== 'all') {
            p.bucket = bucket;
        }
        const m = mode?.trim();
        if (m) p.mode = m;
        const c = correlationId.trim();
        if (c) p.correlationId = c;
        const bk = businessKey.trim();
        if (bk) p.businessKey = bk;
        if (dateRange[0]) p.fromUtc = dateRange[0].startOf('day').toISOString();
        if (dateRange[1]) p.toUtc = dateRange[1].endOf('day').toISOString();
        return p;
    }, [bucket, statusCsv, mode, correlationId, businessKey, dateRange]);

    const {
        data: listData,
        isLoading,
        isFetching,
        error,
    } = useQuery({
        queryKey: rksvAdminQueryKeys.finanzOnlineOutbox.list(listParams),
        queryFn: () => getApiAdminFinanzonlineOutbox(listParams),
        staleTime: 15_000,
    });

    const { data: detailData, isLoading: detailLoading } = useQuery({
        queryKey: rksvAdminQueryKeys.finanzOnlineOutbox.detail(selectedId ?? ''),
        queryFn: () => getApiAdminFinanzonlineOutboxId(selectedId!),
        enabled: Boolean(drawerOpen && selectedId),
        staleTime: 10_000,
    });

    const displayRow = detailData ?? selectedRow;

    const openDrawer = useCallback((row: FinanzOnlineOutboxItemDto) => {
        const id = row.outboxId;
        if (!id) return;
        setSelectedRow(row);
        setSelectedId(id);
        setDrawerOpen(true);
    }, []);

    const closeDrawer = useCallback(() => {
        setDrawerOpen(false);
        setSelectedId(null);
        setSelectedRow(null);
    }, []);

    const columns: ColumnsType<FinanzOnlineOutboxItemDto> = useMemo(
        () => [
            {
                title: t('finanzOnlineOutbox.table.columns.outboxId'),
                dataIndex: 'outboxId',
                key: 'outboxId',
                width: 280,
                ellipsis: true,
                render: (v: string) => (
                    <Typography.Text copyable={{ text: v }} style={{ fontSize: 12 }}>
                        {v}
                    </Typography.Text>
                ),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.businessKey'),
                dataIndex: 'businessKey',
                width: 160,
                ellipsis: true,
            },
            {
                title: t('finanzOnlineOutbox.table.columns.correlation'),
                dataIndex: 'correlationId',
                width: 200,
                ellipsis: true,
                render: (v: string | null | undefined) =>
                    v ? (
                        <Typography.Text copyable={{ text: v }} style={{ fontSize: 12 }}>
                            {v}
                        </Typography.Text>
                    ) : (
                        emDash
                    ),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.mode'),
                dataIndex: 'mode',
                width: 88,
                render: (_: unknown, r) => modeTag(r.mode),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.status'),
                key: 'status',
                width: 200,
                render: (_: unknown, r) => (
                    <Space direction="vertical" size={0}>
                        <Tag color={statusTagColor(r.status)}>{r.operatorStatusLabel ?? r.status ?? emDash}</Tag>
                        <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                            {r.status}
                        </Typography.Text>
                    </Space>
                ),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.hintOrError'),
                key: 'err',
                width: 220,
                ellipsis: true,
                render: (_: unknown, r) => {
                    const hint = r.operatorFailureHint?.trim();
                    const sum = r.lastErrorSummary?.trim();
                    const sep = ' — ';
                    const line = [hint, sum].filter(Boolean).join(sep) || emDash;
                    return (
                        <Tooltip title={line}>
                            <span>{line}</span>
                        </Tooltip>
                    );
                },
            },
            {
                title: t('finanzOnlineOutbox.table.columns.attempts'),
                dataIndex: 'attemptCount',
                width: 72,
                align: 'right',
            },
            {
                title: t('finanzOnlineOutbox.table.columns.nextAttemptUtc'),
                dataIndex: 'nextAttemptAtUtc',
                width: 168,
                render: (v: string | undefined) => fmtUtc(v),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.paketTransmission'),
                dataIndex: 'transmissionId',
                width: 120,
                ellipsis: true,
            },
            {
                title: t('finanzOnlineOutbox.table.columns.protocol'),
                dataIndex: 'protocolSummary',
                width: 200,
                ellipsis: true,
            },
            {
                title: t('finanzOnlineOutbox.table.columns.createdUtc'),
                dataIndex: 'createdAtUtc',
                width: 168,
                render: (v: string | undefined) => fmtUtc(v),
            },
            {
                title: t('finanzOnlineOutbox.table.columns.processedUtc'),
                dataIndex: 'processedAtUtc',
                width: 168,
                render: (v: string | undefined) => fmtUtc(v),
            },
        ],
        [t, emDash, fmtUtc, modeTag]
    );

    const items = listData?.items ?? [];
    const total = listData?.total ?? 0;

    const pageTitle = t(ADMIN_NAV_LABEL_KEYS.finanzOnlineOutbox);

    return (
        <>
            <AdminPageHeader
                title={pageTitle}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('adminShell.group.rksv'), href: '/rksv' },
                    { title: pageTitle },
                ]}
                actions={
                    <Button
                        icon={<ReloadOutlined />}
                        onClick={() =>
                            queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnlineOutbox.base })
                        }
                    >
                        {t('common.buttons.refresh')}
                    </Button>
                }
            >
                <Typography.Paragraph style={{ marginBottom: 8, maxWidth: 960 }}>
                    <Tag color="green">{t('finanzOnlineOutbox.page.leadTag')}</Tag>{' '}
                    {t('finanzOnlineOutbox.page.leadLine')} {t('finanzOnlineOutbox.page.leadNoPaymentRows')}{' '}
                    <Link href="/rksv/finanz-online-queue">{t('finanzOnlineOutbox.page.leadLegacyQueueLink')}</Link>.
                </Typography.Paragraph>
            </AdminPageHeader>

            <Alert
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
                message={t('finanzOnlineOutbox.privacyAlert.title')}
                description={t('finanzOnlineOutbox.privacyAlert.description')}
            />

            <Card size="small" style={{ marginBottom: 16 }}>
                <Space wrap size="middle" align="start">
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.bucket')}
                        </Typography.Text>
                        <Select
                            style={{ width: 280 }}
                            value={bucket}
                            onChange={setBucket}
                            options={bucketOptions}
                            disabled={Boolean(statusCsv.trim())}
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.statusCsv')}
                        </Typography.Text>
                        <Input
                            style={{ width: 260 }}
                            placeholder={t('finanzOnlineOutbox.filters.statusCsvPlaceholder')}
                            value={statusCsv}
                            onChange={(e) => setStatusCsv(e.target.value)}
                            allowClear
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.mode')}
                        </Typography.Text>
                        <Select
                            style={{ width: 120 }}
                            allowClear
                            placeholder={t('finanzOnlineOutbox.filters.modePlaceholderAll')}
                            value={mode}
                            onChange={setMode}
                            options={[
                                { value: 'TEST', label: 'TEST' },
                                { value: 'PROD', label: 'PROD' },
                            ]}
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.correlationId')}
                        </Typography.Text>
                        <Input
                            style={{ width: 220 }}
                            value={correlationId}
                            onChange={(e) => setCorrelationId(e.target.value)}
                            allowClear
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.businessKey')}
                        </Typography.Text>
                        <Input
                            style={{ width: 200 }}
                            value={businessKey}
                            onChange={(e) => setBusinessKey(e.target.value)}
                            allowClear
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                            {t('finanzOnlineOutbox.filters.createdDateRange')}
                        </Typography.Text>
                        <DatePicker.RangePicker
                            value={dateRange}
                            onChange={(r) => setDateRange(r ?? [null, null])}
                        />
                    </div>
                </Space>
            </Card>

            {error ? (
                <Alert
                    type="error"
                    showIcon
                    message={t('finanzOnlineOutbox.error.listLoadTitle')}
                    description={
                        <ApiErrorAlertDescription
                            t={t}
                            error={error}
                            logContext="FinanzOnlineOutbox.list"
                            fallbackKey="finanzOnlineOutbox.error.listLoadTitle"
                        />
                    }
                />
            ) : null}

            <Spin spinning={isLoading || isFetching}>
                {items.length === 0 && !isLoading && !isFetching ? (
                    <Empty description={t('finanzOnlineOutbox.empty.noRows')} />
                ) : (
                    <Table<FinanzOnlineOutboxItemDto>
                        size="small"
                        rowKey={(r) => r.outboxId ?? `${r.businessKey}-${r.createdAtUtc}`}
                        columns={columns}
                        dataSource={items}
                        loading={isLoading || isFetching}
                        scroll={{ x: 2000 }}
                        pagination={{
                            pageSize: 50,
                            showSizeChanger: true,
                            showTotal: () => t('finanzOnlineOutbox.pagination.showTotal', { total, shown: items.length }),
                        }}
                        onRow={(record) => ({
                            onClick: () => openDrawer(record),
                            style: { cursor: 'pointer' },
                        })}
                    />
                )}
            </Spin>

            <Drawer
                title={t('finanzOnlineOutbox.drawer.title')}
                width={640}
                open={drawerOpen}
                onClose={closeDrawer}
                destroyOnClose
            >
                {detailLoading && !displayRow ? (
                    <Spin />
                ) : displayRow ? (
                    <Descriptions bordered size="small" column={1}>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.outboxId')}>
                            {displayRow.outboxId}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.businessKey')}>
                            {displayRow.businessKey ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.correlation')}>
                            {displayRow.correlationId ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.mode')}>
                            {modeTag(displayRow.mode)}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.statusTechnical')}>
                            {displayRow.status ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.statusDisplay')}>
                            <Tag color={statusTagColor(displayRow.status)}>{displayRow.operatorStatusLabel ?? emDash}</Tag>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.hint')}>
                            {displayRow.operatorFailureHint ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.lastErrorCode')}>
                            {displayRow.lastErrorCode ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.lastErrorSummary')}>
                            {displayRow.lastErrorSummary ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.failureCategory')}>
                            {displayRow.failureCategory ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.attempts')}>
                            {displayRow.attemptCount ?? 0}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.nextAttemptUtc')}>
                            {fmtUtc(displayRow.nextAttemptAtUtc)}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.paketTransmission')}>
                            {displayRow.transmissionId ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.externalReference')}>
                            {displayRow.externalReferenceId ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.externalStatus')}>
                            {displayRow.externalStatus ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.protocolCode')}>
                            {displayRow.protocolCode ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.protocolShort')}>
                            {displayRow.protocolSummary ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.protocolPayloadHash')}>
                            {displayRow.protocolPayloadHash ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.createdUtc')}>
                            {fmtUtc(displayRow.createdAtUtc)}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.processedUtc')}>
                            {fmtUtc(displayRow.processedAtUtc)}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.terminal')}>
                            {displayRow.isTerminal ? t('common.buttons.yes') : t('common.buttons.no')}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.idempotencySuffix')}>
                            {displayRow.idempotencyKeySuffix ?? emDash}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.scope')}>
                            {displayRow.scope
                                ? `${displayRow.scope.tenantId ?? emDash} / ${displayRow.scope.branchId ?? emDash} / ${displayRow.scope.registerId ?? emDash}`
                                : `${displayRow.tenantId ?? emDash} / ${displayRow.branchId ?? emDash} / ${displayRow.registerId ?? emDash}`}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('finanzOnlineOutbox.drawer.fields.responsePreview')}>
                            <Typography.Paragraph style={{ marginBottom: 0, fontSize: 12, whiteSpace: 'pre-wrap' }}>
                                {displayRow.lastResponsePreview ?? emDash}
                            </Typography.Paragraph>
                        </Descriptions.Item>
                    </Descriptions>
                ) : (
                    <Empty />
                )}
            </Drawer>
        </>
    );
}
