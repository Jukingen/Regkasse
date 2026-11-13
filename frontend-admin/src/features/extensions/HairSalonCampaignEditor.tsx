'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import Link from 'next/link';
import { useEffect, useState } from 'react';

import {
  HAIR_SALON_CAMPAIGN_EXTENSION,
  createHairSalonCampaign,
  deactivateHairSalonCampaign,
  hairSalonCampaignQueryKeys,
  updateHairSalonCampaign,
  useHairSalonCampaigns,
  type HairSalonCampaign,
  type UpsertHairSalonCampaignRequest,
} from '@/api/admin/hairSalonCampaigns';
import { useAdminProductsList } from '@/api/admin/products';
import { useTenantExtensions } from '@/api/admin/tenantExtensions';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

interface CampaignFormValues {
  name: string;
  discountPercent: number;
  appliesToProductIds?: string[];
  validFrom: Dayjs;
  validUntil: Dayjs;
  isActive?: boolean;
}

function toRequest(values: CampaignFormValues, includeActive: boolean): UpsertHairSalonCampaignRequest {
  return {
    name: values.name.trim(),
    discountPercent: values.discountPercent,
    appliesToProductIds: values.appliesToProductIds ?? [],
    validFromUtc: values.validFrom.toDate().toISOString(),
    validUntilUtc: values.validUntil.toDate().toISOString(),
    ...(includeActive ? { isActive: values.isActive !== false } : {}),
  };
}

