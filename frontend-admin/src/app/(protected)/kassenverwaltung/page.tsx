'use client';

import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Card, Checkbox, Space, Typography, message } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { getApiCashRegister, getGetApiCashRegisterQueryKey } from '@/api/generated/cash-register/cash-register';
import type { CashRegister } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { normalizeCashRegisterList } from '@/features/cash-registers/normalizers';
import { CashRegisterTable } from '@/features/cash-registers/components/CashRegisterTable';
import { DecommissionModal } from '@/features/cash-registers/components/DecommissionModal';
import { CashRegisterDetailDrawer } from '@/features/cash-registers/components/CashRegisterDetailDrawer';
import { CashRegisterHardDeleteModal } from '@/features/cash-registers/components/CashRegisterHardDeleteModal';
import {
    decommissionCashRegister,
    getCashRegisterCapabilities,
    hardDeleteCashRegister,
} from '@/features/cash-registers/api/cashRegisters';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    rawRegisterStatus,
} from '@/features/cash-registers/utils/registerStatus';
import styles from './kassenverwaltung.module.css';

const CAPABILITIES_QUERY_KEY = ['admin', 'cash-registers', 'capabilities'] as const;

export default function KassenverwaltungPage() {
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const { hasPermission } = usePermissions();

    const canView = hasPermission(PERMISSIONS.CASHREGISTER_VIEW);
    const canDecommission = hasPermission(PERMISSIONS.RKSV_SCHLUSSBELEG_CREATE);
    const canHardDelete = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

    const [showDecommissioned, setShowDecommissioned] = useState(false);
    const [detailRegister, setDetailRegister] = useState<CashRegister | null>(null);
    const [decommissionRegister, setDecommissionRegister] = useState<CashRegister | null>(null);
    const [hardDeleteRegister, setHardDeleteRegister] = useState<CashRegister | null>(null);
    const [decommissionReason, setDecommissionReason] = useState('');
    const [hardDeleteConfirm, setHardDeleteConfirm] = useState('');

    const capabilitiesQuery = useQuery({
        queryKey: CAPABILITIES_QUERY_KEY,
        queryFn: getCashRegisterCapabilities,
        enabled: canView,
        staleTime: 60_000,
    });

    const allowHardDeleteUi =
        canHardDelete && (capabilitiesQuery.data?.allowHardDelete ?? false);

    const registersQuery = useQuery({
        queryKey: getGetApiCashRegisterQueryKey(),
        queryFn: () => getApiCashRegister(),
        enabled: canView,
    });

    const allRegisters = useMemo(
        () => normalizeCashRegisterList(registersQuery.data),
        [registersQuery.data],
    );

    const visibleRegisters = useMemo(() => {
        if (showDecommissioned) return allRegisters;
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

    const invalidateRegisters = useCallback(async () => {
        await queryClient.invalidateQueries({ queryKey: getGetApiCashRegisterQueryKey() });
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

    const submitDecommission = useCallback(() => {
        const reg = decommissionRegister;
        const id = reg?.id?.trim();
        if (!id) return;
        if (!canDecommissionRegister(rawRegisterStatus(reg!))) {
            message.error(t('cashRegisters.decommission.mustCloseFirst'));
            return;
        }
        decommissionMutation.mutate({
            id,
            reason: decommissionReason.trim() || 'Kassenverwaltung Stilllegung',
        });
    }, [decommissionRegister, decommissionReason, decommissionMutation, t]);

    const submitHardDelete = useCallback(() => {
        const id = hardDeleteRegister?.id?.trim();
        if (!id) return;
        hardDeleteMutation.mutate({ id, confirmPhrase: hardDeleteConfirm.trim() });
    }, [hardDeleteRegister, hardDeleteConfirm, hardDeleteMutation]);

    if (!canView) {
        return (
            <AdminPageShell>
                <Alert type="warning" showIcon message={t('errors.forbidden.FORBIDDEN')} />
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

            <Card>
                <Space style={{ marginBottom: 16 }} wrap align="center">
                    <Button
                        icon={<ReloadOutlined />}
                        onClick={() => registersQuery.refetch()}
                        loading={registersQuery.isFetching}
                    >
                        {t('cashRegisters.actions.refresh')}
                    </Button>
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
                    {canDecommission ? (
                        <Link href="/rksv/sonderbelege?focus=schlussbeleg">
                            {t('cashRegisters.decommission.hintSchlussbelegLink')}
                        </Link>
                    ) : null}
                </Space>

                {registersQuery.isError ? (
                    <Alert
                        type="error"
                        showIcon
                        message={t('cashRegisters.errors.loadFailed')}
                        style={{ marginBottom: 16 }}
                    />
                ) : null}

                <CashRegisterTable
                    registers={visibleRegisters}
                    loading={registersQuery.isLoading}
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
