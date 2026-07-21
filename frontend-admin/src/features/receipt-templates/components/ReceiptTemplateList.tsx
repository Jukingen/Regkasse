'use client';

import { DeleteOutlined, EditOutlined, EyeOutlined, FileTextOutlined } from '@ant-design/icons';
import { Button, Popconfirm, Space, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import Link from 'next/link';
import React from 'react';

import type { ReceiptTemplate } from '@/api/generated/model';
import { adminTablePaginationDefaults } from '@/components/ui/adminTablePagination';
import { useI18n } from '@/i18n';

interface ReceiptTemplateListProps {
  data: ReceiptTemplate[];
  loading: boolean;
  canManage?: boolean;
  onDelete: (id: string) => void;
  onPreview: (id: string) => void;
}

export default function ReceiptTemplateList({
  data,
  loading,
  canManage = false,
  onDelete,
  onPreview,
}: ReceiptTemplateListProps) {
  const { t } = useI18n();

  const columns: ColumnsType<ReceiptTemplate> = [
    {
      title: t('receiptTemplates.list.colName'),
      dataIndex: 'templateName',
      key: 'templateName',
      render: (text: string) => <span style={{ fontWeight: 600 }}>{text}</span>,
    },
    {
      title: t('receiptTemplates.list.colLanguage'),
      dataIndex: 'language',
      key: 'language',
      render: (text: string) => <Tag>{text.toUpperCase()}</Tag>,
    },
    {
      title: t('receiptTemplates.list.colType'),
      dataIndex: 'templateType',
      key: 'templateType',
      render: (text: string) => <Tag color="blue">{text}</Tag>,
    },
    {
      title: t('receiptTemplates.list.colDefault'),
      dataIndex: 'isDefault',
      key: 'isDefault',
      render: (val: boolean) =>
        val ? <Tag color="green">{t('receiptTemplates.list.tagYes')}</Tag> : '—',
    },
    {
      title: t('receiptTemplates.list.colActive'),
      dataIndex: 'isActive',
      key: 'isActive',
      render: (val: boolean) =>
        val ? (
          <Tag color="green">{t('receiptTemplates.list.tagActive')}</Tag>
        ) : (
          <Tag>{t('receiptTemplates.list.tagInactive')}</Tag>
        ),
    },
    {
      title: t('receiptTemplates.list.colCreated'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => dayjs(date).format('DD.MM.YYYY HH:mm'),
    },
    {
      title: t('receiptTemplates.list.colActions'),
      key: 'actions',
      width: 180,
      render: (_: unknown, record: ReceiptTemplate) => (
        <Space>
          {canManage && (
            <Link href={`/receipt-templates/${record.id}`}>
              <Button size="small" icon={<EditOutlined />}>
                {t('receiptTemplates.list.edit')}
              </Button>
            </Link>
          )}
          <Button size="small" icon={<EyeOutlined />} onClick={() => onPreview(record.id!)}>
            {t('receiptTemplates.list.preview')}
          </Button>
          <Link href={`/receipt-generate?templateId=${record.id ?? ''}`}>
            <Button size="small" icon={<FileTextOutlined />}>
              {t('receiptTemplates.list.generate')}
            </Button>
          </Link>
          {canManage && (
            <Popconfirm
              title={t('receiptTemplates.list.deleteConfirm')}
              onConfirm={() => onDelete(record.id!)}
              okText={t('common.buttons.yes')}
              cancelText={t('common.buttons.no')}
            >
              <Button size="small" danger icon={<DeleteOutlined />} />
            </Popconfirm>
          )}
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
      pagination={{ ...adminTablePaginationDefaults }}
    />
  );
}