export function HairSalonCampaignEditor() {
  const { t } = useI18n();
  const { user } = useAuth();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
  const [form] = Form.useForm<CampaignFormValues>();
  const [editing, setEditing] = useState<HairSalonCampaign | null>(null);
  const [open, setOpen] = useState(false);

  const extensions = useTenantExtensions();
  const enabled = (extensions.data ?? []).some(
    (row) => row.name === HAIR_SALON_CAMPAIGN_EXTENSION && row.enabled
  );
  const campaigns = useHairSalonCampaigns({ enabled });
  const products = useAdminProductsList(
    { pageNumber: 1, pageSize: 200 },
    { enabled }
  );

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: hairSalonCampaignQueryKeys.all });
  };

  const save = useMutation({
    mutationFn: (values: CampaignFormValues) => {
      const body = toRequest(values, editing !== null);
      return editing
        ? updateHairSalonCampaign(editing.id, body)
        : createHairSalonCampaign(body);
    },
    onSuccess: async () => {
      await refresh();
      notify.successKey(editing ? 'admin.hairSalonCampaigns.saved' : 'admin.hairSalonCampaigns.created');
      setOpen(false);
      setEditing(null);
    },
    onError: (err) =>
      notify.apiError(err, {
        logContext: 'HairSalonCampaignEditor.save',
        fallbackKey: 'admin.hairSalonCampaigns.saveError',
      }),
  });

  const deactivate = useMutation({
    mutationFn: (id: string) => deactivateHairSalonCampaign(id),
    onSuccess: async () => {
      await refresh();
      notify.successKey('admin.hairSalonCampaigns.deactivated');
    },
    onError: (err) =>
      notify.apiError(err, {
        logContext: 'HairSalonCampaignEditor.deactivate',
        fallbackKey: 'admin.hairSalonCampaigns.saveError',
      }),
  });

  const openCreate = () => {
    setEditing(null);
    setOpen(true);
  };

  const openEdit = (row: HairSalonCampaign) => {
    setEditing(row);
    setOpen(true);
  };

  useEffect(() => {
    if (!open) return;
    if (editing) {
      form.setFieldsValue({
        name: editing.name,
        discountPercent: editing.discountPercent,
        appliesToProductIds: editing.appliesToProductIds ?? [],
        validFrom: dayjs(editing.validFromUtc),
        validUntil: dayjs(editing.validUntilUtc),
        isActive: editing.isActive,
      });
      return;
    }
    form.setFieldsValue({
      name: '',
      discountPercent: 10,
      appliesToProductIds: [],
      validFrom: dayjs(),
      validUntil: dayjs().add(30, 'day'),
      isActive: true,
    });
  }, [editing, form, open]);

  if (extensions.isLoading) {
    return <Table loading columns={[]} dataSource={[]} />;
  }

  if (extensions.isError) {
    return <Alert type="error" showIcon title={t('admin.hairSalonCampaigns.loadError')} />;
  }

  if (!enabled) {
    return (
      <Alert
        type="info"
        showIcon
        title={t('admin.hairSalonCampaigns.notEnabled')}
        action={
          <Link href="/admin/extensions">{t('admin.hairSalonCampaigns.back')}</Link>
        }
      />
    );
  }

  const productName = new Map((products.data?.items ?? []).map((product) => [product.id, product.name]));

  const columns: ColumnsType<HairSalonCampaign> = [
    { title: t('admin.hairSalonCampaigns.colName'), dataIndex: 'name', key: 'name' },
    {
      title: t('admin.hairSalonCampaigns.colDiscount'),
      dataIndex: 'discountPercent',
      key: 'discount',
      render: (value: number) => `${value}`,
    },
    {
      title: t('admin.hairSalonCampaigns.colAppliesTo'),
      key: 'applies',
      render: (_value, row) => {
        const ids = row.appliesToProductIds ?? [];
        if (ids.length === 0) return t('admin.hairSalonCampaigns.allProducts');
        return ids.map((id) => productName.get(id) ?? id).join(', ');
      },
    },
    {
      title: t('admin.hairSalonCampaigns.colValidity'),
      key: 'validity',
      render: (_value, row) => {
        const from = new Date(row.validFromUtc);
        const until = new Date(row.validUntilUtc);
        if (Number.isNaN(from.getTime()) || Number.isNaN(until.getTime())) return '—';
        return `${from.toLocaleString()} – ${until.toLocaleString()}`;
      },
    },
    {
      title: t('admin.hairSalonCampaigns.colActive'),
      dataIndex: 'isActive',
      key: 'active',
      render: (active: boolean) => (
        <Tag color={active ? 'green' : 'default'}>
          {active ? t('admin.hairSalonCampaigns.active') : t('admin.hairSalonCampaigns.inactive')}
        </Tag>
      ),
    },
    {
      title: t('admin.extensions.colActions'),
      key: 'actions',
      render: (_value, row) => (
        <Space>
          <Button size="small" disabled={!canWrite} onClick={() => openEdit(row)}>
            {t('admin.hairSalonCampaigns.edit')}
          </Button>
          {row.isActive ? (
            <Button
              size="small"
              danger
              disabled={!canWrite || deactivate.isPending}
              onClick={() => {
                modal.confirm({
                  title: t('admin.hairSalonCampaigns.deactivate'),
                  content: t('admin.hairSalonCampaigns.deactivateConfirm'),
                  okText: t('admin.hairSalonCampaigns.deactivate'),
                  cancelText: t('admin.hairSalonCampaigns.cancel'),
                  onOk: () => deactivate.mutateAsync(row.id),
                });
              }}
            >
              {t('admin.hairSalonCampaigns.deactivate')}
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" disabled={!canWrite} onClick={openCreate}>
          {t('admin.hairSalonCampaigns.create')}
        </Button>
        <Link href="/admin/extensions">{t('admin.hairSalonCampaigns.back')}</Link>
      </Space>
      {campaigns.isError ? (
        <Alert type="error" showIcon title={t('admin.hairSalonCampaigns.loadError')} style={{ marginBottom: 16 }} />
      ) : null}
      <Table
        rowKey="id"
        loading={campaigns.isLoading}
        columns={columns}
        dataSource={campaigns.data ?? []}
        pagination={false}
        locale={{ emptyText: t('admin.hairSalonCampaigns.empty') }}
      />
      <Modal
        open={open}
        destroyOnHidden
        title={editing ? t('admin.hairSalonCampaigns.edit') : t('admin.hairSalonCampaigns.create')}
        okText={t('admin.hairSalonCampaigns.save')}
        cancelText={t('admin.hairSalonCampaigns.cancel')}
        confirmLoading={save.isPending}
        onCancel={() => {
          setOpen(false);
          setEditing(null);
        }}
        onOk={() => form.submit()}
      >
        <Form form={form} layout="vertical" onFinish={(values) => save.mutate(values)}>
          <Form.Item
            name="name"
            label={t('admin.hairSalonCampaigns.fieldName')}
            rules={[{ required: true, message: t('admin.hairSalonCampaigns.nameRequired') }]}
          >
            <Input maxLength={255} aria-label={t('admin.hairSalonCampaigns.fieldName')} />
          </Form.Item>
          <Form.Item
            name="discountPercent"
            label={t('admin.hairSalonCampaigns.fieldDiscount')}
            rules={[
              { required: true, message: t('admin.hairSalonCampaigns.discountRange') },
              {
                type: 'number',
                min: 0,
                max: 100,
                message: t('admin.hairSalonCampaigns.discountRange'),
              },
            ]}
          >
            <InputNumber
              min={0}
              max={100}
              step={0.01}
              style={{ width: '100%' }}
              aria-label={t('admin.hairSalonCampaigns.fieldDiscount')}
            />
          </Form.Item>
          <Form.Item name="appliesToProductIds" label={t('admin.hairSalonCampaigns.fieldProducts')}>
            <Select
              mode="multiple"
              allowClear
              placeholder={t('admin.hairSalonCampaigns.productsPlaceholder')}
              aria-label={t('admin.hairSalonCampaigns.fieldProducts')}
              options={(products.data?.items ?? []).map((product) => ({
                value: product.id,
                label: product.name,
              }))}
            />
          </Form.Item>
          <Form.Item
            name="validFrom"
            label={t('admin.hairSalonCampaigns.fieldValidFrom')}
            rules={[{ required: true, message: t('admin.hairSalonCampaigns.validityOrder') }]}
          >
            <DatePicker showTime style={{ width: '100%' }} aria-label={t('admin.hairSalonCampaigns.fieldValidFrom')} />
          </Form.Item>
          <Form.Item
            name="validUntil"
            label={t('admin.hairSalonCampaigns.fieldValidUntil')}
            rules={[
              { required: true, message: t('admin.hairSalonCampaigns.validityOrder') },
              ({ getFieldValue }) => ({
                validator(_rule, value: Dayjs | undefined) {
                  const from = getFieldValue('validFrom') as Dayjs | undefined;
                  if (!value || !from || value.isAfter(from)) return Promise.resolve();
                  return Promise.reject(new Error(t('admin.hairSalonCampaigns.validityOrder')));
                },
              }),
            ]}
          >
            <DatePicker showTime style={{ width: '100%' }} aria-label={t('admin.hairSalonCampaigns.fieldValidUntil')} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
