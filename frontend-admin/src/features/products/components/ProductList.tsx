import React from 'react';
import { Table, Space, Button, Popconfirm, Tag } from 'antd';
import { EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { Product } from '@/api/generated/model';
import { formatProductUnitLabelForLocale } from '@/features/products/utils/productMapper';
import { useProductFilters } from '../hooks/useProducts';
import { useI18n } from '@/i18n';

interface ProductListProps {
    data: Product[];
    loading: boolean;
    onEdit: (product: Product) => void;
    onDelete: (id: string) => void;
}

export default function ProductList({ data, loading, onEdit, onDelete }: ProductListProps) {
    const { t } = useI18n();
    const { setParam } = useProductFilters();

    const columns = [
        {
            title: t('products.table.product'),
            dataIndex: 'name',
            key: 'name',
            render: (text: string, record: Product) => (
                <Space orientation="vertical" size={0}>
                    <span style={{ fontWeight: 500 }}>{text}</span>
                    {record.barcode && <span style={{ fontSize: 12, color: '#888' }}>{record.barcode}</span>}
                </Space>
            ),
        },
        {
            title: t('products.table.price'),
            dataIndex: 'price',
            key: 'price',
            render: (price: number) => `€${price.toFixed(2)}`,
        },
        {
            title: t('products.table.stock'),
            dataIndex: 'stockQuantity',
            key: 'stockQuantity',
            render: (stock: number, record: Product) => {
                const minStock = record.minStockLevel || 0;
                const isLow = stock <= minStock;
                return (
                    <Tag color={isLow ? 'red' : 'green'}>
                        {stock} {formatProductUnitLabelForLocale(record.unit, t)}
                    </Tag>
                );
            }
        },
        {
            title: t('products.table.category'),
            dataIndex: 'category',
            key: 'category',
        },
        {
            title: t('products.table.tax'),
            dataIndex: 'taxRate',
            key: 'taxRate',
            render: (rate: number) => `${rate}%`,
        },
        {
            title: t('products.table.actions'),
            key: 'actions',
            render: (_: any, record: Product) => (
                <Space>
                    <Button
                        icon={<EditOutlined />}
                        onClick={() => onEdit(record)}
                    />
                    <Popconfirm
                        title={t('products.actions.deleteConfirmTitle')}
                        description={t('products.actions.deleteConfirmDescription')}
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
