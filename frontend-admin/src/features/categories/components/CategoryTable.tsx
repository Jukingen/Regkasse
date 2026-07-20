'use client';

import React, { useMemo } from 'react';
import { Button, Popconfirm, Space, Table, Tag, Empty } from 'antd';
import type { TableProps } from 'antd';
import { DeleteOutlined, EditOutlined } from '@ant-design/icons';
import type { ColumnType } from 'antd/es/table';
import type { AdminCategory, RksvProductCategoryValue } from '../types';
import EditableCell from './EditableCell';
import { useI18n } from '@/i18n';
import { adminTablePaginationDefaults } from '@/components/ui/adminTablePagination';

interface CategoryTableProps {
  data: AdminCategory[];
  loading: boolean;
  canManage?: boolean;
  onEdit: (category: AdminCategory) => void;
  onDelete: (category: AdminCategory) => void;
  onUpdateName: (category: AdminCategory, newName: string) => Promise<void>;
  deleteLoadingId?: string;
  expandable?: TableProps<AdminCategory>['expandable'];
}

function FiscalCategoryTag({ value, t }: { value: RksvProductCategoryValue | undefined; t: (key: string) => string }) {
  switch (value) {
    case 1:
      return <Tag color="green">{t('common.categories.table.fiscalFood')}</Tag>;
    case 2:
      return <Tag color="blue">{t('common.categories.table.fiscalBeverage')}</Tag>;
    case 3:
      return <Tag color="red">{t('common.categories.table.fiscalAlcohol')}</Tag>;
    case 4:
      return <Tag color="volcano">{t('common.categories.table.fiscalTobacco')}</Tag>;
    default:
      return <Tag>{t('common.categories.table.fiscalOther')}</Tag>;
  }
}

export default function CategoryTable({
  data,
  loading,
  canManage = true,
  onEdit,
  onDelete,
  onUpdateName,
  deleteLoadingId,
  expandable,
}: CategoryTableProps) {
  const { t } = useI18n();

  const columns: ColumnType<AdminCategory>[] = useMemo(
    () => {
      const cols: ColumnType<AdminCategory>[] = [
      {
        title: t('common.categories.table.icon'),
        dataIndex: 'icon',
        key: 'icon',
        width: 80,
        render: (icon: string | null | undefined) => (
          <span style={{ fontSize: 24 }}>{icon?.trim() ? icon : '📁'}</span>
        ),
      },
      {
        title: t('common.categories.table.key'),
        dataIndex: 'key',
        key: 'key',
        width: 160,
        render: (key: string | undefined) => (key ? <Tag color="blue">{key}</Tag> : '—'),
      },
      {
        title: t('common.categories.table.name'),
        dataIndex: 'name',
        key: 'name',
        render: (name: string, record: AdminCategory) => (
          <EditableCell
            value={name}
            onSave={(newName) => onUpdateName(record, newName)}
            disabled={!canManage}
          />
        ),
      },
      {
        title: t('common.categories.table.vatRate'),
        dataIndex: 'defaultTaxRate',
        key: 'defaultTaxRate',
        width: 100,
        align: 'right',
        sorter: (a, b) => (a.defaultTaxRate ?? 0) - (b.defaultTaxRate ?? 0),
        render: (_rate: number | undefined, record: AdminCategory) => {
          const rate = record.defaultTaxRate ?? (record as { vatRate?: number }).vatRate;
          return rate != null ? `${rate}%` : '—';
        },
      },
      {
        title: t('common.categories.table.fiscalCategory'),
        dataIndex: 'fiscalCategory',
        key: 'fiscalCategory',
        width: 140,
        render: (cat: RksvProductCategoryValue | undefined) => <FiscalCategoryTag value={cat} t={t} />,
      },
      {
        title: t('common.categories.table.productCount'),
        dataIndex: 'productCount',
        key: 'productCount',
        width: 100,
        align: 'right',
        sorter: (a, b) => (a.productCount ?? 0) - (b.productCount ?? 0),
        render: (count: number | undefined) => count ?? 0,
      },
      {
        title: t('common.categories.table.sortOrder'),
        dataIndex: 'sortOrder',
        key: 'sortOrder',
        width: 100,
        align: 'right',
        sorter: (a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0),
      },
      ];

      if (canManage) {
        cols.push({
        title: t('common.categories.table.actions'),
        key: 'actions',
        width: 220,
        align: 'right',
        render: (_: unknown, record: AdminCategory) => (
          <Space size="small" wrap>
            <Button
              type="default"
              size="small"
              icon={<EditOutlined />}
              onClick={() => onEdit(record)}
            >
              {t('common.buttons.edit')}
            </Button>
            {!record.isSystemCategory ? (
              <Popconfirm
                title={t('common.categories.deleteConfirmTitle')}
                description={t('common.categories.list.deleteConfirmDescription')}
                onConfirm={() => onDelete(record)}
                okText={t('common.buttons.yes')}
                cancelText={t('common.buttons.no')}
              >
                <Button
                  type="default"
                  size="small"
                  danger
                  icon={<DeleteOutlined />}
                  loading={deleteLoadingId === record.id}
                >
                  {t('common.buttons.delete')}
                </Button>
              </Popconfirm>
            ) : null}
          </Space>
        ),
      });
      }

      return cols;
    },
    [t, onEdit, onDelete, onUpdateName, deleteLoadingId, canManage],
  );

  return (
    <Table<AdminCategory>
      columns={columns}
      dataSource={data}
      rowKey="id"
      loading={loading}
      pagination={{ ...adminTablePaginationDefaults, pageSize: 10 }}
      expandable={expandable}
      locale={{ emptyText: <Empty description={t('common.categories.emptyCategories')} /> }}
    />
  );
}
