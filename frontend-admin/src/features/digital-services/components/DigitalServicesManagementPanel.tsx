'use client';

import { useMemo, useRef, useState } from 'react';
import { Alert, Button, Form, Input, InputNumber, Modal, Space, Switch, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined } from '@ant-design/icons';
import { DigitalServiceRequestsPanel } from '@/features/digital-services/components/DigitalServiceRequestsPanel';
import {
  type DigitalServiceType,
  type TenantDigitalServiceRow,
} from '@/features/digital-services/api/tenantDigitalServicesApi';
import {
  useTenantDigitalServices,
  useToggleTenantDigitalService,
  useUpdateTenantDigitalServicePrice,
} from '@/features/digital-services/hooks/useTenantDigitalServices';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

const { Paragraph } = Typography;

type PriceEditTarget = {
  tenantId: string;
  tenantName: string;
  serviceType: DigitalServiceType;
  currentPrice: number;
  customPrice: number | null;
  listPrice: number;
};

export function DigitalServicesManagementPanel() {
  const { t, formatLocale } = useI18n();
  const { message, modal } = useAntdApp();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const canActivate =
    isSuperAdmin ||
    hasPermission(PERMISSIONS.DIGITAL_ACTIVATE) ||
    hasPermission(PERMISSIONS.DIGITAL_MANAGE);
  const canPrice =
    isSuperAdmin ||
    hasPermission(PERMISSIONS.DIGITAL_PRICING_MANAGE) ||
    hasPermission(PERMISSIONS.DIGITAL_MANAGE);
  const canView =
    canActivate ||
    canPrice ||
    hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

  const { data, isLoading, isError } = useTenantDigitalServices();
  const toggleMutation = useToggleTenantDigitalService();
  const priceMutation = useUpdateTenantDigitalServicePrice();
  const [editing, setEditing] = useState<PriceEditTarget | null>(null);
  const [priceForm] = Form.useForm<{ customPrice: number | null }>();
  const deactivateReasonRef = useRef('');

  const formatMoney = (value: number, currency = 'EUR') => {
    try {
      return new Intl.NumberFormat(formatLocale, { style: 'currency', currency }).format(value);
    } catch {
      return `${value.toFixed(2)} ${currency}`;
    }
  };

  const serviceLabel = (serviceType: DigitalServiceType) =>
    serviceType === 'website'
      ? t('superadmin.digital.columns.website')
      : t('superadmin.digital.columns.app');

  const openPriceModal = (row: TenantDigitalServiceRow, serviceType: DigitalServiceType) => {
    const state = serviceType === 'website' ? row.website : row.app;
    setEditing({
      tenantId: row.tenantId,
      tenantName: row.name,
      serviceType,
      currentPrice: state.price,
      customPrice: state.customPrice,
      listPrice: state.listPrice,
    });
    priceForm.setFieldsValue({ customPrice: state.customPrice ?? state.listPrice });
  };

  const toggleService = async (
    row: TenantDigitalServiceRow,
    serviceType: DigitalServiceType,
    active: boolean,
    reason?: string,
  ) => {
    try {
      await toggleMutation.mutateAsync({
        tenantId: row.tenantId,
        serviceType,
        active,
        reason: active ? undefined : reason?.trim() || undefined,
      });
      message.success(
        active
          ? t('superadmin.digital.toggleActivated')
          : t('superadmin.digital.toggleDeactivated'),
      );
    } catch {
      message.error(t('superadmin.digital.toggleFailed'));
    }
  };

  const handleToggle = (
    row: TenantDigitalServiceRow,
    serviceType: DigitalServiceType,
    active: boolean,
  ) => {
    if (active) {
      void toggleService(row, serviceType, true);
      return;
    }

    deactivateReasonRef.current = '';
    modal.confirm({
      title: t('superadmin.digital.deactivateConfirmTitle'),
      content: (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Paragraph style={{ marginBottom: 0 }}>
            {t('superadmin.digital.deactivateConfirmBody', {
              tenant: row.name,
              service: serviceLabel(serviceType),
            })}
          </Paragraph>
          <Input.TextArea
            rows={3}
            placeholder={t('superadmin.digital.deactivateReasonPlaceholder')}
            onChange={(e) => {
              deactivateReasonRef.current = e.target.value;
            }}
            maxLength={500}
            showCount
          />
        </Space>
      ),
      okText: t('superadmin.digital.deactivateConfirmOk'),
      cancelText: t('common.buttons.cancel'),
      okButtonProps: { danger: true },
      onOk: () => toggleService(row, serviceType, false, deactivateReasonRef.current),
    });
  };

  const handleSavePrice = async () => {
    if (!editing) return;
    try {
      const values = await priceForm.validateFields();
      const raw = values.customPrice;
      const customPrice =
        raw === null || raw === undefined || raw === editing.listPrice ? null : raw;
      await priceMutation.mutateAsync({
        tenantId: editing.tenantId,
        serviceType: editing.serviceType,
        customPrice,
      });
      message.success(t('superadmin.digital.priceUpdated'));
      setEditing(null);
    } catch {
      message.error(t('superadmin.digital.priceUpdateFailed'));
    }
  };

  const columns: ColumnsType<TenantDigitalServiceRow> = useMemo(
    () => [
      {
        title: t('superadmin.digital.columns.tenant'),
        key: 'name',
        render: (_, row) => (
          <Space orientation="vertical" size={0}>
            <span>{row.name}</span>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.slug}
            </Typography.Text>
          </Space>
        ),
      },
      {
        title: t('superadmin.digital.columns.website'),
        key: 'website',
        render: (_, row) => (
          <Switch
            checked={row.website.isActive}
            disabled={!canActivate || toggleMutation.isPending}
            onChange={(checked) => handleToggle(row, 'website', checked)}
          />
        ),
      },
      {
        title: t('superadmin.digital.columns.app'),
        key: 'app',
        render: (_, row) => (
          <Switch
            checked={row.app.isActive}
            disabled={!canActivate || toggleMutation.isPending}
            onChange={(checked) => handleToggle(row, 'app', checked)}
          />
        ),
      },
      {
        title: t('superadmin.digital.columns.websitePrice'),
        key: 'websitePrice',
        render: (_, row) => (
          <Space>
            <span>{formatMoney(row.website.price, row.website.currency)}</span>
            {row.website.customPrice != null && (
              <Tag color="blue">{t('superadmin.digital.customPriceTag')}</Tag>
            )}
            {canPrice && (
              <Button
                size="small"
                type="text"
                icon={<EditOutlined />}
                aria-label={t('superadmin.digital.editPrice')}
                onClick={() => openPriceModal(row, 'website')}
              />
            )}
          </Space>
        ),
      },
      {
        title: t('superadmin.digital.columns.appPrice'),
        key: 'appPrice',
        render: (_, row) => (
          <Space>
            <span>{formatMoney(row.app.price, row.app.currency)}</span>
            {row.app.customPrice != null && (
              <Tag color="blue">{t('superadmin.digital.customPriceTag')}</Tag>
            )}
            {canPrice && (
              <Button
                size="small"
                type="text"
                icon={<EditOutlined />}
                aria-label={t('superadmin.digital.editPrice')}
                onClick={() => openPriceModal(row, 'app')}
              />
            )}
          </Space>
        ),
      },
      {
        title: t('superadmin.digital.columns.status'),
        key: 'status',
        render: (_, row) => (
          <Space wrap>
            <Tag color={row.website.isActive ? 'success' : 'error'}>
              {t('superadmin.digital.websiteStatus', {
                status: row.website.isActive
                  ? t('superadmin.digital.active')
                  : t('superadmin.digital.inactive'),
              })}
            </Tag>
            <Tag color={row.app.isActive ? 'success' : 'error'}>
              {t('superadmin.digital.appStatus', {
                status: row.app.isActive
                  ? t('superadmin.digital.active')
                  : t('superadmin.digital.inactive'),
              })}
            </Tag>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- handlers use stable mutation + i18n
    [canActivate, canPrice, formatLocale, t, toggleMutation.isPending],
  );

  if (!canView) {
    return <Alert type="warning" showIcon message={t('superadmin.digital.accessDenied')} />;
  }

  if (isError) {
    return <Alert type="error" showIcon message={t('superadmin.digital.loadFailed')} />;
  }

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('superadmin.digital.pageSubtitle')}
      </Paragraph>

      <DigitalServiceRequestsPanel viewAllHref="/admin/digital/requests" />

      <Table<TenantDigitalServiceRow>
        loading={isLoading}
        dataSource={data ?? []}
        columns={columns}
        rowKey="tenantId"
        pagination={{ pageSize: 20 }}
        locale={{ emptyText: t('superadmin.digital.empty') }}
      />

      <Modal
        title={t('superadmin.digital.priceModalTitle')}
        open={!!editing}
        destroyOnHidden
        onCancel={() => setEditing(null)}
        onOk={() => void handleSavePrice()}
        confirmLoading={priceMutation.isPending}
        okText={t('superadmin.digital.priceModalSave')}
        cancelText={t('common.buttons.cancel')}
      >
        {editing && (
          <Form form={priceForm} layout="vertical">
            <Paragraph type="secondary">
              {t('superadmin.digital.priceModalHint', {
                tenant: editing.tenantName,
                service:
                  editing.serviceType === 'website'
                    ? t('superadmin.digital.columns.website')
                    : t('superadmin.digital.columns.app'),
                listPrice: formatMoney(editing.listPrice),
              })}
            </Paragraph>
            <Form.Item
              name="customPrice"
              label={t('superadmin.digital.priceModalLabel')}
              rules={[{ required: true, message: t('superadmin.digital.priceRequired') }]}
            >
              <InputNumber min={0} step={10} style={{ width: '100%' }} addonBefore="€" />
            </Form.Item>
            <Button
              type="link"
              onClick={() => {
                priceForm.setFieldsValue({ customPrice: editing.listPrice });
              }}
            >
              {t('superadmin.digital.resetToListPrice')}
            </Button>
          </Form>
        )}
      </Modal>
    </Space>
  );
}
