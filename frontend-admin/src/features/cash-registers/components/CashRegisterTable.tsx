'use client';

import type { Key } from 'react';
import {
    CloudSyncOutlined,
    EnvironmentOutlined,
    EyeOutlined,
    FileProtectOutlined,
    MinusCircleOutlined,
    SafetyOutlined,
    ShopOutlined,
    UserOutlined,
} from '@ant-design/icons';
import { Button, Empty, Space, Table, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { CashRegister } from '@/api/generated/model';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';
import { CashRegisterActions, type CashRegisterActionKey } from '@/features/cash-registers/components/CashRegisterActions';
import { CashRegisterStatusBadge } from '@/features/cash-registers/components/CashRegisterStatusBadge';
import { TseHealthBadge } from '@/features/cash-registers/components/TseHealthBadge';
import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import { formatRelativeTime } from '@/features/cash-registers/utils/formatRelativeTime';
import {
    isDecommissionedRegister,
    rawRegisterStatus,
} from '@/features/cash-registers/utils/registerStatus';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { usePermissions } from '@/hooks/usePermissions';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';
import { AppPermissions } from '@/shared/auth/permissions';
import styles from './CashRegisterTable.module.css';

function asEnhanced(record: CashRegister): EnhancedCashRegister {
    return record as EnhancedCashRegister;
}

function resolveCashierName(record: EnhancedCashRegister): string | null {
    const fromApi = record.currentCashierName?.trim();
    if (fromApi) {
        return fromApi;
    }
    return record.currentUser?.userName?.trim() || record.currentUserId?.trim() || null;
}

export type CashRegisterTableProps = {
    registers: CashRegister[];
    loading?: boolean;
    canCreate?: boolean;
    /** @deprecated Column visibility uses `cash_register.manage` from JWT via `usePermissions`. */
    canManage?: boolean;
    /** Total registers before visibility filter (decommissioned hidden). */
    totalRegisterCount?: number;
    canDecommission: boolean;
    /** @deprecated Use localized labels inside CashRegisterStatusBadge. */
    statusLabel?: (status: number | undefined) => string;
    rowClassName?: (record: CashRegister) => string;
    selectedRowKeys?: Key[];
    onSelectionChange?: (keys: Key[], rows: CashRegister[]) => void;
    onEdit: (register: CashRegister) => void;
    onDecommission: (register: CashRegister) => void;
    onRegisterAction?: (key: CashRegisterActionKey, register: CashRegister) => void;
};

function isFiniteNumber(value: unknown): value is number {
    return typeof value === 'number' && Number.isFinite(value);
}

export function CashRegisterTable({
    registers,
    loading,
    canCreate = false,
    totalRegisterCount = 0,
    canDecommission: _canDecommission,
    rowClassName,
    selectedRowKeys,
    onSelectionChange,
    onEdit,
    onDecommission: _onDecommission,
    onRegisterAction,
}: CashRegisterTableProps) {
    const { t, formatLocale } = useI18n();
    const { hasPermission, isSuperAdmin } = usePermissions();
    const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);
    const canManageRegisters = hasPermission(AppPermissions.CashRegisterManage);

    const emptyDescription =
        totalRegisterCount === 0
            ? canCreate
                ? t('cashRegisters.emptyCanCreate')
                : t('cashRegisters.emptyContactAdmin')
            : t('cashRegisters.empty');

    const showBalanceColumn = registers.some(
        (record) => isFiniteNumber(record.currentBalance) || isFiniteNumber(record.startingBalance),
    );

    const columns: ColumnsType<CashRegister> = [
        {
            title: t('cashRegisters.columns.name'),
            key: 'name',
            width: 320,
            render: (_: unknown, record) => (
                <div className={styles.registerCell}>
                    <span className={styles.registerIcon} aria-hidden>
                        <ShopOutlined />
                    </span>
                    <div className={styles.registerContent}>
                        <div className={styles.registerHeading}>
                            <Typography.Text strong className={styles.registerNumber}>
                                {record.registerNumber?.trim() || FORMAT_EMPTY_DISPLAY}
                            </Typography.Text>
                            {record.isActive === false ? <Tag>{t('common.categories.table.inactive')}</Tag> : null}
                        </div>
                        <Typography.Text className={styles.registerLocation}>
                            <EnvironmentOutlined />
                            {record.location?.trim() || FORMAT_EMPTY_DISPLAY}
                        </Typography.Text>
                    </div>
                </div>
            ),
        },
        {
            title: t('cashRegisters.columns.status'),
            key: 'status',
            width: 180,
            render: (_: unknown, record) => {
                return (
                    <div className={styles.statusCell}>
                        <CashRegisterStatusBadge register={record} useIcon />
                        <Typography.Text className={styles.cellSubtle}>
                            {record.isActive === false
                                ? t('common.categories.table.inactive')
                                : t('common.categories.table.active')}
                        </Typography.Text>
                    </div>
                );
            },
        },
        {
            title: t('cashRegisters.columns.operations'),
            key: 'operations',
            width: 200,
            render: (_: unknown, record) => {
                const enhanced = asEnhanced(record);
                const offlineCount = enhanced.offlineQueueCount ?? 0;

                return (
                    <Space orientation="vertical" size={4}>
                        <TseHealthBadge
                            status={enhanced.tseHealthStatus}
                            registerId={record.id}
                            offlineQueueCount={offlineCount}
                        />
                        {offlineCount > 0 ? (
                            <Tag color="orange">
                                {t('cashRegisters.offlineQueue.label', { count: offlineCount })}
                            </Tag>
                        ) : null}
                        <Typography.Text className={styles.cellSubtle}>
                            <UserOutlined />{' '}
                            {resolveCashierName(enhanced) ?? FORMAT_EMPTY_DISPLAY}
                        </Typography.Text>
                    </Space>
                );
            },
        },
        {
            title: t('cashRegisters.columns.compliance'),
            key: 'compliance',
            width: 200,
            render: (_: unknown, record) => {
                const enhanced = asEnhanced(record);
                return (
                    <Space orientation="vertical" size={0}>
                        <Typography.Text className={styles.cellSubtle}>
                            {t('cashRegisters.detail.lastMonatsbelegUtc')}:{' '}
                            {enhanced.lastMonatsbelegUtc
                                ? formatDateTime(enhanced.lastMonatsbelegUtc, formatLocale)
                                : FORMAT_EMPTY_DISPLAY}
                        </Typography.Text>
                        <Typography.Text className={styles.cellSubtle}>
                            {t('cashRegisters.detail.lastJahresbelegUtc')}:{' '}
                            {enhanced.lastJahresbelegUtc
                                ? formatDateTime(enhanced.lastJahresbelegUtc, formatLocale)
                                : FORMAT_EMPTY_DISPLAY}
                        </Typography.Text>
                    </Space>
                );
            },
        },
        {
            title: t('cashRegisters.columns.activity'),
            key: 'activity',
            width: 220,
            render: (_: unknown, record) => {
                const enhanced = asEnhanced(record);
                const status = rawRegisterStatus(record);
                const activityAt = isDecommissionedRegister(status)
                    ? record.decommissionedAtUtc
                    : record.lastBalanceUpdate;
                const activityLabel = isDecommissionedRegister(status)
                    ? t('cashRegisters.detail.decommissionedAt')
                    : t('cashRegisters.detail.lastBalanceUpdate');

                return (
                    <Space orientation="vertical" size={0}>
                        <Typography.Text className={styles.cellValue}>
                            {formatRelativeTime(activityAt, formatLocale)}
                        </Typography.Text>
                        <Typography.Text className={styles.cellSubtle}>
                            {formatDateTime(activityAt, formatLocale)}
                        </Typography.Text>
                        <Typography.Text className={styles.cellSubtle}>{activityLabel}</Typography.Text>
                        <Typography.Text className={styles.cellSubtle}>
                            {t('cashRegisters.detail.lastSyncAtUtc')}:{' '}
                            {enhanced.lastSyncAtUtc
                                ? formatRelativeTime(enhanced.lastSyncAtUtc, formatLocale)
                                : FORMAT_EMPTY_DISPLAY}
                        </Typography.Text>
                    </Space>
                );
            },
        },
        ...(showBalanceColumn
            ? [
                  {
                      title: t('cashRegisters.columns.balance'),
                      key: 'balance',
                      width: 220,
                      render: (_: unknown, record: CashRegister) => (
                          <Space orientation="vertical" size={0}>
                              <Typography.Text className={styles.cellValue}>
                                  {isFiniteNumber(record.currentBalance)
                                      ? formatCurrency(record.currentBalance, formatLocale)
                                      : FORMAT_EMPTY_DISPLAY}
                              </Typography.Text>
                              <Typography.Text className={styles.cellSubtle}>
                                  {t('cashRegisters.detail.startingBalance')}:{' '}
                                  {isFiniteNumber(record.startingBalance)
                                      ? formatCurrency(record.startingBalance, formatLocale)
                                      : FORMAT_EMPTY_DISPLAY}
                              </Typography.Text>
                          </Space>
                      ),
                  } satisfies ColumnsType<CashRegister>[number],
              ]
            : []),
    ];

    if (canManageRegisters && onRegisterAction) {
        columns.push({
            title: t('cashRegisters.columns.actions'),
            key: 'actions',
            width: 220,
            fixed: 'right',
            render: (_: unknown, record: CashRegister) => {
                const enhanced = asEnhanced(record);
                const status = rawRegisterStatus(record);
                const decommissioned = isDecommissionedRegister(status);
                const registerId = record.id?.trim();
                const offlineHref = registerId
                    ? `/admin/tse/offline-transactions?cashRegisterId=${encodeURIComponent(registerId)}`
                    : '/admin/tse/offline-transactions';

                return (
                    <div className={styles.actions}>
                        <CashRegisterActions
                            register={record}
                            canOperate={canManageRegisters}
                            onAction={onRegisterAction}
                        />
                        <Tooltip title={t('cashRegisters.actions.view')}>
                            <Button
                                size="small"
                                icon={<EyeOutlined />}
                                aria-label={t('cashRegisters.actions.view')}
                                onClick={() => onEdit(record)}
                            />
                        </Tooltip>
                        <Tooltip title={t('cashRegisters.actions.tseHealth')}>
                            <Button
                                size="small"
                                icon={<SafetyOutlined />}
                                aria-label={t('cashRegisters.actions.tseHealth')}
                                href="/rksv/status"
                            />
                        </Tooltip>
                        {(enhanced.offlineQueueCount ?? 0) > 0 ? (
                            <Tooltip
                                title={t('cashRegisters.offlineQueue.tooltip', {
                                    count: enhanced.offlineQueueCount ?? 0,
                                })}
                            >
                                <Button
                                    size="small"
                                    icon={<CloudSyncOutlined />}
                                    aria-label={t('cashRegisters.actions.offlineQueue')}
                                    href={offlineHref}
                                />
                            </Tooltip>
                        ) : null}
                        {canOpenSonderbelege ? (
                            <Tooltip title={t('cashRegisters.actions.specialReceipts')}>
                                <Button
                                    size="small"
                                    icon={<FileProtectOutlined />}
                                    aria-label={t('cashRegisters.actions.specialReceipts')}
                                    href="/rksv/sonderbelege?focus=schlussbeleg"
                                />
                            </Tooltip>
                        ) : null}
                        {isSuperAdmin && decommissioned ? (
                            <Tooltip title={t('cashRegisters.decommission.restoreTooltip')}>
                                <Button
                                    size="small"
                                    icon={<MinusCircleOutlined />}
                                    aria-label={t('cashRegisters.actions.restore')}
                                    disabled
                                />
                            </Tooltip>
                        ) : null}
                    </div>
                );
            },
        });
    }

    return (
        <Table<CashRegister>
            rowKey={(r) => r.id ?? r.registerNumber}
            loading={loading}
            columns={columns}
            dataSource={registers}
            rowClassName={rowClassName}
            rowSelection={
                onSelectionChange
                    ? {
                          selectedRowKeys,
                          onChange: (keys, rows) => onSelectionChange(keys, rows),
                      }
                    : undefined
            }
            pagination={{ pageSize: 20, showSizeChanger: true }}
            scroll={{ x: showBalanceColumn || canManageRegisters ? 1400 : 1200 }}
            locale={{
                emptyText: <Empty description={emptyDescription} />,
            }}
        />
    );
}
