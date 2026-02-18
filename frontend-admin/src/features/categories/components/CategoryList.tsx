import React from 'react';
import { Table, Space, Button, Popconfirm, Tag } from 'antd';
import { EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { Category } from '@/api/generated/model';

interface CategoryListProps {
    data: Category[];
    loading: boolean;
    onEdit: (category: Category) => void;
    onDelete: (id: string) => void;
}

export default function CategoryList({ data, loading, onEdit, onDelete }: CategoryListProps) {
    const columns = [
        {
            title: 'Name',
            dataIndex: 'name',
            key: 'name',
            render: (text: string, record: Category) => (
                <Space>
                    {record.icon && <span>{record.icon}</span>}
                    <span style={{ fontWeight: 500 }}>{text}</span>
                </Space>
            ),
        },
        {
            title: 'Color',
            dataIndex: 'color',
            key: 'color',
            render: (color: string) => (
                color ? <Tag color={color}>{color}</Tag> : '-'
            ),
        },
        {
            title: 'Description',
            dataIndex: 'description',
            key: 'description',
        },
        {
            title: 'Sort Order',
            dataIndex: 'sortOrder',
            key: 'sortOrder',
        },
        {
            title: 'Status',
            dataIndex: 'isActive',
            key: 'isActive',
            render: (isActive: boolean) => (
                <Tag color={isActive ? 'green' : 'red'}>
                    {isActive ? 'Active' : 'Inactive'}
                </Tag>
            ),
        },
        {
            title: 'Actions',
            key: 'actions',
            render: (_: any, record: Category) => (
                <Space>
                    <Button
                        icon={<EditOutlined />}
                        onClick={() => onEdit(record)}
                    />
                    <Popconfirm
                        title="Delete category?"
                        description="Are you sure you want to delete this category?"
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
            pagination={false}
        />
    );
}
