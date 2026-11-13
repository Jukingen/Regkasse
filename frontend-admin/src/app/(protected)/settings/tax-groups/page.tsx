'use client';

import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  ColorPicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { Color } from 'antd/es/color-picker';
import type { ColumnsType } from 'antd/es/table';
import React, { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { TaxComplianceCard } from '@/features/tax/components/TaxComplianceCard';
import { TaxGroupStatsDashboard } from '@/features/tax/components/TaxGroupStatsDashboard';
import { TaxPreview } from '@/features/tax/components/TaxPreview';
import {
  type TaxGroupAdmin,
  type UpsertTaxGroupRequest,
  bulkUpdateProductTaxGroups,
  createTaxGroup,
  deleteTaxGroup,
  getTaxGroups,
  taxGroupStatsQueryKey,
  taxGroupsQueryKey,
  updateTaxGroup,
} from '@/features/tax/api/taxGroups';
import { taxHistoryQueryKey } from '@/features/tax/api/taxHistory';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

type TaxGroupFormValues = {
  name: string;
  description?: string;
  rate: number;
  austrianCode?: string;
  color?: string | Color;
  icon?: string;
  isActive: boolean;
  isDefault?: boolean;
};

function colorToHex(value: string | Color | undefined | null): string | null {
  if (value == null || value === '') return null;
  if (typeof value === 'string') return value;
  if (typeof value.toHexString === 'function') return value.toHexString();
  return null;
}

export default function TaxGroupsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const canManage = hasPermission(user, PERMISSIONS.PRODUCT_MANAGE);
  const [form] = Form.useForm<TaxGroupFormValues>();
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingGroup, setEditingGroup] = useState<TaxGroupAdmin | null>(null);
  const [bulkFromId, setBulkFromId] = useState<string | undefined>();
  const [bulkToId, setBulkToId] = useState<string | undefined>();

  const { data: taxGroups, isLoading } = useQuery({
    queryKey: taxGroupsQueryKey,
    queryFn: getTaxGroups,
  });

  const taxGroupSelectOptions = useMemo(
    () =>
      (taxGroups ?? [])
        .filter((g) => g.isActive)
        .map((g) => ({
          value: g.id,
          label: `${g.icon ? `${g.icon} ` : ''}${g.name} (${g.rate}%)`,
        })),
    [taxGroups]
  );

  const bulkFromRate = taxGroups?.find((g) => g.id === bulkFromId)?.rate;
  const bulkToRate = taxGroups?.find((g) => g.id === bulkToId)?.rate;

  const invalidateTaxGroupQueries = () => {
    void queryClient.invalidateQueries({ queryKey: taxGroupsQueryKey });
    void queryClient.invalidateQueries({ queryKey: taxGroupStatsQueryKey });
  };

  const createMutation = useMutation({
    mutationFn: createTaxGroup,
    onSuccess: () => {
      notify.successKey('settings.taxGroups.created');
      invalidateTaxGroupQueries();
      setIsModalOpen(false);
      form.resetFields();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TaxGroups.create',
        fallbackKey: 'settings.taxGroups.saveFailed',
      });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpsertTaxGroupRequest }) =>
      updateTaxGroup(id, body),
    onSuccess: () => {
      notify.successKey('settings.taxGroups.updated');
      invalidateTaxGroupQueries();
      setIsModalOpen(false);
      form.resetFields();
      setEditingGroup(null);
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TaxGroups.update',
        fallbackKey: 'settings.taxGroups.saveFailed',
      });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteTaxGroup,
    onSuccess: () => {
      notify.successKey('settings.taxGroups.deleted');
      invalidateTaxGroupQueries();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TaxGroups.delete',
        fallbackKey: 'settings.taxGroups.deleteFailed',
      });
    },
  });

  const bulkMutation = useMutation({
    mutationFn: bulkUpdateProductTaxGroups,
    onSuccess: (result) => {
      notify.successKey('settings.taxGroups.bulk.success', {
        count: result.updatedProducts,
        oldRate: result.oldRate,
        newRate: result.newRate,
      });
      invalidateTaxGroupQueries();
      void queryClient.invalidateQueries({ queryKey: taxHistoryQueryKey });
      setBulkFromId(undefined);
      setBulkToId(undefined);
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TaxGroups.bulkUpdate',
        fallbackKey: 'settings.taxGroups.bulk.failed',
      });
    },
  });

  const handleBulkUpdate = () => {
    if (!bulkFromId || !bulkToId) {
      notify.warning('settings.taxGroups.bulk.selectBoth');
      return;
    }
    if (bulkFromId === bulkToId) {
      notify.warning('settings.taxGroups.bulk.mustDiffer');
      return;
    }

    const fromGroup = taxGroups?.find((g) => g.id === bulkFromId);
    const toGroup = taxGroups?.find((g) => g.id === bulkToId);
    modal.confirm({
      title: t('settings.taxGroups.bulk.confirmTitle'),
      content: t('settings.taxGroups.bulk.confirmContent', {
        from: fromGroup ? `${fromGroup.name} (${fromGroup.rate}%)` : bulkFromId,
        to: toGroup ? `${toGroup.name} (${toGroup.rate}%)` : bulkToId,
      }),
      okText: t('settings.taxGroups.bulk.confirmOk'),
      cancelText: t('common.buttons.cancel'),
      onOk: () =>
        bulkMutation.mutateAsync({
          oldTaxGroupId: bulkFromId,
          newTaxGroupId: bulkToId,
          reason: t('settings.taxGroups.bulk.defaultReason'),
        }),
    });
  };

  const openCreate = () => {
    setEditingGroup(null);
    form.resetFields();
    form.setFieldsValue({ isActive: true, isDefault: false, rate: 20 });
    setIsModalOpen(true);
  };

  const openEdit = (record: TaxGroupAdmin) => {
    setEditingGroup(record);
    form.setFieldsValue({
      name: record.name,
      description: record.description ?? undefined,
      rate: record.rate,
      austrianCode: record.austrianCode ?? undefined,
      color: record.color ?? undefined,
      icon: record.icon ?? undefined,
      isActive: record.isActive,
      isDefault: record.isDefault,
    });
    setIsModalOpen(true);
  };

  const confirmDelete = (record: TaxGroupAdmin) => {
    modal.confirm({
      title: t('settings.taxGroups.deleteConfirmTitle'),
      content: t('settings.taxGroups.deleteConfirmContent'),
      okText: t('common.buttons.delete'),
      okType: 'danger',
      cancelText: t('common.buttons.cancel'),
      onOk: () => deleteMutation.mutateAsync(record.id),
    });
  };

  const onFinish = (values: TaxGroupFormValues) => {
    const body: UpsertTaxGroupRequest = {
      name: values.name,
      description: values.description ?? null,
      rate: values.rate,
      austrianCode: values.austrianCode ?? null,
      color: colorToHex(values.color),
      icon: values.icon ?? null,
      isActive: values.isActive,
      isDefault: values.isDefault ?? false,
    };

    if (editingGroup) {
      updateMutation.mutate({ id: editingGroup.id, body });
    } else {
      createMutation.mutate(body);
    }
  };

  const columns: ColumnsType<TaxGroupAdmin> = useMemo(
    () => [
      {
        title: t('settings.taxGroups.columns.name'),
        dataIndex: 'name',
        key: 'name',
        render: (name: string, record) => (
          <Space>
            {record.color ? (
              <span
                style={{
                  width: 12,
                  height: 12,
                  borderRadius: 2,
                  background: record.color,
                  display: 'inline-block',
                }}
              />
            ) : null}
            <span>{record.icon}</span>
            <span>{name}</span>
            {record.isDefault ? <Tag color="blue">{t('settings.taxGroups.default')}</Tag> : null}
            {record.isSystem ? <Tag>{t('settings.taxGroups.system')}</Tag> : null}
          </Space>
        ),
      },
      {
        title: t('settings.taxGroups.columns.rate'),
        dataIndex: 'rate',
        key: 'rate',
        width: 100,
        render: (rate: number) => `${rate}%`,
      },
      {
        title: t('settings.taxGroups.columns.description'),
        dataIndex: 'description',
        key: 'description',
        ellipsis: true,
      },
      {
        title: t('settings.taxGroups.columns.code'),
        dataIndex: 'austrianCode',
        key: 'austrianCode',
        width: 90,
        render: (code: string | null | undefined) => (code ? <Tag>{code}</Tag> : '—'),
      },
      {
        title: t('settings.taxGroups.columns.status'),
        dataIndex: 'isActive',
        key: 'isActive',
        width: 110,
        render: (isActive: boolean) => (
          <Tag color={isActive ? 'green' : 'red'}>
            {isActive ? t('settings.taxGroups.active') : t('settings.taxGroups.inactive')}
          </Tag>
        ),
      },
      ...(canManage
        ? [
            {
              title: t('settings.taxGroups.columns.actions'),
              key: 'actions',
              width: 220,
              render: (_: unknown, record: TaxGroupAdmin) => (
                <Space>
                  <Button icon={<EditOutlined />} size="small" onClick={() => openEdit(record)}>
                    {t('common.buttons.edit')}
                  </Button>
                  {!record.isSystem ? (
                    <Button
                      icon={<DeleteOutlined />}
                      size="small"
                      danger
                      onClick={() => confirmDelete(record)}
                    >
                      {t('common.buttons.delete')}
                    </Button>
                  ) : null}
                </Space>
              ),
            } satisfies ColumnsType<TaxGroupAdmin>[number],
          ]
        : []),
    ],
    // openEdit/confirmDelete are stable enough for this page; t + canManage drive labels
    // eslint-disable-next-line react-hooks/exhaustive-deps -- intentional
    [canManage, t]
  );

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('settings.taxGroups.pageTitle') },
  ];

  const saving = createMutation.isPending || updateMutation.isPending;
  const isSystemEdit = Boolean(editingGroup?.isSystem);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('settings.taxGroups.pageTitle')} breadcrumbs={breadcrumbs} />

      <TaxGroupStatsDashboard />

      <TaxComplianceCard />

      <Card
        title={t('settings.taxGroups.cardTitle')}
        extra={
          canManage ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('settings.taxGroups.create')}
            </Button>
          ) : null
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          {t('settings.taxGroups.description')}
        </Typography.Paragraph>
        <Table<TaxGroupAdmin>
          dataSource={taxGroups}
          columns={columns}
          loading={isLoading}
          rowKey="id"
          pagination={false}
        />
      </Card>

      {canManage ? (
        <Card title={t('settings.taxGroups.bulk.cardTitle')}>
          <Space wrap size="middle" style={{ marginBottom: 16 }} align="center">
            <Select
              placeholder={t('settings.taxGroups.bulk.fromPlaceholder')}
              options={taxGroupSelectOptions}
              value={bulkFromId}
              onChange={setBulkFromId}
              style={{ minWidth: 192 }}
              allowClear
              showSearch
              optionFilterProp="label"
            />
            <span aria-hidden>→</span>
            <Select
              placeholder={t('settings.taxGroups.bulk.toPlaceholder')}
              options={taxGroupSelectOptions}
              value={bulkToId}
              onChange={setBulkToId}
              style={{ minWidth: 192 }}
              allowClear
              showSearch
              optionFilterProp="label"
            />
            <Button type="primary" loading={bulkMutation.isPending} onClick={handleBulkUpdate}>
              {t('settings.taxGroups.bulk.submit')}
            </Button>
          </Space>
          <Alert
            type="warning"
            showIcon
            title={t('settings.taxGroups.bulk.warningTitle')}
            description={t('settings.taxGroups.bulk.warningDescription')}
          />
        </Card>
      ) : null}

      {canManage && bulkFromId && bulkToId && bulkFromId !== bulkToId ? (
        <TaxPreview currentTaxRate={bulkFromRate} newTaxRate={bulkToRate} />
      ) : null}

      <Modal
        title={
          editingGroup ? t('settings.taxGroups.editTitle') : t('settings.taxGroups.createTitle')
        }
        open={isModalOpen}
        onCancel={() => {
          setIsModalOpen(false);
          setEditingGroup(null);
          form.resetFields();
        }}
        footer={null}
        destroyOnHidden
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={onFinish}
          initialValues={{ isActive: true, isDefault: false }}
        >
          <Form.Item
            name="name"
            label={t('settings.taxGroups.form.name')}
            rules={[{ required: true, message: t('settings.taxGroups.form.nameRequired') }]}
          >
            <Input />
          </Form.Item>

          <Form.Item name="description" label={t('settings.taxGroups.form.description')}>
            <Input.TextArea rows={2} />
          </Form.Item>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item
              name="rate"
              label={t('settings.taxGroups.form.rate')}
              rules={[{ required: true, message: t('settings.taxGroups.form.rateRequired') }]}
            >
              <InputNumber
                style={{ width: '100%' }}
                min={0}
                max={100}
                step={0.1}
                precision={1}
                disabled={isSystemEdit}
                addonAfter="%"
              />
            </Form.Item>

            <Form.Item name="austrianCode" label={t('settings.taxGroups.form.austrianCode')}>
              <Select
                allowClear
                disabled={isSystemEdit}
                placeholder={t('settings.taxGroups.form.codeOptional')}
              >
                <Select.Option value="A">A (20%)</Select.Option>
                <Select.Option value="B">B (10%)</Select.Option>
                <Select.Option value="C">C (4,9%)</Select.Option>
                <Select.Option value="D">D (13%)</Select.Option>
                <Select.Option value="E">E (0%)</Select.Option>
              </Select>
            </Form.Item>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item
              name="color"
              label={t('settings.taxGroups.form.color')}
              getValueFromEvent={(color: Color | string) => colorToHex(color)}
            >
              <ColorPicker format="hex" showText />
            </Form.Item>

            <Form.Item name="icon" label={t('settings.taxGroups.form.icon')}>
              <Input placeholder="💰" maxLength={50} />
            </Form.Item>
          </div>

          <Form.Item
            name="isActive"
            label={t('settings.taxGroups.form.status')}
            valuePropName="checked"
          >
            <Switch
              checkedChildren={t('settings.taxGroups.active')}
              unCheckedChildren={t('settings.taxGroups.inactive')}
            />
          </Form.Item>

          <Form.Item
            name="isDefault"
            label={t('settings.taxGroups.form.isDefault')}
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>

          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={saving}>
                {t('common.buttons.save')}
              </Button>
              <Button
                onClick={() => {
                  setIsModalOpen(false);
                  setEditingGroup(null);
                  form.resetFields();
                }}
              >
                {t('common.buttons.cancel')}
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
