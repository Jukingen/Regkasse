'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Card, Col, Empty, Input, Row, Select, Segmented, Space, Statistic, Switch, Typography } from 'antd';
import {
    AppstoreOutlined,
    BarsOutlined,
    CheckCircleOutlined,
    ExportOutlined,
    LockOutlined,
    PlusOutlined,
    ReloadOutlined,
    ShopOutlined,
    StopOutlined,
    ToolOutlined,
} from '@ant-design/icons';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import type { CashRegister } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { usePermissions } from '@/shared/auth/usePermissions';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { CashRegisterSelector } from '@/features/cash-registers/components/CashRegisterSelector';
import { CashRegisterGrid } from '@/features/cash-registers/components/CashRegisterGrid';
import { CashRegisterShiftRksvGuide } from '@/features/cash-registers/components/CashRegisterShiftRksvGuide';
import { CashRegisterTable } from '@/features/cash-registers/components/CashRegisterTable';
import { CreateCashRegisterModal } from '@/features/cash-registers/components/CreateCashRegisterModal';
import { DecommissionModal } from '@/features/cash-registers/components/DecommissionModal';
import { BulkDecommissionBar } from '@/features/cash-registers/components/BulkDecommissionBar';
import { BulkDecommissionModal } from '@/features/cash-registers/components/BulkDecommissionModal';
import { RegisterDetailModal } from '@/features/cash-registers/components/RegisterDetailModal';
import { CashRegisterHardDeleteModal } from '@/features/cash-registers/components/CashRegisterHardDeleteModal';
import {
    cashRegisterListQueryKey,
    decommissionCashRegister,
    getCashRegisterCapabilities,
    hardDeleteCashRegister,
    type AdminCashRegisterListItem,
} from '@/features/cash-registers/api/cashRegisters';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    rawRegisterStatus,
    REGISTER_STATUS,
} from '@/features/cash-registers/utils/registerStatus';
import { filterCashRegisters } from '@/features/cash-registers/utils/filterRegisters';
import type { TseHealthStatus } from '@/features/cash-registers/types/enhancedCashRegister';
import styles from '@/app/(protected)/kassenverwaltung/kassenverwaltung.module.css';
import pageStyles from './cash-registers.module.css';

const CAPABILITIES_QUERY_KEY = ['admin', 'cash-registers', 'capabilities'] as const;

const STATS_CARD_BASE_STYLE: React.CSSProperties = {
    height: '100%',
    textAlign: 'center',
};

function statsAccentCardStyle(color?: string): React.CSSProperties {
    if (!color) {
        return STATS_CARD_BASE_STYLE;
    }

    return {
        ...STATS_CARD_BASE_STYLE,
        borderInlineStart: `4px solid ${color}`,
    };
}

