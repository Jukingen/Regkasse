'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useCallback, useEffect, useState } from 'react';
import { Alert, Form, Tabs, Typography } from 'antd';
import { AppstoreOutlined, EditOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';

import {
  adminPaymentMethodDefinitionsQueryKeys,
  createAdminPaymentMethodDefinition,
  deleteAdminPaymentMethodDefinition,
  updateAdminPaymentMethodDefinition,
  useAdminPaymentMethodDefinitionsList,
  type CreatePaymentMethodDefinitionRequest,
  type PaymentMethodDefinitionAdmin,
} from '@/api/admin/payment-method-definitions';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useCashRegisters } from '@/features/cash-registers/hooks/useCashRegisters';
import { PaymentMethodDefinitionModal } from '@/features/payment-methods/components/PaymentMethodDefinitionModal';
import { PaymentMethodMatrixOverview } from '@/features/payment-methods/components/PaymentMethodMatrixOverview';
import { PaymentMethodRegisterPanel } from '@/features/payment-methods/components/PaymentMethodRegisterPanel';
import { useAllRegistersPaymentMethods } from '@/features/payment-methods/hooks/useAllRegistersPaymentMethods';

type SettingsTab = 'overview' | 'manage';

function toRequestPayload(
  row: PaymentMethodDefinitionAdmin,
  overrides?: Partial<CreatePaymentMethodDefinitionRequest>,
): CreatePaymentMethodDefinitionRequest {
  return {
    cashRegisterId: row.cashRegisterId,
    code: row.code,
    name: row.name,
    legacyPaymentMethodValue: row.legacyPaymentMethodValue,
    fiscalCategory: row.fiscalCategory ?? null,
    isActive: row.isActive,
    isDefault: row.isDefault,
    displayOrder: row.displayOrder,
    requiresTerminal: row.requiresTerminal,
    terminalType: row.terminalType ?? null,
    allowRefund: row.allowRefund,
    icon: row.icon ?? null,
    metadataJson: row.metadataJson ?? null,
    ...overrides,
  };
}

