'use client';

import React from 'react';
import { Table, Button, Space, Tag, Popconfirm } from 'antd';
import { EditOutlined, DeleteOutlined, EyeOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { ReceiptTemplate } from '@/api/generated/model';
import Link from 'next/link';
import dayjs from 'dayjs';

interface ReceiptTemplatesTableProps {
    data: ReceiptTemplate[];
    loading: boolean;
    onDelete: (id: string) => void;
    onPreview: (id: string) => void;
}

export default function ReceiptTemplatesTable({
    data,
    loading,
    onDelete,
    onPreview,
}: ReceiptTemplatesTableProps) {
    const columns: ColumnsType<ReceiptTemplate> = [
        {
            title: 'Name',
            dataIndex: 'templateName',
            key: 'templateName',
            render: (text: string) => <span style={{ fontWeight: 600 }}>{text}</span>,
        },
        {
            title: 'Language',
            dataIndex: 'language',
            key: 'language',
            render: (text: string) => <Tag>{text.toUpperCase()}</Tag>,
        },
        {
            title: 'Type',
            dataIndex: 'templateType',
            key: 'templateType',
            render: (text: string) => <Tag color="blue">{text}</Tag>,
        },
        {
            title: 'Default',
            dataIndex: 'isDefault',
            key: 'isDefault',
            render: (val: boolean) => (val ? <Tag color="green">Yes</Tag> : '—'),
        },
        {
            title: 'Active',
            dataIndex: 'isActive',
            key: 'isActive',
            render: (val: boolean) => (val ? <Tag color="green">Active</Tag> : <Tag>Inactive</Tag>),
        },
        {
            title: 'Created',
            dataIndex: 'createdAt',
            key: 'createdAt',
            render: (date: string) => dayjs(date).format('DD.MM.YYYY HH:mm'),
        },
        {
            title: 'Actions',
            key: 'actions',
            width: 180,
            render: (_: unknown, record: ReceiptTemplate) => (
                <Space>
                    <Link href={`/receipt-templates/${record.id}`}>
                        <Button size="small" icon={<EditOutlined />}>
                            Edit
                        </Button>
                    </Link>
                    <Button
                        size="small"
                        icon={<EyeOutlined />}
                        onClick={() => onPreview(record.id!)}
                    >
                        Preview
                    </Button>
                    <Popconfirm
                        title="Delete this template?"
                        onConfirm={() => onDelete(record.id!)}
                        okText="Yes"
                        cancelText="No"
                    >
                        <Button size="small" danger icon={<DeleteOutlined />} />
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <Table<ReceiptTemplate>
            columns={columns}
            dataSource={data}
            rowKey="id"
            loading={loading}
            pagination={false}
        />
    );
}
