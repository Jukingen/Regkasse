'use client';

import React, { useCallback, useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Checkbox,
    Collapse,
    DatePicker,
    Descriptions,
    Empty,
    Form,
    Modal,
    Select,
    Space,
    Spin,
    Table,
    Tabs,
    Input,
    Tag,
    Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import Link from 'next/link';
import {
    CopyOutlined,
    DownloadOutlined,
    EyeOutlined,
    HistoryOutlined,
    KeyOutlined,
    PlayCircleOutlined,
} from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { formatBytes, formatDate, formatDateTime } from '@/i18n/formatting';
import { useAntdApp } from '@/hooks/useAntdApp';
import { extractApiErrorMessage, getAdminCashRegisters } from '@/api/admin-rksv/client';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import { useDepExport } from '@/features/rksv/hooks/useDepExport';
import { useCryptoMaterial } from '@/features/rksv/hooks/useCryptoMaterial';
import {
    deactivateDepExportSchedule,
    downloadDepExportHistoryFile,
    fetchDepExportHistoryDetail,
    useDepExportHistory,
    useDepExportSchedules,
    createDepExportSchedule,
    type DepExportHistoryItem,
    type DepExportScheduleItem,
    depExportHistoryQueryKey,
    depExportSchedulesQueryKey,
} from '@/features/rksv/hooks/useDepExportHistory';
import { useQueryClient } from '@tanstack/react-query';
import {
    computeDepExportStats,
    type DepExportRequestParams,
    type CryptoMaterial,
    type RksvDepExportRoot,
} from '@/features/rksv/types/depExport';

const { RangePicker } = DatePicker;

type ScheduleFormValues = {
    scheduleType: string;
    recipientEmails?: string;
};

const DEFAULT_SCHEDULE_DAY_OF_MONTH = 1;
const DEFAULT_SCHEDULE_TIME_OF_DAY = '02:00';

const PRUEFTOOL_COMMAND =
    '.\\scripts\\verify-rksv-dep-export.ps1 -DepExportPath "./dep-export.json" -CryptoMaterialPath "./crypto-material.json"';

function downloadJsonBlob(data: unknown, fileName: string) {
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = globalThis.URL.createObjectURL(blob);
    const anchor = globalThis.document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    globalThis.URL.revokeObjectURL(url);
}

function formatRegisterLabel(registerNumber?: string, location?: string, id?: string): string {
    if (registerNumber && location) return `${registerNumber} — ${location}`;
    if (registerNumber) return registerNumber;
    if (location) return location;
    return id ? `${id.slice(0, 8)}…` : '—';
}

type DepExportSchedulesTabProps = {
    tp: (path: string) => string;
    formatLocale: string;
    selectedRegisterId?: string;
    schedules: DepExportScheduleItem[] | undefined;
    schedulesLoading: boolean;
    onRefetchSchedules: () => Promise<unknown>;
};

function DepExportSchedulesTab({
    tp,
    formatLocale,
    selectedRegisterId,
    schedules,
    schedulesLoading,
    onRefetchSchedules,
}: DepExportSchedulesTabProps) {
    const { message } = useAntdApp();
    const queryClient = useQueryClient();
    const [scheduleForm] = Form.useForm<ScheduleFormValues>();
    const [scheduleSaving, setScheduleSaving] = useState(false);

    const handleScheduleExport = async (values: ScheduleFormValues) => {
        if (!selectedRegisterId) {
            message.warning(tp('selectRegisterWarning'));
            return;
        }

        setScheduleSaving(true);
        try {
            await createDepExportSchedule({
                cashRegisterId: selectedRegisterId,
                scheduleType: values.scheduleType,
                dayOfMonth: DEFAULT_SCHEDULE_DAY_OF_MONTH,
                timeOfDay: DEFAULT_SCHEDULE_TIME_OF_DAY,
                recipientEmails: values.recipientEmails?.trim() || null,
            });
            scheduleForm.resetFields();
            await onRefetchSchedules();
            void queryClient.invalidateQueries({ queryKey: depExportSchedulesQueryKey });
            message.success(tp('scheduleCreated'));
        } catch {
            message.error(tp('scheduleCreateFailed'));
        } finally {
            setScheduleSaving(false);
        }
    };

    const handleDeactivateSchedule = async (scheduleId: string) => {
        try {
            await deactivateDepExportSchedule(scheduleId);
            await onRefetchSchedules();
            message.success(tp('scheduleDeactivated'));
        } catch {
            message.error(tp('scheduleDeactivateFailed'));
        }
    };

    const scheduleColumns: ColumnsType<DepExportScheduleItem> = [
        { title: tp('scheduleColumnType'), dataIndex: 'scheduleType', key: 'scheduleType' },
        {
            title: tp('scheduleColumnNextRun'),
            dataIndex: 'nextRunAt',
            key: 'nextRunAt',
            render: (v: string | null | undefined) =>
                v ? formatDateTime(v, formatLocale) : '—',
        },
        {
            title: tp('scheduleRecipientsPlaceholder'),
            dataIndex: 'recipientEmails',
            key: 'recipientEmails',
            render: (v: string | null | undefined) => v ?? '—',
        },
        {
            title: tp('historyColumnActions'),
            key: 'actions',
            render: (_, row) => (
                <Button size="small" danger onClick={() => void handleDeactivateSchedule(row.id)}>
                    {tp('scheduleDeactivateButton')}
                </Button>
            ),
        },
    ];

    return (
        <Card size="small">
            <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                <Alert type="info" showIcon title={tp('scheduleTitle')} description={tp('scheduleDescription')} />
                <Form
                    form={scheduleForm}
                    layout="vertical"
                    onFinish={(values) => void handleScheduleExport(values)}
                    initialValues={{ scheduleType: 'Monthly' }}
                >
                    <Form.Item name="scheduleType" label={tp('schedulePeriodPlaceholder')}>
                        <Select
                            placeholder={tp('schedulePeriodPlaceholder')}
                            options={[
                                { value: 'Daily', label: tp('scheduleTypeDaily') },
                                { value: 'Weekly', label: tp('scheduleTypeWeekly') },
                                { value: 'Monthly', label: tp('scheduleTypeMonthly') },
                                { value: 'Yearly', label: tp('scheduleTypeYearly') },
                            ]}
                        />
                    </Form.Item>
                    <Form.Item name="recipientEmails" label={tp('scheduleRecipientsPlaceholder')}>
                        <Input placeholder={tp('scheduleRecipientsPlaceholder')} />
                    </Form.Item>
                    <Form.Item>
                        <Button type="primary" htmlType="submit" loading={scheduleSaving} disabled={!selectedRegisterId}>
                            {tp('scheduleSubmitButton')}
                        </Button>
                    </Form.Item>
                </Form>
                {schedulesLoading ? (
                    <Spin />
                ) : schedules?.length ? (
                    <Table rowKey="id" size="small" pagination={false} dataSource={schedules} columns={scheduleColumns} />
                ) : (
                    <Empty description={tp('scheduleEmpty')} />
                )}
            </Space>
        </Card>
    );
}

export function DepExportTestPage() {
    const { t, formatLocale } = useI18n();
    const { message } = useAntdApp();
    const queryClient = useQueryClient();
    const tp = useCallback((path: string) => t(`rksvHub.depExportPage.${path}`), [t]);

    const [selectedRegisterId, setSelectedRegisterId] = useState<string | undefined>();
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([
        dayjs().subtract(7, 'day').startOf('day'),
        dayjs().endOf('day'),
    ]);
    const [includeSpecialReceipts, setIncludeSpecialReceipts] = useState(true);
    const [includeDailyClosings, setIncludeDailyClosings] = useState(true);
    const [exportResult, setExportResult] = useState<RksvDepExportRoot | null>(null);
    const [cryptoMaterial, setCryptoMaterial] = useState<CryptoMaterial | null>(null);
    const [activeTab, setActiveTab] = useState('export');
    const [historyPage, setHistoryPage] = useState(1);
    const [viewingHistory, setViewingHistory] = useState<DepExportHistoryItem | null>(null);
    const [viewingHistoryId, setViewingHistoryId] = useState<string | null>(null);

    const { data: cashRegisters, isLoading: registersLoading } = useQuery({
        queryKey: rksvAdminQueryKeys.cashRegisters,
        queryFn: getAdminCashRegisters,
        staleTime: 60_000,
    });

    const { mutate: fetchDepExport, isPending } = useDepExport();
    const { mutate: fetchCryptoMaterial, isPending: cryptoLoading } = useCryptoMaterial();
    const { data: historyData, isLoading: historyLoading } = useDepExportHistory(selectedRegisterId, historyPage);
    const { data: schedules, isLoading: schedulesLoading, refetch: refetchSchedules } = useDepExportSchedules();

    const registerOptions = useMemo(
        () =>
            (cashRegisters ?? [])
                .filter((register) => typeof register.id === 'string' && register.id.length > 0)
                .map((register) => ({
                    value: register.id as string,
                    label: formatRegisterLabel(register.registerNumber, register.location, register.id),
                })),
        [cashRegisters],
    );

    const stats = useMemo(() => computeDepExportStats(exportResult), [exportResult]);
    const previewJson = exportResult ? JSON.stringify(exportResult, null, 2) : '';

    const buildRequestParams = (): DepExportRequestParams | null => {
        const fromUtc = dateRange?.[0]?.toISOString();
        const toUtc = dateRange?.[1]?.toISOString();
        if (!selectedRegisterId || !fromUtc || !toUtc) return null;
        return {
            cashRegisterId: selectedRegisterId,
            fromUtc,
            toUtc,
            includeSpecialReceipts,
            includeDailyClosings,
        };
    };

    const handleExport = () => {
        const params = buildRequestParams();
        if (!params) {
            if (!selectedRegisterId) {
                message.warning(tp('selectRegisterWarning'));
            } else {
                message.warning(tp('selectDateRangeWarning'));
            }
            return;
        }

        fetchDepExport(params, {
            onSuccess: (data) => {
                setExportResult(data);
                void queryClient.invalidateQueries({ queryKey: depExportHistoryQueryKey(selectedRegisterId) });
                message.success(tp('exportSuccess'));
            },
            onError: (error) => {
                const msg = extractApiErrorMessage(error, tp('exportFailed'));
                message.error(`${tp('exportFailed')}: ${msg}`);
            },
        });
    };

    const handleDownload = () => {
        if (!exportResult || !selectedRegisterId) return;
        const fromLabel = dateRange?.[0]?.format('YYYY-MM-DD') ?? 'from';
        const toLabel = dateRange?.[1]?.format('YYYY-MM-DD') ?? 'to';
        downloadJsonBlob(exportResult, `dep-export-${selectedRegisterId}-${fromLabel}-${toLabel}.json`);
    };

    const downloadCryptoMaterial = () => {
        if (!cryptoMaterial || !selectedRegisterId) return;
        downloadJsonBlob(cryptoMaterial, `crypto-material-${selectedRegisterId}.json`);
    };

    const handleGenerateCryptoMaterial = () => {
        if (!selectedRegisterId) {
            message.warning(tp('selectRegisterWarning'));
            return;
        }

        fetchCryptoMaterial(selectedRegisterId, {
            onSuccess: (data) => {
                setCryptoMaterial(data);
                message.success(tp('cryptoMaterialSuccess'));
            },
            onError: (error) => {
                const msg = extractApiErrorMessage(error, tp('cryptoMaterialFailed'));
                message.error(`${tp('cryptoMaterialFailed')}: ${msg}`);
            },
        });
    };

    const handleCopy = async () => {
        if (!exportResult) return;
        try {
            await navigator.clipboard.writeText(JSON.stringify(exportResult, null, 2));
            message.success(tp('copySuccess'));
        } catch {
            message.error(tp('copyFailed'));
        }
    };

    const handleCopyPrueftoolCommand = async () => {
        try {
            await navigator.clipboard.writeText(PRUEFTOOL_COMMAND);
            message.success(tp('copyCommandSuccess'));
        } catch {
            message.error(tp('copyFailed'));
        }
    };

    const applyHistoryEntry = (entry: DepExportHistoryItem) => {
        setSelectedRegisterId(entry.cashRegisterId);
        setDateRange([dayjs(entry.fromUtc), dayjs(entry.toUtc)]);
        setIncludeSpecialReceipts(entry.includeSpecialReceipts);
        setIncludeDailyClosings(entry.includeDailyClosings);
        setActiveTab('export');
        message.info(tp('historyParamsLoaded'));
    };

    const downloadExport = async (row: DepExportHistoryItem) => {
        if (!row.hasStoredFile) {
            message.info(tp('historyDownloadUnavailable'));
            return;
        }
        try {
            await downloadDepExportHistoryFile(row.id, row.fileName);
        } catch {
            message.error(tp('exportFailed'));
        }
    };

    const viewExport = async (historyId: string) => {
        setViewingHistoryId(historyId);
        try {
            const detail = await fetchDepExportHistoryDetail(historyId);
            setViewingHistory(detail);
        } catch {
            message.error(tp('historyViewLoadFailed'));
            setViewingHistory(null);
        } finally {
            setViewingHistoryId(null);
        }
    };

    const renderHistoryStatusTag = (status: DepExportHistoryItem['status']) => {
        const color =
            status === 'Completed' ? 'green' : status === 'Failed' ? 'red' : status === 'Processing' ? 'blue' : 'default';
        return <Tag color={color}>{status}</Tag>;
    };

    const historyColumns: ColumnsType<DepExportHistoryItem> = [
        {
            title: tp('historyColumnDate'),
            dataIndex: 'exportedAt',
            key: 'exportedAt',
            render: (value: string) => formatDateTime(value, formatLocale),
        },
        {
            title: tp('historyColumnPeriod'),
            key: 'period',
            render: (_, row) =>
                `${formatDate(row.fromUtc, formatLocale)} — ${formatDate(row.toUtc, formatLocale)}`,
        },
        {
            title: tp('historyColumnSignatures'),
            dataIndex: 'signatureCount',
            key: 'signatureCount',
        },
        {
            title: tp('historyColumnSize'),
            dataIndex: 'fileSizeBytes',
            key: 'fileSizeBytes',
            render: (value: number) => formatBytes(value, formatLocale),
        },
        {
            title: tp('historyColumnStatus'),
            dataIndex: 'status',
            key: 'status',
            render: (status: DepExportHistoryItem['status']) => renderHistoryStatusTag(status),
        },
        {
            title: tp('historyColumnActions'),
            key: 'actions',
            render: (_, row) => (
                <Space>
                    <Button
                        icon={<DownloadOutlined />}
                        aria-label={tp('historyDownloadStored')}
                        disabled={!row.hasStoredFile}
                        onClick={() => void downloadExport(row)}
                    />
                    <Button
                        icon={<EyeOutlined />}
                        aria-label={tp('historyViewTitle')}
                        loading={viewingHistoryId === row.id}
                        onClick={() => void viewExport(row.id)}
                    />
                </Space>
            ),
        },
    ];

    const exportTab = (
        <Card size="small">
            <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                <div>
                    <Typography.Text strong>{tp('cashRegisterLabel')}</Typography.Text>
                    <Select
                        placeholder={tp('cashRegisterPlaceholder')}
                        style={{ width: '100%', marginTop: 8 }}
                        loading={registersLoading}
                        allowClear
                        value={selectedRegisterId}
                        onChange={setSelectedRegisterId}
                        options={registerOptions}
                    />
                </div>

                <div>
                    <Typography.Text strong>{tp('dateRangeLabel')}</Typography.Text>
                    <RangePicker
                        showTime
                        format="DD.MM.YYYY HH:mm:ss"
                        style={{ width: '100%', marginTop: 8 }}
                        value={[dateRange[0], dateRange[1]]}
                        onChange={(dates) => setDateRange((dates as [Dayjs | null, Dayjs | null] | null) ?? [null, null])}
                        placeholder={[tp('dateRangeStart'), tp('dateRangeEnd')]}
                    />
                </div>

                <div>
                    <Typography.Text strong>{tp('optionsLabel')}</Typography.Text>
                    <Space wrap style={{ marginTop: 8 }}>
                        <Checkbox
                            checked={includeSpecialReceipts}
                            onChange={(event) => setIncludeSpecialReceipts(event.target.checked)}
                        >
                            {tp('includeSpecialReceipts')}
                        </Checkbox>
                        <Checkbox
                            checked={includeDailyClosings}
                            onChange={(event) => setIncludeDailyClosings(event.target.checked)}
                        >
                            {tp('includeDailyClosings')}
                        </Checkbox>
                    </Space>
                </div>

                <Space wrap>
                    <Button
                        type="primary"
                        icon={<PlayCircleOutlined />}
                        onClick={handleExport}
                        loading={isPending}
                        disabled={!selectedRegisterId || !dateRange[0] || !dateRange[1]}
                    >
                        {tp('exportButton')}
                    </Button>
                    {exportResult ? (
                        <>
                            <Button icon={<DownloadOutlined />} onClick={handleDownload}>
                                {tp('downloadButton')}
                            </Button>
                            <Button icon={<CopyOutlined />} onClick={() => void handleCopy()}>
                                {tp('copyButton')}
                            </Button>
                        </>
                    ) : null}
                    <Button
                        icon={<KeyOutlined />}
                        onClick={handleGenerateCryptoMaterial}
                        loading={cryptoLoading}
                        disabled={!selectedRegisterId}
                    >
                        {tp('cryptoMaterialGenerateButton')}
                    </Button>
                    {cryptoMaterial ? (
                        <Button icon={<DownloadOutlined />} onClick={downloadCryptoMaterial}>
                            {tp('cryptoMaterialDownloadButton')}
                        </Button>
                    ) : null}
                </Space>

                {stats ? (
                    <Alert
                        type="info"
                        showIcon
                        title={tp('statsTitle')}
                        description={
                            <Descriptions size="small" column={2} style={{ marginTop: 8 }}>
                                <Descriptions.Item label={tp('statsGroups')}>{stats.groupCount}</Descriptions.Item>
                                <Descriptions.Item label={tp('statsSignatures')}>{stats.totalSignatures}</Descriptions.Item>
                            </Descriptions>
                        }
                    />
                ) : null}

                {exportResult ? (
                    <Collapse
                        items={[
                            {
                                key: 'json',
                                label: tp('jsonPreviewTitle'),
                                children: (
                                    <pre
                                        style={{
                                            fontSize: 11,
                                            maxHeight: 520,
                                            overflow: 'auto',
                                            background: '#1e1e1e',
                                            color: '#d4d4d4',
                                            padding: 12,
                                            borderRadius: 6,
                                            margin: 0,
                                        }}
                                    >
                                        {previewJson}
                                    </pre>
                                ),
                            },
                        ]}
                    />
                ) : (
                    <Alert type="info" showIcon title={tp('emptyHintTitle')} description={tp('emptyHintBody')} />
                )}
            </Space>
        </Card>
    );

    const verifyTab = (
        <Card size="small">
            <Alert type="info" showIcon title={tp('verifyTitle')} description={tp('verifyDescription')} />
            <Space orientation="vertical" size="middle" style={{ width: '100%', marginTop: 16 }}>
                <Space wrap>
                    <Button icon={<DownloadOutlined />} onClick={handleDownload} disabled={!exportResult}>
                        {tp('verifyDownloadButton')}
                    </Button>
                    <Button
                        icon={<KeyOutlined />}
                        onClick={handleGenerateCryptoMaterial}
                        loading={cryptoLoading}
                        disabled={!selectedRegisterId}
                    >
                        {tp('cryptoMaterialGenerateButton')}
                    </Button>
                    {cryptoMaterial ? (
                        <Button icon={<DownloadOutlined />} onClick={downloadCryptoMaterial}>
                            {tp('cryptoMaterialDownloadButton')}
                        </Button>
                    ) : null}
                </Space>
                <div style={{ padding: 12, background: '#f5f5f5', borderRadius: 6 }}>
                    <Typography.Text code style={{ whiteSpace: 'pre-wrap', fontSize: 12 }}>
                        {PRUEFTOOL_COMMAND}
                    </Typography.Text>
                </div>
                <Space wrap>
                    <Button icon={<CopyOutlined />} onClick={() => void handleCopyPrueftoolCommand()}>
                        {tp('verifyCopyCommand')}
                    </Button>
                </Space>
                <Alert type="warning" showIcon title={tp('verifyPrerequisiteTitle')} description={tp('verifyPrerequisiteBody')} />
            </Space>
        </Card>
    );

    const certificateTab = (
        <Card size="small">
            <Alert type="info" showIcon title={tp('certificateTitle')} description={tp('certificateDescription')} />
            <Space orientation="vertical" size="middle" style={{ width: '100%', marginTop: 16 }}>
                {stats?.certificateThumbprints.length ? (
                    <Descriptions bordered size="small" column={1} title={tp('certificateGroupsTitle')}>
                        {stats.certificateThumbprints.map((thumbprint, index) => (
                            <Descriptions.Item key={`${thumbprint}-${index}`} label={`${tp('certificateGroupLabel')} ${index + 1}`}>
                                <Typography.Text code>{thumbprint}…</Typography.Text>
                            </Descriptions.Item>
                        ))}
                    </Descriptions>
                ) : (
                    <Empty description={tp('certificateEmpty')} />
                )}
                <Button href="/rksv/cmc-certificate">{tp('certificateOpenCmc')}</Button>
            </Space>
        </Card>
    );

    const historyTab = (
        <Card size="small">
            {historyLoading ? (
                <div style={{ textAlign: 'center', padding: 40 }}>
                    <Spin />
                </div>
            ) : historyData?.items.length ? (
                <Table
                    rowKey="id"
                    size="small"
                    pagination={{
                        current: historyPage,
                        pageSize: 20,
                        total: historyData.totalCount,
                        onChange: setHistoryPage,
                    }}
                    columns={historyColumns}
                    dataSource={historyData.items}
                />
            ) : (
                <Empty
                    image={<HistoryOutlined style={{ fontSize: 48, color: '#bfbfbf' }} />}
                    description={tp('historyEmpty')}
                />
            )}
            <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0, fontSize: 12 }}>
                {tp('historyFootnoteServer')}
            </Typography.Paragraph>
        </Card>
    );

    const tabItems = [
        { key: 'export', label: tp('tabExport'), children: exportTab },
        { key: 'verify', label: tp('tabVerify'), children: verifyTab },
        { key: 'certificate', label: tp('tabCertificate'), children: certificateTab },
        { key: 'history', label: tp('tabHistory'), children: historyTab },
        {
            key: 'schedule',
            label: tp('tabSchedules'),
            children: (
                <DepExportSchedulesTab
                    tp={tp}
                    formatLocale={formatLocale}
                    selectedRegisterId={selectedRegisterId}
                    schedules={schedules}
                    schedulesLoading={schedulesLoading}
                    onRefetchSchedules={refetchSchedules}
                />
            ),
        },
    ];

    return (
        <>
            <AdminPageHeader
                title={tp('title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('adminShell.group.rksv'), href: '/rksv' },
                    { title: tp('breadcrumb') },
                ]}
            >
                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                    {tp('subtitle')}
                </Typography.Paragraph>
            </AdminPageHeader>

            <Alert
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
                title={tp('scopeTitle')}
                description={
                    <span>
                        {tp('scopeBody')}{' '}
                        <Link href="/rksv/fiscal-export-diagnostics">{tp('scopeFiscalExportLink')}</Link>.
                    </span>
                }
            />

            {registersLoading ? (
                <div style={{ textAlign: 'center', padding: 80 }}>
                    <Spin size="large" />
                </div>
            ) : (
                <>
                    <Tabs activeKey={activeTab} onChange={setActiveTab} items={tabItems} />
                    <Modal
                        title={tp('historyViewTitle')}
                        open={viewingHistory != null}
                        onCancel={() => setViewingHistory(null)}
                        footer={
                            <Space>
                                {viewingHistory?.hasStoredFile ? (
                                    <Button
                                        icon={<DownloadOutlined />}
                                        onClick={() => viewingHistory && void downloadExport(viewingHistory)}
                                    >
                                        {tp('historyDownloadStored')}
                                    </Button>
                                ) : null}
                                <Button
                                    onClick={() => {
                                        if (viewingHistory) applyHistoryEntry(viewingHistory);
                                        setViewingHistory(null);
                                    }}
                                >
                                    {tp('historyLoadParams')}
                                </Button>
                                <Button type="primary" onClick={() => setViewingHistory(null)}>
                                    {t('common.close')}
                                </Button>
                            </Space>
                        }
                        width={640}
                    >
                        {viewingHistory ? (
                            <Descriptions bordered size="small" column={1}>
                                <Descriptions.Item label={tp('historyColumnDate')}>
                                    {formatDateTime(viewingHistory.exportedAt, formatLocale)}
                                </Descriptions.Item>
                                <Descriptions.Item label={tp('historyColumnPeriod')}>
                                    {formatDate(viewingHistory.fromUtc, formatLocale)} —{' '}
                                    {formatDate(viewingHistory.toUtc, formatLocale)}
                                </Descriptions.Item>
                                <Descriptions.Item label={tp('historyColumnRegister')}>
                                    {viewingHistory.registerNumber ?? viewingHistory.cashRegisterId}
                                </Descriptions.Item>
                                <Descriptions.Item label={tp('historyColumnSignatures')}>
                                    {viewingHistory.signatureCount}
                                </Descriptions.Item>
                                <Descriptions.Item label={tp('statsGroups')}>{viewingHistory.groupCount}</Descriptions.Item>
                                <Descriptions.Item label={tp('historyColumnSize')}>
                                    {formatBytes(viewingHistory.fileSizeBytes, formatLocale)}
                                </Descriptions.Item>
                                <Descriptions.Item label={tp('historyColumnStatus')}>
                                    {renderHistoryStatusTag(viewingHistory.status)}
                                </Descriptions.Item>
                                {viewingHistory.errorMessage ? (
                                    <Descriptions.Item label={tp('historyColumnError')}>
                                        {viewingHistory.errorMessage}
                                    </Descriptions.Item>
                                ) : null}
                            </Descriptions>
                        ) : null}
                    </Modal>
                </>
            )}
        </>
    );
}