export default function PaymentMethodsSettingsPage() {
  const { message, modal } = useAntdApp();
  const queryClient = useQueryClient();
  const { t } = useI18n();
  const { user } = useAuth();
  const { tenantId, requiresTenantSelection } = useCurrentTenant();
  const canManage = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);

  const { registers, defaultRegister, isLoading: registersLoading } = useCashRegisters(tenantId ?? undefined, {
    enabled: Boolean(tenantId) && !requiresTenantSelection,
  });

  const registerIds = registers.map((r) => r.id);
  const matrixQuery = useAllRegistersPaymentMethods(registerIds, Boolean(tenantId) && !requiresTenantSelection);

  const [activeTab, setActiveTab] = useState<SettingsTab>('overview');
  const [selectedRegisterId, setSelectedRegisterId] = useState<string | null>(null);

  useEffect(() => {
    if (!tenantId || registers.length === 0) {
      setSelectedRegisterId(null);
      return;
    }
    if (selectedRegisterId && registers.some((r) => r.id === selectedRegisterId)) {
      return;
    }
    setSelectedRegisterId(defaultRegister?.id ?? registers[0]?.id ?? null);
  }, [tenantId, registers, defaultRegister, selectedRegisterId]);

  const cashRegisterId = selectedRegisterId ?? defaultRegister?.id ?? registers[0]?.id ?? null;

  const listQuery = useAdminPaymentMethodDefinitionsList(cashRegisterId);

  const [modalOpen, setModalOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editing, setEditing] = useState<PaymentMethodDefinitionAdmin | null>(null);
  const [modalRegisterId, setModalRegisterId] = useState<string | null>(null);
  const [form] = Form.useForm<CreatePaymentMethodDefinitionRequest>();

  const invalidateAll = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: adminPaymentMethodDefinitionsQueryKeys.lists() });
    await matrixQuery.refetchAll();
  }, [matrixQuery, queryClient]);

  const openCreate = () => {
    if (!cashRegisterId) return;
    setModalRegisterId(cashRegisterId);
    setEditing(null);
    form.setFieldsValue({
      cashRegisterId,
      code: '',
      name: '',
      legacyPaymentMethodValue: 1,
      fiscalCategory: '',
      isActive: true,
      isDefault: false,
      displayOrder: 100,
      requiresTerminal: false,
      terminalType: '',
      allowRefund: true,
      icon: '',
      metadataJson: '',
    });
    setModalOpen(true);
  };

  const openEdit = (row: PaymentMethodDefinitionAdmin) => {
    setModalRegisterId(row.cashRegisterId);
    setEditing(row);
    if (row.cashRegisterId !== cashRegisterId) {
      setSelectedRegisterId(row.cashRegisterId);
    }
    form.setFieldsValue(toRequestPayload(row, { fiscalCategory: row.fiscalCategory ?? '', icon: row.icon ?? '', metadataJson: row.metadataJson ?? '', terminalType: row.terminalType ?? '' }));
    setModalOpen(true);
  };

  const handleSubmit = async (payload: CreatePaymentMethodDefinitionRequest) => {
    const targetRegisterId = modalRegisterId ?? cashRegisterId;
    if (!targetRegisterId) return;
    setSaving(true);
    try {
      const body: CreatePaymentMethodDefinitionRequest = { ...payload, cashRegisterId: targetRegisterId };
      if (editing) {
        await updateAdminPaymentMethodDefinition(editing.id, body);
        message.success(t('settings.paymentMethods.saved'));
      } else {
        await createAdminPaymentMethodDefinition(body);
        message.success(t('settings.paymentMethods.created'));
      }
      setModalOpen(false);
      setEditing(null);
      await invalidateAll();
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(t('settings.paymentMethods.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const handleDeactivate = (row: PaymentMethodDefinitionAdmin) => {
    modal.confirm({
      title: t('settings.paymentMethods.deactivateConfirmTitle'),
      content: t('settings.paymentMethods.deactivateConfirmBody', { code: row.code }),
      okText: t('common.buttons.yes'),
      cancelText: t('common.buttons.cancel'),
      onOk: async () => {
        await deleteAdminPaymentMethodDefinition(row.id);
        message.success(t('settings.paymentMethods.deactivated'));
        await invalidateAll();
      },
    });
  };

  const handleToggleActive = async (definition: PaymentMethodDefinitionAdmin, nextActive: boolean) => {
    if (!canManage) return;
    try {
      if (nextActive) {
        await updateAdminPaymentMethodDefinition(definition.id, toRequestPayload(definition, { isActive: true }));
        message.success(t('settings.paymentMethods.matrix.turnedOn', { code: definition.code }));
      } else {
        await deleteAdminPaymentMethodDefinition(definition.id);
        message.success(t('settings.paymentMethods.deactivated'));
      }
      await invalidateAll();
    } catch {
      message.error(t('settings.paymentMethods.saveFailed'));
    }
  };

  const handleManageRegister = (registerId: string) => {
    setSelectedRegisterId(registerId);
    setActiveTab('manage');
  };

  const headerBreadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('settings.paymentMethods.title') },
  ];

  if (requiresTenantSelection) {
    return (
      <>
        <AdminPageHeader title={t('settings.paymentMethods.title')} breadcrumbs={headerBreadcrumbs} />
        <Alert type="info" showIcon message={t('settings.paymentMethods.noCashRegister')} />
      </>
    );
  }

  return (
    <>
      <AdminPageHeader title={t('settings.paymentMethods.title')} breadcrumbs={headerBreadcrumbs} />
      <Typography.Paragraph type="secondary">{t('settings.paymentMethods.intro')}</Typography.Paragraph>

      <Tabs
        activeKey={activeTab}
        onChange={(key) => setActiveTab(key as SettingsTab)}
        destroyOnHidden={false}
        items={[
          {
            key: 'overview',
            label: (
              <span>
                <AppstoreOutlined /> {t('settings.paymentMethods.tabs.overview')}
              </span>
            ),
            children: (
              <PaymentMethodMatrixOverview
                registers={registers}
                methodsByRegisterId={matrixQuery.methodsByRegisterId}
                loading={registersLoading || matrixQuery.isLoading}
                canManage={canManage}
                onManageRegister={handleManageRegister}
                onEditDefinition={(definition) => {
                  setActiveTab('manage');
                  openEdit(definition);
                }}
                onToggleActive={handleToggleActive}
              />
            ),
          },
          {
            key: 'manage',
            label: (
              <span>
                <EditOutlined /> {t('settings.paymentMethods.tabs.manage')}
              </span>
            ),
            children: (
              <PaymentMethodRegisterPanel
                registers={registers}
                registersLoading={registersLoading}
                cashRegisterId={cashRegisterId}
                onSelectRegister={setSelectedRegisterId}
                rows={listQuery.data ?? []}
                tableLoading={listQuery.isLoading}
                canManage={canManage}
                onAdd={openCreate}
                onEdit={openEdit}
                onDeactivate={handleDeactivate}
              />
            ),
          },
        ]}
      />

      <PaymentMethodDefinitionModal
        open={modalOpen}
        editing={editing}
        cashRegisterId={modalRegisterId ?? cashRegisterId ?? ''}
        confirmLoading={saving}
        onCancel={() => setModalOpen(false)}
        onSubmit={handleSubmit}
        form={form}
      />
    </>
  );
}
