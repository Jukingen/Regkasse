'use client';

import React, { useCallback, useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Checkbox,
    Empty,
    Select,
    Space,
    Typography,
    message,
} from 'antd';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
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
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { CashRegisterSelector } from '@/features/cash-registers/components/CashRegisterSelector';
import { CashRegisterTable } from '@/features/cash-registers/components/CashRegisterTable';
import { CreateCashRegisterModal } from '@/features/cash-registers/components/CreateCashRegisterModal';
import { DecommissionModal } from '@/features/cash-registers/components/DecommissionModal';
import { CashRegisterDetailDrawer } from '@/features/cash-registers/components/CashRegisterDetailDrawer';
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
} from '@/features/cash-registers/utils/registerStatus';
import styles from '@/app/(protected)/kassenverwaltung/kassenverwaltung.module.css';

const CAPABILITIES_QUERY_KEY = ['admin', 'cash-registers', 'capabilities'] as const;

function toCashRegister(row: AdminCashRegisterListItem): CashRegister {
    return row as unknown as CashRegister;
}

export default function AdminCashRegistersPage() {
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

    const [selectedTenantId, setSelectedTenantId] = useState<string>();
    const [showDecommissioned, setShowDecommissioned] = useState(false);
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

    const {
        registers: tenantRegisters,
        isLoading: registersLoading,
        isFetching: registersFetching,
        error: registersError,
        refetch: refetchRegisters,
    } = useAdminCashRegisterList({
        tenantId: selectedTenantId,
        enabled: isSuperAdminUser && Boolean(selectedTenantId) && canView,
    });

    const allRegisters = useMemo(
        () => tenantRegisters.map(toCashRegister),
        [tenantRegisters],
    );

    const visibleRegisters = useMemo(() => {
        if (showDecommissioned) {
            return allRegisters;
        }
        return allRegisters.filter((r) => !isDecommissionedRegister(rawRegisterStatus(r)));
    }, [allRegisters, showDecommissioned]);

    const hiddenDecommissionedCount = useMemo(
        () => allRegisters.filter((r) => isDecommissionedRegister(rawRegisterStatus(r))).length,
        [allRegisters],
    );

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

    const submitDecommission = useCallback(() => {
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
            reason: decommissionReason.trim() || 'Admin tenant cash register decommission',
        });
    }, [decommissionRegister, decommissionReason, decommissionMutation, t]);

    const submitHardDelete = useCallback(() => {
        const id = hardDeleteRegister?.id?.trim();
        if (!id) {
            return;
        }
        hardDeleteMutation.mutate({ id, confirmPhrase: hardDeleteConfirm.trim() });
    }, [hardDeleteRegister, hardDeleteConfirm, hardDeleteMutation]);

    if (!canView) {
        return (
            <AdminPageShell>
                <Alert type="warning" showIcon message={t('errors.forbidden.FORBIDDEN')} />
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
                    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
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

            <Card>
                <Space style={{ marginBottom: 16 }} wrap align="center">
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
                    <Button
                        icon={<ReloadOutlined />}
                        onClick={() => void refetchRegisters()}
                        loading={registersFetching}
                        disabled={!selectedTenantId}
                    >
                        {t('cashRegisters.actions.refresh')}
                    </Button>
                    {canCreate && selectedTenantId ? (
                        <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
                            {t('cashRegisters.actions.create')}
                        </Button>
                    ) : null}
                    {selectedTenantId ? (
                        <Checkbox
                            checked={showDecommissioned}
                            onChange={(e) => setShowDecommissioned(e.target.checked)}
                        >
                            {t('cashRegisters.filter.showDecommissioned')}
                        </Checkbox>
                    ) : null}
                    {canDecommission ? (
                        <Link href="/rksv/sonderbelege?focus=schlussbeleg">
                            {t('cashRegisters.decommission.hintSchlussbelegLink')}
                        </Link>
                    ) : null}
                </Space>

                {!selectedTenantId ? (
                    <Empty description={t('cashRegisters.adminPage.selectTenantHint')} />
                ) : registersError ? (
                    <Alert
                        type="error"
                        showIcon
                        message={t('cashRegisters.errors.loadFailed')}
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
                        <CashRegisterTable
                            registers={visibleRegisters}
                            loading={registersLoading}
                            canCreate={canCreate}
                            canManage={canCreate}
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
                        />
                    </>
                )}
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
