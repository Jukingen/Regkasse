'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Alert, Button, Card, Checkbox, Empty, Space, Tag, Typography } from 'antd';
import { ReloadOutlined, PlusOutlined } from '@ant-design/icons';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import type { CashRegister } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { useCashRegisterModuleAccess } from '@/features/cash-registers/hooks/useCashRegisterModuleAccess';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { CashRegisterShiftRksvGuide } from '@/features/cash-registers/components/CashRegisterShiftRksvGuide';
import { CashRegisterTable } from '@/features/cash-registers/components/CashRegisterTable';
import { CashRegisterTenantSelector } from '@/features/cash-registers/components/CashRegisterTenantSelector';
import { CreateCashRegisterModal } from '@/features/cash-registers/components/CreateCashRegisterModal';
import { DecommissionModal } from '@/features/cash-registers/components/DecommissionModal';
import { CashRegisterDetailDrawer } from '@/features/cash-registers/components/CashRegisterDetailDrawer';
import { CashRegisterHardDeleteModal } from '@/features/cash-registers/components/CashRegisterHardDeleteModal';
import {
    type AdminCashRegisterListItem,
    cashRegisterListQueryKey,
    decommissionCashRegister,
    getCashRegisterCapabilities,
    hardDeleteCashRegister,
} from '@/features/cash-registers/api/cashRegisters';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import { useCashRegisterActionHandler } from '@/features/cash-registers/hooks/useCashRegisterActionHandler';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    rawRegisterStatus,
} from '@/features/cash-registers/utils/registerStatus';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import {
    FA_QUICK_CASH_REGISTER_QUERY_PARAM,
    readQuickCashRegisterId,
    writeQuickCashRegisterId,
} from '@/features/cash-registers/constants/quickSwitch';
import styles from './kassenverwaltung.module.css';

const CAPABILITIES_QUERY_KEY = ['admin', 'cash-registers', 'capabilities'] as const;

type CashRegisterViewItem = CashRegister & {
    tenantId?: string | null;
};

function toCashRegisterViewItem(row: AdminCashRegisterListItem): CashRegisterViewItem {
    return row as unknown as CashRegisterViewItem;
}

