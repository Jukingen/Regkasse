import { DeleteOutlined, EditOutlined } from '@ant-design/icons';
import { Button, Popconfirm, Space, Table, Tag } from 'antd';
import React from 'react';

import { Category } from '@/api/generated/model';
import { adminTablePaginationDefaults } from '@/components/ui/adminTablePagination';
import { useI18n } from '@/i18n';

interface CategoryListProps {
  data: Category[];
  loading: boolean;
  onEdit: (category: Category) => void;
  onDelete: (id: string) => void;
}

export default function CategoryList({ data, loading, onEdit, onDelete }: CategoryListProps) {
  const { t } = useI18n();
  const columns = [
    {
      title: t('common.categories.table.name'),
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
      title: t('common.categories.table.color'),
      dataIndex: 'color',
      key: 'color',
      render: (color: string) => (color ? <Tag color={color}>{color}</Tag> : '-'),
    },
    {
      title: t('common.categories.table.description'),
      dataIndex: 'description',
      key: 'description',
    },
    {
      title: t('common.categories.table.sortOrder'),
      dataIndex: 'sortOrder',
      key: 'sortOrder',
    },
    {
      title: t('common.categories.table.status'),
      dataIndex: 'isActive',
      key: 'isActive',
      render: (isActive: boolean) => (
        <Tag color={isActive ? 'green' : 'red'}>
          {isActive ? t('common.categories.table.active') : t('common.categories.table.inactive')}
        </Tag>
      ),
    },
    {
      title: t('common.categories.table.actions'),
      key: 'actions',
      render: (_: any, record: Category) => (
        <Space>
          <Button icon={<EditOutlined />} onClick={() => onEdit(record)} />
          <Popconfirm
            title={t('common.categories.deleteConfirmTitle')}
            description={t('common.categories.list.deleteConfirmDescription')}
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
      pagination={{ ...adminTablePaginationDefaults, pageSize: 10 }}
    />
  );
}
