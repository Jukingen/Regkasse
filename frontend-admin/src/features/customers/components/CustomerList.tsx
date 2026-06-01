import React from 'react';
import { Table, Space, Button, Popconfirm, Tag, Tooltip } from 'antd';
import { EditOutlined, DeleteOutlined, GiftOutlined } from '@ant-design/icons';
import { Customer } from '@/api/generated/model';
import type { BenefitAssignment } from '@/api/admin/benefit-assignments';
import { useI18n } from '@/i18n';

interface CustomerListProps {
    data: Customer[];
    loading: boolean;
    onEdit: (customer: Customer) => void;
    onDelete: (id: string) => void;
    /** Navigate to benefit-assignments filtered for this customer. */
    onManageBenefits?: (customer: Customer) => void;
    /** Optional: show benefit summary per customer. Fail-safe when undefined or empty. */
    benefitAssignments?: BenefitAssignment[] | null;
}

/** Active assignments for a customer; safe labels from benefit definition. */
function getBenefitSummaryForCustomer(
    customerId: string | undefined,
    assignments: BenefitAssignment[] | undefined | null,
    benefitFallback: string,
): { label: string; code?: string }[] {
    if (!customerId || !Array.isArray(assignments)) return [];
    return assignments
        .filter((a) => a.customerId === customerId && a.isActive)
        .map((a) => ({
            label: a.benefitDefinition?.name ?? a.benefitDefinition?.code ?? benefitFallback,
            code: a.benefitDefinition?.code,
        }))
        .filter((x) => x.label);
}

export default function CustomerList({ data, loading, onEdit, onDelete, onManageBenefits, benefitAssignments }: CustomerListProps) {
    const { t } = useI18n();
    const dataSource = Array.isArray(data) ? data : [];
    const columns = [
        {
            title: t('customers.list.columnName'),
            dataIndex: 'name',
            key: 'name',
            render: (text: string) => <span style={{ fontWeight: 500 }}>{text}</span>,
        },
        {
            title: t('customers.list.columnContact'),
            key: 'contact',
            render: (_: any, record: Customer) => (
                <Space orientation="vertical" size={0}>
                    {record.email && <span>{record.email}</span>}
                    {record.phone && <span style={{ fontSize: 12, color: '#888' }}>{record.phone}</span>}
                </Space>
            ),
        },
        {
            title: t('customers.list.columnPoints'),
            dataIndex: 'loyaltyPoints',
            key: 'loyaltyPoints',
            render: (points: number) => points || 0,
        },
        {
            title: t('customers.list.columnTotalSpent'),
            dataIndex: 'totalSpent',
            key: 'totalSpent',
            render: (val: number) => `€${(val || 0).toFixed(2)}`,
        },
        {
            title: t('customers.list.columnVisits'),
            dataIndex: 'visitCount',
            key: 'visitCount',
            render: (val: number) => val || 0,
        },
        {
            title: t('customers.list.columnBenefits'),
            key: 'benefits',
            render: (_: unknown, record: Customer) => {
                const items = getBenefitSummaryForCustomer(record.id, benefitAssignments, t('customers.list.benefitFallback'));
                if (items.length === 0) return <span style={{ color: '#999' }}>—</span>;
                const content = (
                    <Space size={4} wrap>
                        {items.slice(0, 3).map((item, i) => (
                            <Tag key={i} icon={<GiftOutlined />}>
                                {item.label}
                            </Tag>
                        ))}
                        {items.length > 3 && <Tag>+{items.length - 3}</Tag>}
                    </Space>
                );
                return items.length > 2 ? (
                    <Tooltip title={items.map((x) => x.label).join(', ')}>{content}</Tooltip>
                ) : (
                    content
                );
            },
        },
        {
            title: t('customers.list.columnStatus'),
            key: 'status',
            render: (_: any, record: Customer) => (
                <Space>
                    {record.isActive ? <Tag color="green">{t('customers.list.statusActive')}</Tag> : <Tag color="red">{t('customers.list.statusInactive')}</Tag>}
                    {record.isVip && <Tag color="gold">{t('customers.list.tagVip')}</Tag>}
                </Space>
            )
        },
        {
            title: t('customers.list.columnActions'),
            key: 'actions',
            render: (_: any, record: Customer) => (
                <Space>
                    <Button
                        icon={<EditOutlined />}
                        onClick={() => onEdit(record)}
                    />
                    {onManageBenefits && (
                        <Tooltip title={t('customers.list.manageBenefits')}>
                            <Button
                                icon={<GiftOutlined />}
                                onClick={() => onManageBenefits(record)}
                            />
                        </Tooltip>
                    )}
                    <Popconfirm
                        title={t('customers.list.deleteConfirmTitle')}
                        description={t('customers.list.deleteConfirmDescription')}
                        onConfirm={() => record.id && onDelete(record.id)}
                        okText={t('common.buttons.yes')}
                        cancelText={t('common.buttons.no')}
                    >
                        <Button danger icon={<DeleteOutlined />} />
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <Table
            dataSource={dataSource}
            columns={columns}
            rowKey="id"
            loading={loading}
            pagination={{
                pageSize: 10,
                showSizeChanger: true,
            }}
        />
    );
}