export default function KassenverwaltungPage() {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const searchParams = useSearchParams();
    const router = useRouter();
    const queryClient = useQueryClient();
    const deepLinkHandledRef = useRef(false);
    const {
        canAccessPage,
        canLoadRegisters,
        canManageCashRegisters,
        hasPermission,
        licenseBlocksModule,
        licenseLoading,
        tenantId,
        isSuperAdminUser,
    } = useCashRegisterModuleAccess();

    const canView = canAccessPage;
    /** Register lifecycle (create / decommission / hard delete) — Super Admin only in FA. */
    const canCreate = isSuperAdminUser;
    const canDecommission = isSuperAdminUser;
    /** Shift open/close and daily closing — Manager with cash_register.manage. */
    const canOperate = canManageCashRegisters;
    const canHardDelete = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
    const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);

    const [selectedTenantId, setSelectedTenantId] = useState<string>();
    const [showDecommissioned, setShowDecommissioned] = useState(false);
    const [detailRegister, setDetailRegister] = useState<CashRegister | null>(null);
    const [decommissionRegister, setDecommissionRegister] = useState<CashRegister | null>(null);
    const [hardDeleteRegister, setHardDeleteRegister] = useState<CashRegister | null>(null);
    const [decommissionReason, setDecommissionReason] = useState('');
    const [hardDeleteConfirm, setHardDeleteConfirm] = useState('');
    const [createOpen, setCreateOpen] = useState(false);

    const { tenants, isLoading: tenantsLoading } = useTenantList({
        enabled: canView && isSuperAdminUser,
    });

    const selectedTenant = useMemo(
        () => tenants.find((row) => row.id === selectedTenantId) ?? null,
        [selectedTenantId, tenants],
    );

    const capabilitiesQuery = useQuery({
        queryKey: CAPABILITIES_QUERY_KEY,
        queryFn: getCashRegisterCapabilities,
        enabled: canLoadRegisters,
        staleTime: 60_000,
    });

    const allowHardDeleteUi =
        canHardDelete && (capabilitiesQuery.data?.allowHardDelete ?? false);

    const tenantRegistersQuery = useAdminCashRegisterList({
        tenantId: tenantId ?? undefined,
        allowTenantScopedDefault: !isSuperAdminUser,
        enabled: canLoadRegisters && !isSuperAdminUser,
        excludeDecommissioned: false,
    });

    const adminRegistersQuery = useAdminCashRegisterList({
        tenantId: selectedTenantId,
        allowAllTenants: isSuperAdminUser && !selectedTenantId,
        enabled: canLoadRegisters && isSuperAdminUser,
        excludeDecommissioned: false,
    });

    const allRegisters = useMemo(
        (): CashRegisterViewItem[] =>
            isSuperAdminUser
                ? adminRegistersQuery.registers.map(toCashRegisterViewItem)
                : tenantRegistersQuery.registers.map(toCashRegisterViewItem),
        [adminRegistersQuery.registers, isSuperAdminUser, tenantRegistersQuery.registers],
    );

    const visibleRegisters = useMemo(() => {
        if (showDecommissioned) return allRegisters;
        return allRegisters.filter((r) => !isDecommissionedRegister(rawRegisterStatus(r)));
    }, [allRegisters, showDecommissioned]);

    const hiddenDecommissionedCount = useMemo(
        () => allRegisters.filter((r) => isDecommissionedRegister(rawRegisterStatus(r))).length,
        [allRegisters],
    );

    const groupedRegisters = useMemo(() => {
        if (!isSuperAdminUser || selectedTenantId || visibleRegisters.length === 0) {
            return [];
        }

        const groups = new Map<string, CashRegisterViewItem[]>();
        for (const register of visibleRegisters) {
            const tenantKey = register.tenantId?.trim() || '__unknown__';
            const existing = groups.get(tenantKey);
            if (existing) {
                existing.push(register);
            } else {
                groups.set(tenantKey, [register]);
            }
        }

        return Array.from(groups.entries())
            .map(([tenantKey, registers]) => ({
                tenantId: tenantKey === '__unknown__' ? null : tenantKey,
                tenant:
                    tenantKey === '__unknown__'
                        ? null
                        : tenants.find((row) => row.id === tenantKey) ?? null,
                tenantName: registers[0]?.tenantName?.trim() || null,
                tenantSlug: registers[0]?.tenantSlug?.trim() || null,
                registers,
            }))
            .sort((a, b) =>
                (a.tenant?.name ?? a.tenantId ?? '').localeCompare(
                    b.tenant?.name ?? b.tenantId ?? '',
                    'de',
                ),
            );
    }, [isSuperAdminUser, selectedTenantId, tenants, visibleRegisters]);

    const registersLoading = isSuperAdminUser
        ? adminRegistersQuery.isLoading
        : tenantRegistersQuery.isLoading;
    const registersFetching = isSuperAdminUser
        ? adminRegistersQuery.isFetching
        : tenantRegistersQuery.isFetching;
    const registersError = isSuperAdminUser
        ? adminRegistersQuery.error != null
        : tenantRegistersQuery.error != null;
    const showGroupedTenantView = isSuperAdminUser && !selectedTenantId;

    useEffect(() => {
        if (searchParams.get('create') !== '1' || !canCreate) return;
        setCreateOpen(true);
        router.replace('/kassenverwaltung');
    }, [searchParams, canCreate, router]);

    useEffect(() => {
        if (deepLinkHandledRef.current || allRegisters.length === 0) {
            return;
        }
        const fromQuery = searchParams.get(FA_QUICK_CASH_REGISTER_QUERY_PARAM)?.trim();
        const targetId = fromQuery || readQuickCashRegisterId(tenantId ?? null);
        if (!targetId) {
            return;
        }
        const register = allRegisters.find((row) => row.id === targetId);
        if (!register) {
            return;
        }
        deepLinkHandledRef.current = true;
        writeQuickCashRegisterId(register.id ?? targetId, register.tenantId ?? tenantId ?? null);
        setDetailRegister(register);
    }, [allRegisters, searchParams, tenantId]);

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

    const invalidateRegisters = useCallback(async () => {
        await Promise.all([
            queryClient.invalidateQueries({ queryKey: cashRegisterListQueryKey }),
            queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers', 'list'] }),
        ]);
    }, [queryClient]);

    const decommissionMutation = useMutation({
        mutationFn: ({ id, reason }: { id: string; reason: string }) =>
            decommissionCashRegister(id, { reason: reason || null }),
        onSuccess: async () => {
            message.success(t('cashRegisters.decommission.success'));
            setDecommissionRegister(null);
            setDecommissionReason('');
            await invalidateRegisters();
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'Kassenverwaltung.decommission',
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
            await invalidateRegisters();
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'Kassenverwaltung.hardDelete',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    const submitDecommission = useCallback((nextReason?: string) => {
        const reg = decommissionRegister;
        const id = reg?.id?.trim();
        if (!id) return;
        if (!canDecommissionRegister(rawRegisterStatus(reg!))) {
            message.error(t('cashRegisters.decommission.mustCloseFirst'));
            return;
        }
        decommissionMutation.mutate({
            id,
            reason: nextReason?.trim() || decommissionReason.trim() || 'Kassenverwaltung Stilllegung',
        });
    }, [decommissionRegister, decommissionReason, decommissionMutation, t]);

    const submitHardDelete = useCallback(() => {
        const id = hardDeleteRegister?.id?.trim();
        if (!id) return;
        hardDeleteMutation.mutate({ id, confirmPhrase: hardDeleteConfirm.trim() });
    }, [hardDeleteRegister, hardDeleteConfirm, hardDeleteMutation]);

    const openCreateModal = useCallback(() => {
        setCreateOpen(true);
    }, []);

    const closeCreateModal = useCallback(() => {
        setCreateOpen(false);
    }, []);

    const { handleRegisterAction } = useCashRegisterActionHandler({
        onEdit: setDetailRegister,
        onDecommission: (register) => {
            setDecommissionReason('');
            setDecommissionRegister(register);
        },
        onHardDelete: (register) => {
            setHardDeleteConfirm('');
            setHardDeleteRegister(register);
        },
    });

    if (!canAccessPage) {
        return (
            <AdminPageShell>
                <Alert type="warning" showIcon title={t('errors.forbidden.FORBIDDEN')} />
            </AdminPageShell>
        );
    }

    if (licenseBlocksModule && !licenseLoading) {
        return (
            <AdminPageShell>
                <AdminPageHeader
                    title={t('cashRegisters.pageTitle')}
                    breadcrumbs={[adminOverviewCrumb(t), { title: t('cashRegisters.pageTitle') }]}
                />
                <Alert
                    type="info"
                    showIcon
                    title={t('cashRegisters.moduleUnavailable')}
                    description={t('cashRegisters.moduleUnavailableDescription')}
                />
            </AdminPageShell>
        );
    }

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('cashRegisters.pageTitle')}
                breadcrumbs={[adminOverviewCrumb(t), { title: t('cashRegisters.pageTitle') }]}
            />
            <AdminPageScopeSummary label={t('cashRegisters.pageTitle')}>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t('cashRegisters.pageIntro')}
                </Typography.Paragraph>
            </AdminPageScopeSummary>

            <CashRegisterShiftRksvGuide />

            <Card>
                <Space style={{ marginBottom: 16 }} wrap align="center">
                    <Button
                        icon={<ReloadOutlined />}
                        onClick={() =>
                            void (isSuperAdminUser
                                ? adminRegistersQuery.refetch()
                                : tenantRegistersQuery.refetch())
                        }
                        loading={registersFetching}
                    >
                        {t('cashRegisters.actions.refresh')}
                    </Button>
                    {canCreate && (
                        <Button type="primary" onClick={openCreateModal}>
                            <PlusOutlined /> {t('cashRegisters.actions.create')}
                        </Button>
                    )}
                    <Checkbox
                        checked={showDecommissioned}
                        onChange={(e) => setShowDecommissioned(e.target.checked)}
                    >
                        {t('cashRegisters.filter.showDecommissioned')}
                    </Checkbox>
                    {!showDecommissioned && hiddenDecommissionedCount > 0 ? (
                        <Typography.Text type="secondary">
                            {t('cashRegisters.filter.hiddenCount', {
                                count: hiddenDecommissionedCount,
                            })}
                        </Typography.Text>
                    ) : null}
                    {canDecommission && canOpenSonderbelege ? (
                        <Link href="/rksv/sonderbelege?focus=schlussbeleg">
                            {t('cashRegisters.decommission.hintSchlussbelegLink')}
                        </Link>
                    ) : null}
                </Space>

                {isSuperAdminUser ? (
                    <Space style={{ marginBottom: 16 }} wrap align="center">
                        <Typography.Text>{t('cashRegisters.adminPage.selectTenant')}:</Typography.Text>
                        <CashRegisterTenantSelector
                            value={selectedTenantId}
                            onChange={setSelectedTenantId}
                            tenants={tenants}
                            loading={tenantsLoading}
                        />
                        <Tag color={selectedTenant ? 'default' : 'blue'}>
                            {selectedTenant?.slug ?? 'Super Admin'}
                        </Tag>
                    </Space>
                ) : null}

                {registersError ? (
                    <Alert
                        type="error"
                        showIcon
                        title={t('cashRegisters.errors.loadFailed')}
                        style={{ marginBottom: 16 }}
                    />
                ) : null}

                {!registersError && showGroupedTenantView ? (
                    visibleRegisters.length === 0 ? (
                        <Empty
                            description={
                                allRegisters.length === 0
                                    ? canCreate
                                        ? t('cashRegisters.emptyCanCreate')
                                        : t('cashRegisters.emptyContactAdmin')
                                    : t('cashRegisters.empty')
                            }
                        />
                    ) : (
                        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                            {groupedRegisters.map((group) => (
                                <div key={group.tenantId ?? 'unknown-tenant'}>
                                    <Space
                                        wrap
                                        align="center"
                                        style={{ marginBottom: 12, width: '100%' }}
                                    >
                                        <Typography.Title level={5} style={{ margin: 0 }}>
                                            {group.tenant?.name ?? group.tenantName ?? group.tenantId ?? '—'}
                                        </Typography.Title>
                                        {group.tenant?.slug || group.tenantSlug ? (
                                            <Tag>{group.tenant?.slug ?? group.tenantSlug}</Tag>
                                        ) : null}
                                    </Space>
                                    <CashRegisterTable
                                        registers={group.registers}
                                        loading={registersLoading}
                                        canCreate={canCreate}
                                        canManage={canOperate}
                                        totalRegisterCount={group.registers.length}
                                        canDecommission={canDecommission}
                                        statusLabel={statusLabel}
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
                                        onRegisterAction={handleRegisterAction}
                                    />
                                </div>
                            ))}
                        </Space>
                    )
                ) : !registersError ? (
                    <CashRegisterTable
                        registers={visibleRegisters}
                        loading={registersLoading}
                        canCreate={canCreate}
                        canManage={canOperate}
                        totalRegisterCount={allRegisters.length}
                        canDecommission={canDecommission}
                        statusLabel={statusLabel}
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
                        onRegisterAction={handleRegisterAction}
                    />
                ) : null}
            </Card>

            <CashRegisterDetailDrawer
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

            <CreateCashRegisterModal
                visible={createOpen}
                tenantId={isSuperAdminUser ? selectedTenantId : (tenantId ?? undefined)}
                onClose={closeCreateModal}
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