function toCashRegister(row: AdminCashRegisterListItem): CashRegister {
    return row as unknown as CashRegister;
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

export default function AdminCashRegistersPage() {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const router = useRouter();
    const queryClient = useQueryClient();
    const { user } = useAuth();
    const isSuperAdminUser = isSuperAdmin(user?.role);

    const {
        canViewCashRegisters,
        canManageCashRegisters,
        canDecommissionCashRegisters,
        hasPermission,
    } = usePermissions();

    const canView = canViewCashRegisters;
    const canCreate = canManageCashRegisters;
    const canDecommission = canDecommissionCashRegisters;
    const canHardDelete = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
    const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);

    const [selectedTenantId, setSelectedTenantId] = useState<string>();
    const [viewMode, setViewMode] = useState<'table' | 'grid'>('table');
    const [searchTerm, setSearchTerm] = useState('');
    const [statusFilter, setStatusFilter] = useState<number | undefined>();
    const [tseHealthFilter, setTseHealthFilter] = useState<TseHealthStatus | undefined>();
    const [showDecommissioned, setShowDecommissioned] = useState(false);
    const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);
    const [selectedRows, setSelectedRows] = useState<CashRegister[]>([]);
    const [bulkDecommissionOpen, setBulkDecommissionOpen] = useState(false);
    const [detailRegister, setDetailRegister] = useState<CashRegister | null>(null);
    const [decommissionRegister, setDecommissionRegister] = useState<CashRegister | null>(null);
    const [hardDeleteRegister, setHardDeleteRegister] = useState<CashRegister | null>(null);
    const [decommissionReason, setDecommissionReason] = useState('');
    const [hardDeleteConfirm, setHardDeleteConfirm] = useState('');
    const [createOpen, setCreateOpen] = useState(false);

    const { tenants, isLoading: tenantsLoading } = useTenantList({
        enabled: isSuperAdminUser && canView,
    });

    const tenantOptions = useMemo(
        () =>
            tenants.map((row) => ({
                value: row.id,
                label: t('cashRegisters.create.tenantOption', { name: row.name, slug: row.slug }),
            })),
        [tenants, t],
    );

    const selectedTenant = useMemo(
        () => tenants.find((row) => row.id === selectedTenantId) ?? null,
        [selectedTenantId, tenants],
    );

    const {
        registers: tenantRegisters,
        isLoading: registersLoading,
        isFetching: registersFetching,
        error: registersError,
        refetch: refetchRegisters,
    } = useAdminCashRegisterList({
        tenantId: selectedTenantId,
        enabled: isSuperAdminUser && Boolean(selectedTenantId) && canView,
        pollIntervalMs: 30_000,
    });

    const allRegisters = useMemo(
        () => tenantRegisters.map(toCashRegister),
        [tenantRegisters],
    );

    const visibleRegisters = useMemo(
        () =>
            filterCashRegisters(allRegisters, {
                search: searchTerm,
                status: statusFilter,
                tseHealth: tseHealthFilter,
                showDecommissioned,
            }),
        [allRegisters, searchTerm, showDecommissioned, statusFilter, tseHealthFilter],
    );

    const hiddenDecommissionedCount = useMemo(
        () =>
            filterCashRegisters(allRegisters, {
                search: searchTerm,
                status: statusFilter,
                showDecommissioned: true,
            }).filter((r) => isDecommissionedRegister(rawRegisterStatus(r))).length,
        [allRegisters, searchTerm, statusFilter],
    );

    const registerSummary = useMemo(() => {
        return allRegisters.reduce(
            (acc, register) => {
                const status = rawRegisterStatus(register);
                if (status === 2) {
                    acc.open += 1;
                } else if (status === 1) {
                    acc.closed += 1;
                } else if (status === 5) {
                    acc.decommissioned += 1;
                }

                if (typeof register.currentBalance === 'number' && Number.isFinite(register.currentBalance)) {
                    acc.totalBalance += register.currentBalance;
                    acc.hasBalanceData = true;
                }

                return acc;
            },
            {
                open: 0,
                closed: 0,
                decommissioned: 0,
                totalBalance: 0,
                hasBalanceData: false,
            },
        );
    }, [allRegisters]);

    const statusLabel = useCallback(
        (status: number | undefined) => {
            switch (status) {
                case 1:
                    return t('cashRegisters.status.closed');
                case 2:
                    return t('cashRegisters.status.open');
                case 3:
                    return t('cashRegisters.status.maintenance');
                case 4:
                    return t('cashRegisters.status.disabled');
                case 5:
                    return t('cashRegisters.status.decommissioned');
                default:
                    return status != null
                        ? t('cashRegisters.status.unknown', { status: String(status) })
                        : '—';
            }
        },
        [t],
    );

    const invalidateRegisterQueries = useCallback(async () => {
        await Promise.all([
            queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers', 'list'] }),
            queryClient.invalidateQueries({ queryKey: cashRegisterListQueryKey }),
        ]);
    }, [queryClient]);

    const decommissionMutation = useMutation({
        mutationFn: ({ id, reason }: { id: string; reason: string }) =>
            decommissionCashRegister(id, { reason: reason || null }),
        onSuccess: async () => {
            message.success(t('cashRegisters.decommission.success'));
            setDecommissionRegister(null);
            setDecommissionReason('');
            await invalidateRegisterQueries();
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'AdminCashRegisters.decommission',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    const hardDeleteMutation = useMutation({
        mutationFn: ({ id, confirmPhrase }: { id: string; confirmPhrase: string }) =>
            hardDeleteCashRegister(id, { confirmPhrase }),
        onSuccess: async () => {
            message.success(t('cashRegisters.hardDelete.success'));
            setHardDeleteRegister(null);
            setHardDeleteConfirm('');
            setDetailRegister(null);
            await invalidateRegisterQueries();
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'AdminCashRegisters.hardDelete',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    const capabilitiesQuery = useQuery({
        queryKey: CAPABILITIES_QUERY_KEY,
        queryFn: getCashRegisterCapabilities,
        enabled: canView && isSuperAdminUser,
        staleTime: 60_000,
    });

    const allowHardDeleteUi =
        canHardDelete && (capabilitiesQuery.data?.allowHardDelete ?? false);

    const submitDecommission = useCallback((nextReason?: string) => {
        const reg = decommissionRegister;
        const id = reg?.id?.trim();
        if (!id) {
            return;
        }
        if (!canDecommissionRegister(rawRegisterStatus(reg!))) {
            message.error(t('cashRegisters.decommission.mustCloseFirst'));
            return;
        }
        decommissionMutation.mutate({
            id,
            reason: nextReason?.trim() || decommissionReason.trim() || 'Admin tenant cash register decommission',
        });
    }, [decommissionRegister, decommissionReason, decommissionMutation, t]);

    const bulkDecommissionMutation = useMutation({
        mutationFn: async ({ registers, reason }: { registers: CashRegister[]; reason: string }) => {
            const eligible = registers.filter((r) => canDecommissionRegister(rawRegisterStatus(r)));
            const results = await Promise.allSettled(
                eligible.map((r) =>
                    decommissionCashRegister(r.id!, {
                        reason: reason || 'Bulk admin decommission',
                    }),
                ),
            );
            const success = results.filter((r) => r.status === 'fulfilled').length;
            const failed = results.length - success;
            return { success, failed, total: eligible.length };
        },
        onSuccess: async ({ success, failed }) => {
            if (failed === 0) {
                message.success(t('cashRegisters.bulk.success', { count: success }));
            } else {
                message.warning(
                    t('cashRegisters.bulk.partialSuccess', { success, failed }),
                );
            }
            setBulkDecommissionOpen(false);
            setSelectedRowKeys([]);
            setSelectedRows([]);
            await invalidateRegisterQueries();
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'AdminCashRegisters.bulkDecommission',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    const submitHardDelete = useCallback(() => {
        const id = hardDeleteRegister?.id?.trim();
        if (!id) {
            return;
        }
        hardDeleteMutation.mutate({ id, confirmPhrase: hardDeleteConfirm.trim() });
    }, [hardDeleteRegister, hardDeleteConfirm, hardDeleteMutation]);

    const exportRegisters = useCallback(() => {
        if (visibleRegisters.length === 0) {
            return;
        }

        const header = [
            t('cashRegisters.columns.name'),
            t('cashRegisters.columns.status'),
            t('cashRegisters.detail.currentBalance'),
            t('cashRegisters.detail.lastBalanceUpdate'),
            t('cashRegisters.detail.currentUser'),
        ];

        const lines = visibleRegisters.map((register) =>
            [
                register.registerNumber?.trim() || '',
                statusLabel(rawRegisterStatus(register)),
                typeof register.currentBalance === 'number' ? String(register.currentBalance) : '',
                register.lastBalanceUpdate ?? '',
                register.currentUser?.userName?.trim() || register.currentUserId?.trim() || '',
            ]
                .map((value) => toCsvCell(value))
                .join(';'),
        );

        downloadCsv(
            `cash-registers_${new Date().toISOString().slice(0, 10)}.csv`,
            [header.map((value) => toCsvCell(value)).join(';'), ...lines].join('\n'),
        );
        message.success(t('cashRegisters.export.success'));
    }, [statusLabel, t, visibleRegisters]);

    if (!canView) {
        return (
            <AdminPageShell>
                <Alert type="warning" showIcon title={t('errors.forbidden.FORBIDDEN')} />
            </AdminPageShell>
        );
    }

    if (!isSuperAdminUser) {
        return (
            <AdminPageShell>
                <AdminPageHeader
                    title={t('cashRegisters.pageTitle')}
                    breadcrumbs={[
                        adminOverviewCrumb(t),
                        { title: t('cashRegisters.pageTitle'), href: '/kassenverwaltung' },
                    ]}
                />
                <Card title={t('cashRegisters.pageTitle')}>
                    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                            {t('cashRegisters.adminPage.managerIntro')}
                        </Typography.Paragraph>
                        <CashRegisterSelector
                            showTenantPicker={false}
                            onChange={() => {
                                router.push('/kassenverwaltung');
                            }}
                        />
                        <Link href="/kassenverwaltung">{t('cashRegisters.adminPage.openKassenverwaltung')}</Link>
                    </Space>
                </Card>
            </AdminPageShell>
        );
    }

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('cashRegisters.adminPage.title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t(ADMIN_NAV_LABEL_KEYS.superAdminCashRegisters) },
                ]}
            />
            <AdminPageScopeSummary label={t('cashRegisters.adminPage.title')}>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t('cashRegisters.adminPage.intro')}
                </Typography.Paragraph>
            </AdminPageScopeSummary>

            <CashRegisterShiftRksvGuide />

            <Card>
                <Space orientation="vertical" size="middle" style={{ width: '100%', marginBottom: 16 }}>
                    <Space wrap align="center">
                        <Typography.Text>{t('cashRegisters.adminPage.selectTenant')}:</Typography.Text>
                        <Select
                            style={{ minWidth: 280 }}
                            placeholder={t('cashRegisters.create.tenantPlaceholder')}
                            value={selectedTenantId}
                            onChange={setSelectedTenantId}
                            options={tenantOptions}
                            showSearch
                            optionFilterProp="label"
                            loading={tenantsLoading}
                            allowClear
                        />
                        {selectedTenantId ? (
                            <Segmented<'table' | 'grid'>
                                value={viewMode}
                                onChange={(value) => setViewMode(value)}
                                options={[
                                    {
                                        label: t('cashRegisters.actions.tableView'),
                                        value: 'table',
                                        icon: <BarsOutlined />,
                                    },
                                    {
                                        label: t('cashRegisters.actions.gridView'),
                                        value: 'grid',
                                        icon: <AppstoreOutlined />,
                                    },
                                ]}
                            />
                        ) : null}
                    </Space>

                    {selectedTenant ? (
                        <Alert
                            type="info"
                            showIcon
                            title={t('cashRegisters.create.tenantOption', {
                                name: selectedTenant.name,
                                slug: selectedTenant.slug,
                            })}
                            description={
                                <Space wrap>
                                    <Button size="small" href="/rksv/status">
                                        {t('adminShell.hospitalityHub.linkRksvStatus')}
                                    </Button>
                                    {canDecommission && canOpenSonderbelege ? (
                                        <Button size="small" href="/rksv/sonderbelege?focus=schlussbeleg">
                                            {t('nav.rksvLeafSonderbelege')}
                                        </Button>
                                    ) : null}
                                </Space>
                            }
                        />
                    ) : null}

                    {selectedTenantId ? (
                        <Row gutter={[16, 16]}>
                            <Col xs={24} sm={12} lg={6}>
                                <Card
                                    size="small"
                                    loading={registersLoading}
                                    style={statsAccentCardStyle()}
                                >
                                    <Statistic
                                        title={t('cashRegisters.adminPage.statsTotal')}
                                        value={allRegisters.length}
                                        prefix={<ShopOutlined />}
                                    />
                                </Card>
                            </Col>
                            <Col xs={24} sm={12} lg={6}>
                                <Card
                                    size="small"
                                    loading={registersLoading}
                                    style={statsAccentCardStyle('#52c41a')}
                                >
                                    <Statistic
                                        title={t('cashRegisters.status.open')}
                                        value={registerSummary.open}
                                        prefix={<CheckCircleOutlined />}
                                        styles={{ content: {  color: '#389e0d'  } }}
                                    />
                                </Card>
                            </Col>
                            <Col xs={24} sm={12} lg={6}>
                                <Card
                                    size="small"
                                    loading={registersLoading}
                                    style={statsAccentCardStyle('#1677ff')}
                                >
                                    <Statistic
                                        title={t('cashRegisters.status.closed')}
                                        value={registerSummary.closed}
                                        prefix={<LockOutlined />}
                                        styles={{ content: {  color: '#1677ff'  } }}
                                    />
                                </Card>
                            </Col>
                            <Col xs={24} sm={12} lg={6}>
                                <Card
                                    size="small"
                                    loading={registersLoading}
                                    style={statsAccentCardStyle('#ff4d4f')}
                                >
                                    <Statistic
                                        title={t('cashRegisters.status.decommissioned')}
                                        value={registerSummary.decommissioned}
                                        prefix={<StopOutlined />}
                                        styles={{ content: {  color: '#cf1322'  } }}
                                    />
                                </Card>
                            </Col>
                            {registerSummary.hasBalanceData ? (
                                <Col xs={24}>
                                    <Card size="small" loading={registersLoading}>
                                        <Statistic
                                            title={t('cashRegisters.detail.currentBalance')}
                                            value={registerSummary.totalBalance}
                                            precision={2}
                                            suffix="EUR"
                                        />
                                    </Card>
                                </Col>
                            ) : null}
                        </Row>
                    ) : null}

                    {selectedTenantId ? (
                        <Space
                            wrap
                            align="center"
                            style={{ width: '100%' }}
                            className={pageStyles.filtersBar}
                        >
                            <Input.Search
                                placeholder={t('cashRegisters.filter.searchPlaceholder')}
                                style={{ width: 250 }}
                                value={searchTerm}
                                onChange={(event) => setSearchTerm(event.target.value)}
                                onSearch={(value) => setSearchTerm(value)}
                                allowClear
                            />
                            <Select<TseHealthStatus>
                                placeholder={t('cashRegisters.tseHealthFilter.placeholder')}
                                style={{ width: 190 }}
                                value={tseHealthFilter}
                                onChange={setTseHealthFilter}
                                allowClear
                                options={[
                                    { label: t('cashRegisters.tseHealthFilter.healthy'), value: 'healthy' },
                                    { label: t('cashRegisters.tseHealthFilter.degraded'), value: 'degraded' },
                                    { label: t('cashRegisters.tseHealthFilter.offline'), value: 'offline' },
                                    {
                                        label: t('cashRegisters.tseHealthFilter.notConfigured'),
                                        value: 'notConfigured',
                                    },
                                ]}
                            />
                            <Select<number>
                                placeholder={t('cashRegisters.filter.statusPlaceholder')}
                                style={{ width: 170 }}
                                value={statusFilter}
                                onChange={setStatusFilter}
                                allowClear
                                options={[
                                    {
                                        label: (
                                            <>
                                                <CheckCircleOutlined /> {t('cashRegisters.status.open')}
                                            </>
                                        ),
                                        value: REGISTER_STATUS.open,
                                    },
                                    {
                                        label: (
                                            <>
                                                <LockOutlined /> {t('cashRegisters.status.closed')}
                                            </>
                                        ),
                                        value: REGISTER_STATUS.closed,
                                    },
                                    {
                                        label: (
                                            <>
                                                <StopOutlined /> {t('cashRegisters.status.decommissioned')}
                                            </>
                                        ),
                                        value: REGISTER_STATUS.decommissioned,
                                    },
                                    {
                                        label: (
                                            <>
                                                <ToolOutlined /> {t('cashRegisters.status.maintenance')}
                                            </>
                                        ),
                                        value: REGISTER_STATUS.maintenance,
                                    },
                                ]}
                            />
                            <Switch
                                checkedChildren={t('cashRegisters.filter.showDecommissionedOn')}
                                unCheckedChildren={t('cashRegisters.filter.showDecommissionedOff')}
                                checked={showDecommissioned}
                                onChange={setShowDecommissioned}
                            />
                            {canCreate ? (
                                <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
                                    {t('cashRegisters.actions.create')}
                                </Button>
                            ) : null}
                            <Button
                                icon={<ReloadOutlined />}
                                onClick={() => void refetchRegisters()}
                                loading={registersFetching}
                                disabled={!selectedTenantId}
                            >
                                {t('cashRegisters.actions.refresh')}
                            </Button>
                            <Button
                                icon={<ExportOutlined />}
                                onClick={exportRegisters}
                                disabled={visibleRegisters.length === 0}
                            >
                                {t('cashRegisters.actions.export')}
                            </Button>
                        </Space>
                    ) : null}
                </Space>

                {!selectedTenantId ? (
                    <Empty description={t('cashRegisters.adminPage.selectTenantHint')} />
                ) : registersError ? (
                    <Alert
                        type="error"
                        showIcon
                        title={t('cashRegisters.errors.loadFailed')}
                        style={{ marginBottom: 16 }}
                    />
                ) : (
                    <>
                        {!showDecommissioned && hiddenDecommissionedCount > 0 ? (
                            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
                                {t('cashRegisters.filter.hiddenCount', {
                                    count: hiddenDecommissionedCount,
                                })}
                            </Typography.Text>
                        ) : null}
                        {viewMode === 'grid' ? (
                            <CashRegisterGrid
                                registers={visibleRegisters}
                                loading={registersLoading}
                                canCreate={canCreate}
                                canManage={canCreate}
                                totalRegisterCount={allRegisters.length}
                                canDecommission={canDecommission}
                                statusLabel={statusLabel}
                                onEdit={setDetailRegister}
                                onDecommission={(record) => {
                                    setDecommissionReason('');
                                    setDecommissionRegister(record);
                                }}
                            />
                        ) : (
                            <>
                                {canDecommission ? (
                                    <BulkDecommissionBar
                                        selectedCount={selectedRows.length}
                                        disabled={bulkDecommissionMutation.isPending}
                                        onDecommission={() => setBulkDecommissionOpen(true)}
                                    />
                                ) : null}
                                <CashRegisterTable
                                    registers={visibleRegisters}
                                    loading={registersLoading}
                                    canCreate={canCreate}
                                    canManage={canCreate}
                                    totalRegisterCount={allRegisters.length}
                                    canDecommission={canDecommission}
                                    statusLabel={statusLabel}
                                    selectedRowKeys={canDecommission ? selectedRowKeys : undefined}
                                    onSelectionChange={
                                        canDecommission
                                            ? (keys, rows) => {
                                                  setSelectedRowKeys(keys);
                                                  setSelectedRows(rows);
                                              }
                                            : undefined
                                    }
                                    rowClassName={(record) =>
                                        isDecommissionedRegister(rawRegisterStatus(record))
                                            ? styles.decommissionedRow
                                            : ''
                                    }
                                    onEdit={setDetailRegister}
                                    onDecommission={(record) => {
                                        setDecommissionReason('');
                                        setDecommissionRegister(record);
                                    }}
                                />
                            </>
                        )}
                    </>
                )}
            </Card>

            <RegisterDetailModal
                open={detailRegister != null}
                register={detailRegister}
                onClose={() => setDetailRegister(null)}
                statusLabel={statusLabel}
                showHardDelete={allowHardDeleteUi}
                onHardDelete={
                    allowHardDeleteUi && detailRegister
                        ? () => {
                              setHardDeleteConfirm('');
                              setHardDeleteRegister(detailRegister);
                          }
                        : undefined
                }
            />

            <BulkDecommissionModal
                open={bulkDecommissionOpen}
                registers={selectedRows}
                onCancel={() => setBulkDecommissionOpen(false)}
                onConfirm={(reason) =>
                    bulkDecommissionMutation.mutate({ registers: selectedRows, reason })
                }
                confirmLoading={bulkDecommissionMutation.isPending}
            />

            <CreateCashRegisterModal
                visible={createOpen}
                tenantId={selectedTenantId}
                onClose={() => setCreateOpen(false)}
                onSuccess={() => {
                    void invalidateRegisterQueries();
                }}
            />

            <DecommissionModal
                open={decommissionRegister != null}
                register={decommissionRegister}
                reason={decommissionReason}
                onReasonChange={setDecommissionReason}
                onCancel={() => {
                    setDecommissionRegister(null);
                    setDecommissionReason('');
                }}
                onConfirm={submitDecommission}
                confirmLoading={decommissionMutation.isPending}
            />

            <CashRegisterHardDeleteModal
                open={hardDeleteRegister != null}
                register={hardDeleteRegister}
                confirmText={hardDeleteConfirm}
                onConfirmTextChange={setHardDeleteConfirm}
                onCancel={() => {
                    setHardDeleteRegister(null);
                    setHardDeleteConfirm('');
                }}
                onConfirm={submitHardDelete}
                confirmLoading={hardDeleteMutation.isPending}
            />
        </AdminPageShell>
    );
}
