import React from 'react';
import { Table, Space, Button, Popconfirm, Tag } from 'antd';
import { EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { Customer } from '@/api/generated/model';

interface CustomerListProps {
    data: Customer[];
    loading: boolean;
    onEdit: (customer: Customer) => void;
    onDelete: (id: string) => void;
}

export default function CustomerList({ data, loading, onEdit, onDelete }: CustomerListProps) {
    const columns = [
        {
            title: 'Name',
            dataIndex: 'name',
            key: 'name',
            render: (text: string) => <span style={{ fontWeight: 500 }}>{text}</span>,
        },
        {
            title: 'Contact',
            key: 'contact',
            render: (_: any, record: Customer) => (
                <Space direction="vertical" size={0}>
                    {record.email && <span>{record.email}</span>}
                    {record.phone && <span style={{ fontSize: 12, color: '#888' }}>{record.phone}</span>}
                </Space>
            ),
        },
        {
            title: 'Points',
            dataIndex: 'loyaltyPoints',
            key: 'loyaltyPoints',
            render: (points: number) => points || 0,
        },
        {
            title: 'Total Spent',
            dataIndex: 'totalSpent',
            key: 'totalSpent',
            render: (val: number) => `€${(val || 0).toFixed(2)}`,
        },
        {
            title: 'Visits',
            dataIndex: 'visitCount',
            key: 'visitCount',
            render: (val: number) => val || 0,
        },
        {
            title: 'Status',
            key: 'status',
            render: (_: any, record: Customer) => (
                <Space>
                    {record.isActive ? <Tag color="green">Active</Tag> : <Tag color="red">Inactive</Tag>}
                    {record.isVip && <Tag color="gold">VIP</Tag>}
                </Space>
            )
        },
        {
            title: 'Actions',
            key: 'actions',
            render: (_: any, record: Customer) => (
                <Space>
                    <Button
                        icon={<EditOutlined />}
                        onClick={() => onEdit(record)}
                    />
                    <Popconfirm
                        title="Delete customer?"
                        description="Are you sure you want to delete this customer?"
                        onConfirm={() => record.id && onDelete(record.id)}
                        okText="Yes"
                        cancelText="No"
                    >
                        <Button danger icon={<DeleteOutlined />} />
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <Table
            dataSource={data}
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
