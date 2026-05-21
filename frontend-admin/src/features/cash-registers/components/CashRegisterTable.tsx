'use client';

import { Button, Empty, Space, Table, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, LockOutlined } from '@ant-design/icons';
import type { CashRegister } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    rawRegisterStatus,
    registerStatusEmoji,
    registerStatusTagColor,
} from '@/features/cash-registers/utils/registerStatus';

export type CashRegisterTableProps = {
    registers: CashRegister[];
    loading?: boolean;
    canDecommission: boolean;
    statusLabel: (status: number | undefined) => string;
    rowClassName?: (record: CashRegister) => string;
    onEdit: (register: CashRegister) => void;
    onDecommission: (register: CashRegister) => void;
};

export function CashRegisterTable({
    registers,
    loading,
    canDecommission,
    statusLabel,
    rowClassName,
    onEdit,
    onDecommission,
}: CashRegisterTableProps) {
    const { t } = useI18n();

    const columns: ColumnsType<CashRegister> = [
        {
            title: t('cashRegisters.columns.name'),
            key: 'name',
            render: (_: unknown, record) => (
                <Space direction="vertical" size={0}>
                    <Typography.Text strong>{record.location?.trim() || '—'}</Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {record.registerNumber?.trim() || '—'}
                    </Typography.Text>
                </Space>
            ),
        },
        {
            title: t('cashRegisters.columns.status'),
            key: 'status',
            width: 200,
            render: (_: unknown, record) => {
                const status = rawRegisterStatus(record);
                return (
                    <Tag color={registerStatusTagColor(status)}>
                        {registerStatusEmoji(status)} {statusLabel(status)}
                    </Tag>
                );
            },
        },
        {
            title: t('cashRegisters.columns.actions'),
            key: 'actions',
            width: 300,
            render: (_: unknown, record) => {
                const status = rawRegisterStatus(record);
                const decommissioned = isDecommissionedRegister(status);
                const canStilllegen =
                    canDecommission && !decommissioned && canDecommissionRegister(status);

                return (
                    <Space wrap size="small">
                        <Button size="small" icon={<EditOutlined />} onClick={() => onEdit(record)}>
                            {t('cashRegisters.actions.edit')}
                        </Button>
                        {decommissioned ? (
                            <Tooltip title={t('cashRegisters.decommission.restoreTooltip')}>
                                <Button size="small" disabled>
                                    {t('cashRegisters.actions.restore')}
                                </Button>
                            </Tooltip>
                        ) : (
                            <Tooltip
                                title={
                                    !canDecommission
                                        ? undefined
                                        : !canDecommissionRegister(status)
                                          ? t('cashRegisters.decommission.mustCloseFirst')
                                          : undefined
                                }
                            >
                                <Button
                                    size="small"
                                    icon={<LockOutlined />}
                                    danger
                                    disabled={!canStilllegen}
                                    onClick={() => onDecommission(record)}
                                >
                                    {t('cashRegisters.actions.decommission')}
                                </Button>
                            </Tooltip>
                        )}
                    </Space>
                );
            },
        },
    ];

    return (
        <Table<CashRegister>
            rowKey={(r) => r.id ?? r.registerNumber}
            loading={loading}
            columns={columns}
            dataSource={registers}
            rowClassName={rowClassName}
            pagination={{ pageSize: 20, showSizeChanger: true }}
            locale={{
                emptyText: <Empty description={t('cashRegisters.empty')} />,
            }}
        />
    );
}
