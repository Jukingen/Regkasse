import React from 'react';
import { Table, Space, Button, Popconfirm, Tag } from 'antd';
import { EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { Product } from '@/api/generated/model';
import { useProductFilters } from '../hooks/useProducts';

interface ProductListProps {
    data: Product[];
    loading: boolean;
    onEdit: (product: Product) => void;
    onDelete: (id: string) => void;
}

export default function ProductList({ data, loading, onEdit, onDelete }: ProductListProps) {
    const { setParam } = useProductFilters();

    const columns = [
        {
            title: 'Name',
            dataIndex: 'name',
            key: 'name',
            render: (text: string, record: Product) => (
                <Space direction="vertical" size={0}>
                    <span style={{ fontWeight: 500 }}>{text}</span>
                    {record.barcode && <span style={{ fontSize: 12, color: '#888' }}>{record.barcode}</span>}
                </Space>
            ),
        },
        {
            title: 'Price',
            dataIndex: 'price',
            key: 'price',
            render: (price: number) => `€${price.toFixed(2)}`,
        },
        {
            title: 'Stock',
            dataIndex: 'stockQuantity',
            key: 'stockQuantity',
            render: (stock: number, record: Product) => {
                const minStock = record.minStockLevel || 0;
                const isLow = stock <= minStock;
                return (
                    <Tag color={isLow ? 'red' : 'green'}>
                        {stock} {record.unit || 'pcs'}
                    </Tag>
                );
            }
        },
        {
            title: 'Category',
            dataIndex: 'category',
            key: 'category',
        },
        {
            title: 'Tax',
            dataIndex: 'taxRate',
            key: 'taxRate',
            render: (rate: number) => `${rate}%`,
        },
        {
            title: 'Actions',
            key: 'actions',
            render: (_: any, record: Product) => (
                <Space>
                    <Button
                        icon={<EditOutlined />}
                        onClick={() => onEdit(record)}
                    />
                    <Popconfirm
                        title="Delete product?"
                        description="Are you sure you want to delete this product?"
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
