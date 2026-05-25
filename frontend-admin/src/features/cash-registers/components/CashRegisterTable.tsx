'use client';

import {
    CheckCircleOutlined,
    EnvironmentOutlined,
    EyeOutlined,
    LockOutlined,
    MinusCircleOutlined,
    StopOutlined,
    ToolOutlined,
    ShopOutlined,
} from '@ant-design/icons';
import { Button, Empty, Space, Table, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { CashRegister } from '@/api/generated/model';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    rawRegisterStatus,
    registerStatusTagColor,
} from '@/features/cash-registers/utils/registerStatus';
import styles from './CashRegisterTable.module.css';

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
    onEdit: (register: CashRegister) => void;
    onDecommission: (register: CashRegister) => void;
};

function isFiniteNumber(value: unknown): value is number {
    return typeof value === 'number' && Number.isFinite(value);
}

function formatRelativeTime(input: string | null | undefined, formatLocale: string): string {
    if (!input) {
        return FORMAT_EMPTY_DISPLAY;
    }

    const date = new Date(input);
    if (Number.isNaN(date.getTime())) {
        return FORMAT_EMPTY_DISPLAY;
    }

    const diffMs = date.getTime() - Date.now();
    const diffSeconds = Math.round(diffMs / 1000);
    const absSeconds = Math.abs(diffSeconds);
    const rtf = new Intl.RelativeTimeFormat(formatLocale, { numeric: 'auto' });

    if (absSeconds < 60) {
        return rtf.format(diffSeconds, 'second');
    }

    const diffMinutes = Math.round(diffSeconds / 60);
    if (Math.abs(diffMinutes) < 60) {
        return rtf.format(diffMinutes, 'minute');
    }

    const diffHours = Math.round(diffMinutes / 60);
    if (Math.abs(diffHours) < 24) {
        return rtf.format(diffHours, 'hour');
    }

    const diffDays = Math.round(diffHours / 24);
    if (Math.abs(diffDays) < 30) {
        return rtf.format(diffDays, 'day');
    }

    const diffMonths = Math.round(diffDays / 30);
    if (Math.abs(diffMonths) < 12) {
        return rtf.format(diffMonths, 'month');
    }

    const diffYears = Math.round(diffDays / 365);
    return rtf.format(diffYears, 'year');
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
            title: t('cashRegisters.columns.activity'),
            key: 'activity',
            width: 220,
            render: (_: unknown, record) => {
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
                      width: 180,
                      fixed: 'right',
                      render: (_: unknown, record: CashRegister) => {
                          const status = rawRegisterStatus(record);
                          const decommissioned = isDecommissionedRegister(status);
                          const canStilllegen =
                              canDecommission &&
                              !decommissioned &&
                              canDecommissionRegister(status);

                          return (
                              <div className={styles.actions}>
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
            pagination={{ pageSize: 20, showSizeChanger: true }}
            scroll={{ x: showBalanceColumn ? 1100 : 900 }}
            locale={{
                emptyText: <Empty description={emptyDescription} />,
            }}
        />
    );
}
