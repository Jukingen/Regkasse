'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useCallback, useMemo, useState } from 'react';
import Link from 'next/link';
import { Modal, Alert, Button, Card, DatePicker, Empty, Form, Input, Progress, Select, Space, Statistic, Table, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
    CheckCircleOutlined,
    CloseCircleOutlined,
    EditOutlined,
    ExportOutlined,
    EyeOutlined,
    MailOutlined,
    ReloadOutlined,
    WarningOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n, formatDate } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';
import { useDebounce } from '@/hooks/useDebounce';
import { listAdminTenants, type AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import {
    extendAdminTenantLicense,
    sendAdminTenantLicenseReminder,
} from '@/features/super-admin/api/adminTenantLicense';
import {
    getLicenseStatusLabel,
    resolveTenantRowLicenseStatus,
    type LicenseStatusKind,
    type ResolvedLicenseStatus,
} from '@/features/license/utils/licenseStatus';
import { adminTableScrollXy, shouldUseAdminTableVirtual } from '@/components/ui/adminTableVirtual';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

const TENANT_LICENSE_QUERY_KEY = ['admin', 'tenants', 'licenses'] as const;

type TenantStatusFilterValue = 'all' | 'active' | 'suspended' | 'deleted';
type LicenseStatusFilterValue = 'all' | LicenseStatusKind;

type TenantLicenseTableRow = AdminTenantListItem & {
    resolvedLicenseStatus: ResolvedLicenseStatus;
    searchBlob: string;
};

type ExtendLicenseModalProps = {
    visible: boolean;
    tenant: TenantLicenseTableRow | null;
    onClose: () => void;
    onSuccess: () => void;
    t: (key: string, params?: Record<string, string | number>) => string;
};

type ExtendLicenseFormValues = {
    validUntil: Dayjs;
    licenseKey?: string;
};

const LICENSE_PROGRESS_TOTAL_DAYS = 365;

function getLicenseStatusTagConfig(
    status: ResolvedLicenseStatus,
    t: (key: string, params?: Record<string, string | number>) => string,
): { color: string; icon: React.ReactNode; text: string } {
    switch (status.kind) {
        case 'active':
            return {
                color: 'green',
                icon: <CheckCircleOutlined />,
                text: t('license.phase.labels.active'),
            };
        case 'grace_write':
            return {
                color: 'orange',
                icon: <WarningOutlined />,
                text: t('license.phase.labels.graceWrite'),
            };
        case 'grace_readonly':
            return {
                color: 'warning',
                icon: <WarningOutlined />,
                text: t('license.phase.labels.graceReadonly'),
            };
        case 'lockdown':
            return {
                color: 'red',
                icon: <CloseCircleOutlined />,
                text: t('license.phase.labels.lockdown'),
            };
        case 'expired':
            return {
                color: 'red',
                icon: <CloseCircleOutlined />,
                text: t('license.phase.labels.expired'),
            };
        case 'no_license':
        default:
            return {
                color: 'default',
                icon: <CloseCircleOutlined />,
                text: t('license.phase.labels.noLicense'),
            };
    }
}

function getDaysColumnTone(daysRemaining: number): { color: string; progressColor: string } {
    if (daysRemaining <= 7) {
        return { color: '#ff4d4f', progressColor: '#ff4d4f' };
    }
    if (daysRemaining <= 30) {
        return { color: '#faad14', progressColor: '#faad14' };
    }
    return { color: '#52c41a', progressColor: '#52c41a' };
}

function toCsvCell(value: string): string {
    return `"${value.replace(/"/g, '""')}"`;
}

function downloadCsv(filename: string, content: string): void {
    if (typeof globalThis.window === 'undefined') {
        return;
    }

    const blob = new globalThis.Blob([`\uFEFF${content}`], {
        type: 'text/csv;charset=utf-8;',
    });
    const url = globalThis.URL.createObjectURL(blob);
    const link = globalThis.document.createElement('a');
    link.href = url;
    link.download = filename;
    globalThis.document.body.appendChild(link);
    link.click();
    link.remove();
    globalThis.URL.revokeObjectURL(url);
}

function ExtendLicenseModal({
    visible,
    tenant,
    onClose,
    onSuccess,
    t,
}: ExtendLicenseModalProps) {
    const [form] = Form.useForm<ExtendLicenseFormValues>();

    const extendMutation = useMutation({
        mutationFn: async (values: ExtendLicenseFormValues) => {
            if (!tenant) {
                return null;
            }

            return extendAdminTenantLicense(tenant.id, {
                validUntilUtc: values.validUntil.toISOString(),
                licenseKey: values.licenseKey?.trim() || null,
            });
        },
        onSuccess: () => {
            if (!tenant) {
                return;
            }

            message.success(
                t('tenants.licensesPage.extendModal.success', {
                    name: tenant.name,
                }),
            );
            onSuccess();
            onClose();
        },
    });

    React.useEffect(() => {
        if (!visible || !tenant) {
            return;
        }

        form.setFieldsValue({
            validUntil: tenant.licenseValidUntilUtc
                ? dayjs(tenant.licenseValidUntilUtc)
                : dayjs().add(30, 'day'),
            licenseKey: tenant.licenseKey ?? undefined,
        });
    }, [form, tenant, visible]);

    return (
        <Modal
            title={
                tenant
                    ? t('tenants.licensesPage.extendModal.title', { name: tenant.name })
                    : t('tenants.actions.manageLicense')
            }
            open={visible}
            onCancel={onClose}
            onOk={() => form.submit()}
            confirmLoading={extendMutation.isPending}
            width={500}
            destroyOnHidden
        >
            <Form
                form={form}
                layout="vertical"
                onFinish={(values) => {
                    extendMutation.mutate(values);
                }}
            >
                <Form.Item
                    name="validUntil"
                    label={t('tenants.detail.license.validUntil')}
                    rules={[
                        {
                            required: true,
                            message: t('tenants.licensesPage.extendModal.dateRequired'),
                        },
                    ]}
                >
                    <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" />
                </Form.Item>

                <Form.Item
                    name="licenseKey"
                    label={t('tenants.licensesPage.extendModal.licenseKey')}
                    tooltip={t('tenants.licensesPage.extendModal.licenseKeyTooltip')}
                >
                    <Input placeholder="REGK-XXXX-XXXX-XXXX" />
                </Form.Item>

                <Alert
                    title={t('tenants.licensesPage.extendModal.hintTitle')}
                    description={t('tenants.licensesPage.extendModal.hintDescription')}
                    type="info"
                    showIcon
                />
            </Form>
        </Modal>
    );
}

export default function AdminTenantLicensesPage() {
  const { message } = useAntdApp();

    const { t, formatLocale } = useI18n();
    const { user } = useAuth();
    const queryClient = useQueryClient();
    const [searchInput, setSearchInput] = useState('');
    const includeDeleted = false;
    const [tenantStatusFilter, setTenantStatusFilter] = useState<TenantStatusFilterValue>('all');
    const [licenseStatusFilter, setLicenseStatusFilter] = useState<LicenseStatusFilterValue>('all');
    const [expiryRange, setExpiryRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
    const [extendTenant, setExtendTenant] = useState<TenantLicenseTableRow | null>(null);
    const [reminderTenantId, setReminderTenantId] = useState<string | null>(null);
    const debouncedSearch = useDebounce(searchInput, 300);

    const canAccess =
        isSuperAdmin(user?.role) || hasPermission(user, PERMISSIONS.LICENSE_VIEW);

    const tenantsQuery = useQuery({
        queryKey: TENANT_LICENSE_QUERY_KEY,
        queryFn: () => listAdminTenants(true),
        enabled: canAccess,
    });

    const invalidateTenants = useCallback(() => {
        void queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] });
    }, [queryClient]);

    const rows = useMemo<TenantLicenseTableRow[]>(() => {
        return (tenantsQuery.data ?? []).map((tenant) => {
            const resolvedLicenseStatus = resolveTenantRowLicenseStatus({
                licenseKey: tenant.licenseKey,
                licenseValidUntilUtc: tenant.licenseValidUntilUtc,
                licenseDaysRemaining: tenant.licenseDaysRemaining,
            });

            return {
                ...tenant,
                resolvedLicenseStatus,
                searchBlob: [
                    tenant.name,
                    tenant.slug,
                    tenant.ownerAdminEmail ?? '',
                    tenant.licenseKey ?? '',
                ]
                    .join(' ')
                    .toLowerCase(),
            };
        });
    }, [tenantsQuery.data]);

    const filteredRows = useMemo(() => {
        const normalizedSearch = debouncedSearch.trim().toLowerCase();

        return rows.filter((row) => {
            if (!includeDeleted && row.status === 'deleted') {
                return false;
            }

            if (tenantStatusFilter !== 'all' && row.status !== tenantStatusFilter) {
                return false;
            }

            if (
                licenseStatusFilter !== 'all' &&
                row.resolvedLicenseStatus.kind !== licenseStatusFilter
            ) {
                return false;
            }

            if (normalizedSearch && !row.searchBlob.includes(normalizedSearch)) {
                return false;
            }

            if (expiryRange && (expiryRange[0] || expiryRange[1])) {
                if (!row.licenseValidUntilUtc) {
                    return false;
                }

                const validUntil = dayjs(row.licenseValidUntilUtc);
                if (!validUntil.isValid()) {
                    return false;
                }

                if (expiryRange[0] && validUntil.isBefore(expiryRange[0].startOf('day'))) {
                    return false;
                }

                if (expiryRange[1] && validUntil.isAfter(expiryRange[1].endOf('day'))) {
                    return false;
                }
            }

            return true;
        });
    }, [debouncedSearch, expiryRange, includeDeleted, licenseStatusFilter, rows, tenantStatusFilter]);

    const summary = useMemo(
        () => ({
            total: rows.length,
            active: rows.filter((row) => row.resolvedLicenseStatus.kind === 'active').length,
            graceWrite: rows.filter((row) => row.resolvedLicenseStatus.kind === 'grace_write').length,
            graceReadOnly: rows.filter((row) => row.resolvedLicenseStatus.kind === 'grace_readonly').length,
            lockdown: rows.filter((row) => row.resolvedLicenseStatus.kind === 'lockdown').length,
            expired: rows.filter((row) => row.resolvedLicenseStatus.kind === 'expired').length,
            noLicense: rows.filter((row) => row.resolvedLicenseStatus.kind === 'no_license').length,
        }),
        [rows],
    );

    const exportRows = useCallback(() => {
        if (filteredRows.length === 0) {
            message.info(t('tenants.licensesPage.noRowsToExport'));
            return;
        }

        const header = [
            t('tenants.columns.name'),
            t('tenants.columns.slug'),
            t('tenants.columns.license'),
            t('tenants.detail.license.validUntil'),
            t('tenants.detail.license.remaining'),
            t('tenants.columns.adminUser'),
            t('tenants.columns.created'),
        ];

        const lines = filteredRows.map((row) => {
            return [
                row.name,
                row.slug,
                getLicenseStatusLabel(row.resolvedLicenseStatus.kind, t),
                row.licenseValidUntilUtc
                    ? formatDate(row.licenseValidUntilUtc, formatLocale)
                    : '',
                row.resolvedLicenseStatus.daysRemaining ?? '',
                row.ownerAdminEmail ?? '',
                formatDate(row.createdAt, formatLocale),
            ]
                .map((value) => toCsvCell(String(value)))
                .join(';');
        });

        downloadCsv(
            `licenses_${dayjs().format('YYYY-MM-DD')}.csv`,
            [header.map((value) => toCsvCell(String(value))).join(';'), ...lines].join('\n'),
        );

        message.success(t('tenants.licensesPage.exported', { count: filteredRows.length }));
    }, [filteredRows, formatLocale, t]);

    const handleDateRangeChange = useCallback((value: [Dayjs | null, Dayjs | null] | null) => {
        setExpiryRange(value ? [value[0] ?? null, value[1] ?? null] : null);
    }, []);

    const reminderMutation = useMutation({
        mutationFn: (row: TenantLicenseTableRow) => sendAdminTenantLicenseReminder(row.id),
        onMutate: (row) => setReminderTenantId(row.id),
        onSettled: () => setReminderTenantId(null),
        onSuccess: (result, row) => {
            message.success(
                t('tenants.licensesPage.reminderSent', {
                    recipient: result.recipientEmail || row.ownerAdminEmail || row.name,
                }),
            );
        },
        onError: () => {
            message.error(t('tenants.licensesPage.reminderFailed'));
        },
    });

    const sendReminderEmail = useCallback(
        (row: TenantLicenseTableRow) => {
            reminderMutation.mutate(row);
        },
        [reminderMutation],
    );

    const columns = useMemo<ColumnsType<TenantLicenseTableRow>>(
        () => [
            {
                title: t('tenants.columns.name'),
                dataIndex: 'name',
                key: 'name',
                width: 200,
                fixed: 'left',
                sorter: (a, b) => a.name.localeCompare(b.name),
                render: (name: string, row) => (
                    <div>
                        <div style={{ fontWeight: 500 }}>
                            <Link href={`/admin/tenants/${row.id}`}>{name}</Link>
                        </div>
                        <div style={{ fontSize: 12, color: '#8c8c8c' }}>{row.slug}</div>
                    </div>
                ),
            },
            {
                title: t('tenants.columns.license'),
                dataIndex: 'resolvedLicenseStatus',
                key: 'licenseStatus',
                width: 180,
                filters: [
                    { text: t('license.phase.labels.active'), value: 'active' },
                    { text: t('license.phase.labels.graceWrite'), value: 'grace_write' },
                    { text: t('license.phase.labels.graceReadonly'), value: 'grace_readonly' },
                    { text: t('license.phase.labels.lockdown'), value: 'lockdown' },
                    { text: t('license.phase.labels.expired'), value: 'expired' },
                    { text: t('license.phase.labels.noLicense'), value: 'no_license' },
                ],
                onFilter: (value, row) => row.resolvedLicenseStatus.kind === value,
                render: (status: ResolvedLicenseStatus) => {
                    const config = getLicenseStatusTagConfig(status, t);

                    return (
                        <Tag color={config.color} icon={config.icon}>
                            {config.text}
                        </Tag>
                    );
                },
            },
            {
                title: t('tenants.detail.license.remaining'),
                dataIndex: 'licenseDaysRemaining',
                key: 'daysRemaining',
                width: 200,
                sorter: (a, b) => a.resolvedLicenseStatus.daysRemaining - b.resolvedLicenseStatus.daysRemaining,
                render: (_: number | null | undefined, row) => {
                    const status = row.resolvedLicenseStatus;
                    if (status.kind === 'no_license') {
                        return '—';
                    }

                    if (status.daysExpired > 0 || status.kind === 'expired') {
                        return (
                            <span style={{ color: '#ff4d4f' }}>
                                {t('tenants.licensesPage.daysOverdue', { days: status.daysExpired })}
                            </span>
                        );
                    }

                    if (status.daysRemaining <= 0) {
                        return (
                            <span style={{ color: '#ff4d4f' }}>
                                {t('license.phase.labels.expired')}
                            </span>
                        );
                    }

                    const { color, progressColor } = getDaysColumnTone(status.daysRemaining);
                    const percent = Math.min(
                        (status.daysRemaining / LICENSE_PROGRESS_TOTAL_DAYS) * 100,
                        100,
                    );

                    return (
                        <div>
                            <div style={{ marginBottom: 4 }}>
                                <span style={{ color, fontWeight: 500 }}>{status.daysRemaining}</span>
                                <span style={{ color: '#8c8c8c' }}>
                                    {' '}
                                    / {LICENSE_PROGRESS_TOTAL_DAYS} {t('tenants.licensesPage.daysUnit')}
                                </span>
                            </div>
                            <Progress
                                percent={percent}
                                size="small"
                                strokeColor={progressColor}
                                showInfo={false}
                            />
                        </div>
                    );
                },
            },
            {
                title: t('tenants.detail.license.validUntil'),
                dataIndex: 'licenseValidUntilUtc',
                key: 'licenseValidUntilUtc',
                width: 120,
                sorter: (a, b) => {
                    const left = a.licenseValidUntilUtc ? dayjs(a.licenseValidUntilUtc).unix() : 0;
                    const right = b.licenseValidUntilUtc ? dayjs(b.licenseValidUntilUtc).unix() : 0;
                    return left - right;
                },
                render: (value: string | null | undefined) =>
                    value ? formatDate(value, formatLocale) : '—',
            },
            {
                title: t('tenants.columns.adminUser'),
                dataIndex: 'ownerAdminEmail',
                key: 'ownerAdminEmail',
                width: 180,
                render: (value: string | null | undefined) =>
                    value || <span style={{ color: '#8c8c8c' }}>—</span>,
            },
            {
                title: t('tenants.columns.created'),
                dataIndex: 'createdAt',
                key: 'createdAt',
                width: 110,
                sorter: (a, b) => dayjs(a.createdAt).unix() - dayjs(b.createdAt).unix(),
                render: (value: string) => formatDate(value, formatLocale),
            },
            {
                title: t('tenants.columns.actions'),
                key: 'actions',
                width: 200,
                fixed: 'right',
                render: (_, row) => (
                    <Space>
                        <Tooltip title={t('tenants.actions.view')}>
                            <Link href={`/admin/tenants/${row.id}`}>
                                <Button icon={<EyeOutlined />} size="small" />
                            </Link>
                        </Tooltip>
                        <Tooltip title={t('tenants.actions.manageLicense')}>
                            <Button
                                icon={<EditOutlined />}
                                size="small"
                                onClick={() => setExtendTenant(row)}
                            />
                        </Tooltip>
                        <Tooltip title={t('tenants.licensesPage.sendReminder')}>
                            <Button
                                icon={<MailOutlined />}
                                size="small"
                                loading={reminderMutation.isPending && reminderTenantId === row.id}
                                onClick={() => sendReminderEmail(row)}
                            />
                        </Tooltip>
                    </Space>
                ),
            },
        ],
        [
            formatLocale,
            sendReminderEmail,
            t,
            reminderMutation.isPending,
            reminderTenantId,
        ],
    );

    if (!canAccess) {
        return (
            <AdminPageShell>
                <Alert
                    type="error"
                    title={t('tenants.accessDenied.title')}
                    description={t('tenants.accessDenied.body')}
                />
            </AdminPageShell>
        );
    }

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('nav.superAdminLicenses')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('nav.superAdminLicenses'), href: '/admin/licenses' },
                ]}
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t('tenants.licensesPage.subtitle')}
                </Typography.Paragraph>
            </AdminPageHeader>

            <div
                className="stats-cards"
                style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(7, 1fr)',
                    gap: 12,
                    marginBottom: 24,
                }}
            >
                <Card size="small">
                    <Statistic title={t('tenants.licensesPage.summary.total')} value={summary.total} />
                </Card>
                <Card size="small" style={{ borderLeft: '4px solid #52c41a' }}>
                    <Statistic
                        title={t('license.phase.labels.active')}
                        value={summary.active}
                        styles={{ content: {  color: '#52c41a'  } }}
                    />
                </Card>
                <Card size="small" style={{ borderLeft: '4px solid #faad14' }}>
                    <Statistic
                        title={t('license.phase.labels.graceWrite')}
                        value={summary.graceWrite}
                        styles={{ content: {  color: '#faad14'  } }}
                    />
                </Card>
                <Card size="small" style={{ borderLeft: '4px solid #ff7a45' }}>
                    <Statistic
                        title={t('license.phase.labels.graceReadonly')}
                        value={summary.graceReadOnly}
                        styles={{ content: {  color: '#ff7a45'  } }}
                    />
                </Card>
                <Card size="small" style={{ borderLeft: '4px solid #ff4d4f' }}>
                    <Statistic
                        title={t('license.phase.labels.lockdown')}
                        value={summary.lockdown}
                        styles={{ content: {  color: '#ff4d4f'  } }}
                    />
                </Card>
                <Card size="small" style={{ borderLeft: '4px solid #ff4d4f' }}>
                    <Statistic
                        title={t('license.phase.labels.expired')}
                        value={summary.expired}
                        styles={{ content: {  color: '#ff4d4f'  } }}
                    />
                </Card>
                <Card size="small">
                    <Statistic title={t('license.phase.labels.noLicense')} value={summary.noLicense} />
                </Card>
            </div>

            <div
                className="filters-section"
                style={{ marginBottom: 16, display: 'flex', gap: 12, flexWrap: 'wrap' }}
            >
                <Input.Search
                    allowClear
                    value={searchInput}
                    placeholder={t('tenants.licensesPage.searchPlaceholder')}
                    style={{ width: 250 }}
                    onSearch={setSearchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                />

                <Select<TenantStatusFilterValue>
                    placeholder={t('tenants.licensesPage.tenantStatusPlaceholder')}
                    style={{ width: 150 }}
                    value={tenantStatusFilter}
                    onChange={setTenantStatusFilter}
                    options={[
                        { label: t('tenants.licensesPage.allShort'), value: 'all' },
                        { label: t('tenants.status.active'), value: 'active' },
                        { label: t('tenants.status.suspended'), value: 'suspended' },
                    ]}
                />

                <Select<LicenseStatusFilterValue>
                    placeholder={t('tenants.licensesPage.licenseStatusPlaceholder')}
                    style={{ width: 180 }}
                    value={licenseStatusFilter}
                    onChange={setLicenseStatusFilter}
                    options={[
                        { label: t('tenants.licensesPage.allShort'), value: 'all' },
                        { label: t('license.phase.labels.active'), value: 'active' },
                        { label: t('license.phase.labels.graceWrite'), value: 'grace_write' },
                        { label: t('license.phase.labels.graceReadonly'), value: 'grace_readonly' },
                        { label: t('license.phase.labels.lockdown'), value: 'lockdown' },
                        { label: t('license.phase.labels.expired'), value: 'expired' },
                        { label: t('license.phase.labels.noLicense'), value: 'no_license' },
                    ]}
                />

                <DatePicker.RangePicker format={DAYJS_DATE_FORMAT}
                    value={expiryRange}
                    placeholder={[
                        t('tenants.licensesPage.validFromPlaceholder'),
                        t('tenants.licensesPage.validUntilPlaceholder'),
                    ]}
                    onChange={handleDateRangeChange}
                />

                <Button icon={<ReloadOutlined />} onClick={() => void tenantsQuery.refetch()}>
                    {t('common.buttons.refresh')}
                </Button>

                <Button icon={<ExportOutlined />} onClick={exportRows} disabled={filteredRows.length === 0}>
                    {t('tenants.licensesPage.export')}
                </Button>
            </div>

            {tenantsQuery.isError ? (
                <Alert
                    type="error"
                    showIcon
                    title={t('common.messages.unknownError')}
                    style={{ marginBottom: 16 }}
                />
            ) : null}

            <Card>
                <Table<TenantLicenseTableRow>
                    rowKey="id"
                    loading={tenantsQuery.isLoading}
                    dataSource={filteredRows}
                    columns={columns}
                    locale={{
                        emptyText: (
                            <Empty
                                image={Empty.PRESENTED_IMAGE_SIMPLE}
                                description={t('tenants.page.empty')}
                            />
                        ),
                    }}
                    virtual={shouldUseAdminTableVirtual(filteredRows.length)}
                    scroll={adminTableScrollXy(1310, filteredRows.length)}
                    pagination={{
                        pageSize: 20,
                        showSizeChanger: true,
                        pageSizeOptions: [10, 20, 50, 100],
                    }}
                />
            </Card>

            <ExtendLicenseModal
                visible={extendTenant !== null}
                tenant={extendTenant}
                onClose={() => setExtendTenant(null)}
                onSuccess={invalidateTenants}
                t={t}
            />
        </AdminPageShell>
    );
}
