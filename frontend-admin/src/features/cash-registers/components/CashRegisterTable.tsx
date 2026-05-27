'use client';

import type { Key } from 'react';
import {
    CheckCircleOutlined,
    CloudSyncOutlined,
    EnvironmentOutlined,
    EyeOutlined,
    FileProtectOutlined,
    LockOutlined,
    MinusCircleOutlined,
    SafetyOutlined,
    StopOutlined,
    ToolOutlined,
    ShopOutlined,
    UserOutlined,
} from '@ant-design/icons';
import { Button, Empty, Space, Table, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { CashRegister } from '@/api/generated/model';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';
import { CashRegisterQuickActions } from '@/features/cash-registers/components/CashRegisterQuickActions';
import { TseHealthBadge } from '@/features/cash-registers/components/TseHealthBadge';
import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import { formatRelativeTime } from '@/features/cash-registers/utils/formatRelativeTime';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    rawRegisterStatus,
    registerStatusTagColor,
} from '@/features/cash-registers/utils/registerStatus';
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
    canManage?: boolean;
    /** Total registers before visibility filter (decommissioned hidden). */
    totalRegisterCount?: number;
    canDecommission: boolean;
    statusLabel: (status: number | undefined) => string;
    rowClassName?: (record: CashRegister) => string;
    selectedRowKeys?: Key[];
    onSelectionChange?: (keys: Key[], rows: CashRegister[]) => void;
    onEdit: (register: CashRegister) => void;
    onDecommission: (register: CashRegister) => void;
};

function isFiniteNumber(value: unknown): value is number {
    return typeof value === 'number' && Number.isFinite(value);
}

function getStatusIcon(status: number | undefined) {
    switch (status) {
        case 2:
            return <CheckCircleOutlined />;
        case 1:
            return <LockOutlined />;
        case 3:
            return <ToolOutlined />;
        case 4:
            return <MinusCircleOutlined />;
        case 5:
            return <StopOutlined />;
        default:
            return <MinusCircleOutlined />;
    }
}

export function CashRegisterTable({
    registers,
    loading,
    canCreate = false,
    canManage = false,
    totalRegisterCount = 0,
    canDecommission,
    statusLabel,
    rowClassName,
    selectedRowKeys,
    onSelectionChange,
    onEdit,
    onDecommission,
}: CashRegisterTableProps) {
    const { t, formatLocale } = useI18n();

    const emptyDescription =
        totalRegisterCount === 0
            ? canCreate
                ? t('cashRegisters.emptyCanCreate')
                : t('cashRegisters.emptyContactAdmin')
            : t('cashRegisters.empty');

    const showActionsColumn = canManage || canDecommission;
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
                const status = rawRegisterStatus(record);
                return (
                    <div className={styles.statusCell}>
                        <Tag color={registerStatusTagColor(status)} icon={getStatusIcon(status)}>
                            {statusLabel(status)}
                        </Tag>
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
                    <Space direction="vertical" size={4}>
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
                    <Space direction="vertical" size={0}>
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
                    <Space direction="vertical" size={0}>
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
                          <Space direction="vertical" size={0}>
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
        ...(showActionsColumn
            ? [
                  {
                      title: t('cashRegisters.columns.actions'),
                      key: 'actions',
                      width: 220,
                      fixed: 'right',
                      render: (_: unknown, record: CashRegister) => {
                          const enhanced = asEnhanced(record);
                          const status = rawRegisterStatus(record);
                          const decommissioned = isDecommissionedRegister(status);
                          const canStilllegen =
                              canDecommission &&
                              !decommissioned &&
                              canDecommissionRegister(status);
                          const registerId = record.id?.trim();
                          const offlineHref = registerId
                              ? `/admin/tse/offline-transactions?cashRegisterId=${encodeURIComponent(registerId)}`
                              : '/admin/tse/offline-transactions';

                          return (
                              <div className={styles.actions}>
                                  <CashRegisterQuickActions
                                      register={record}
                                      canManage={canManage}
                                      canDecommission={canDecommission}
                                      onDecommission={() => onDecommission(record)}
                                  />
                                  {canManage ? (
                                      <Tooltip title={t('cashRegisters.actions.view')}>
                                          <Button
                                              size="small"
                                              icon={<EyeOutlined />}
                                              aria-label={t('cashRegisters.actions.view')}
                                              onClick={() => onEdit(record)}
                                          />
                                      </Tooltip>
                                  ) : null}
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
                                  <Tooltip title={t('cashRegisters.actions.specialReceipts')}>
                                      <Button
                                          size="small"
                                          icon={<FileProtectOutlined />}
                                          aria-label={t('cashRegisters.actions.specialReceipts')}
                                          href="/rksv/sonderbelege?focus=schlussbeleg"
                                      />
                                  </Tooltip>
                                  {decommissioned ? (
                                      <Tooltip title={t('cashRegisters.decommission.restoreTooltip')}>
                                          <Button
                                              size="small"
                                              icon={<MinusCircleOutlined />}
                                              aria-label={t('cashRegisters.actions.restore')}
                                              disabled
                                          />
                                      </Tooltip>
                                  ) : canDecommission ? (
                                      <Tooltip
                                          title={
                                              !canDecommissionRegister(status)
                                                  ? t('cashRegisters.decommission.mustCloseFirst')
                                                  : t('cashRegisters.actions.decommission')
                                          }
                                      >
                                          <Button
                                              size="small"
                                              icon={<StopOutlined />}
                                              aria-label={t('cashRegisters.actions.decommission')}
                                              danger
                                              disabled={!canStilllegen}
                                              onClick={() => onDecommission(record)}
                                          />
                                      </Tooltip>
                                  ) : null}
                              </div>
                          );
                      },
                  } satisfies ColumnsType<CashRegister>[number],
              ]
            : []),
    ];

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
            scroll={{ x: showBalanceColumn ? 1400 : 1200 }}
            locale={{
                emptyText: <Empty description={emptyDescription} />,
            }}
        />
    );
}
