'use client';

import { Checkbox, Divider, Empty, Modal, Space, Tag, Typography } from 'antd';
import React, { useEffect, useMemo, useState } from 'react';

import { useI18n } from '@/i18n';

export type UserTenantAssignmentRow = {
  id: string;
  name: string;
  slug: string;
  role?: string;
  isOwner?: boolean;
};

export type UserTenantAssignmentModalProps = {
  open: boolean;
  userEmail: string;
  currentTenants: UserTenantAssignmentRow[];
  allTenants: Array<{
    id: string;
    name: string;
    slug: string;
    status: string;
    isActive: boolean;
  }>;
  confirmLoading?: boolean;
  cancelText?: string;
  initialSelectedTenantIds?: string[];
  onClose: () => void;
  onSave: (selectedTenantIds: string[]) => void;
};

/** Mandant atamalarını görüntüler ve aktif mandant seçimini tek modalda toplar. */
export function UserTenantAssignmentModal({
  open,
  userEmail,
  currentTenants,
  allTenants,
  confirmLoading = false,
  cancelText,
  initialSelectedTenantIds,
  onClose,
  onSave,
}: UserTenantAssignmentModalProps) {
  const { t } = useI18n();
  const [selectedTenantIds, setSelectedTenantIds] = useState<string[]>([]);

  const hasExistingTenants = currentTenants.length > 0;
  const currentTenantsById = useMemo(
    () => new Map(currentTenants.map((tenant) => [tenant.id, tenant])),
    [currentTenants]
  );
  const activeTenants = useMemo(
    () => allTenants.filter((tenant) => tenant.isActive && tenant.status === 'active'),
    [allTenants]
  );

  useEffect(() => {
    if (!open) return;
    setSelectedTenantIds(initialSelectedTenantIds ?? currentTenants.map((tenant) => tenant.id));
  }, [open, currentTenants, initialSelectedTenantIds]);

  return (
    <Modal
      title={t('users.tenants.manageTitle')}
      open={open}
      onCancel={onClose}
      onOk={() => onSave(selectedTenantIds)}
      confirmLoading={confirmLoading}
      okText={t('users.tenants.manageSave')}
      cancelText={cancelText ?? t('users.tenants.manageCancel')}
      width={520}
      destroyOnHidden
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {hasExistingTenants
          ? `Benutzer ${userEmail} ist aktuell folgenden Mandanten zugeordnet.`
          : `Optional können Sie jetzt Mandanten für ${userEmail} auswählen.`}
      </Typography.Paragraph>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {t('users.tenants.manageHint')}
      </Typography.Paragraph>

      {hasExistingTenants ? (
        <>
          <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
            {t('users.tabs.tenant.columnTenant')}
          </Typography.Text>
          <Space orientation="vertical" size={8} style={{ width: '100%' }}>
            {currentTenants.map((tenant) => (
              <div key={tenant.id}>
                <Typography.Text strong>{tenant.name}</Typography.Text>
                <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                  ({tenant.slug})
                </Typography.Text>
                {tenant.role ? (
                  <Tag color="blue" style={{ marginInlineStart: 8 }}>
                    {tenant.role}
                  </Tag>
                ) : null}
                {tenant.isOwner ? (
                  <Tag color="geekblue" style={{ marginInlineStart: 8 }}>
                    {t('users.tabs.tenant.ownerBadge')}
                  </Tag>
                ) : null}
              </div>
            ))}
          </Space>
          <Divider style={{ margin: '16px 0' }} />
        </>
      ) : null}

      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        {activeTenants.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={t('users.tenants.platformOnly')}
          />
        ) : (
          activeTenants.map((tenant) => (
            <Checkbox
              key={tenant.id}
              checked={selectedTenantIds.includes(tenant.id)}
              onChange={(event) =>
                setSelectedTenantIds((prev) =>
                  event.target.checked
                    ? Array.from(new Set([...prev, tenant.id]))
                    : prev.filter((id) => id !== tenant.id)
                )
              }
            >
              {tenant.name} <Typography.Text type="secondary">({tenant.slug})</Typography.Text>
              {currentTenantsById.get(tenant.id)?.role ? (
                <Tag color="blue" style={{ marginInlineStart: 8 }}>
                  {currentTenantsById.get(tenant.id)?.role}
                </Tag>
              ) : null}
              {currentTenantsById.get(tenant.id)?.isOwner ? (
                <Tag color="geekblue" style={{ marginInlineStart: 8 }}>
                  {t('users.tabs.tenant.ownerBadge')}
                </Tag>
              ) : null}
            </Checkbox>
          ))
        )}
      </Space>
    </Modal>
  );
}
