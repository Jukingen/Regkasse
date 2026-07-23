'use client';

import { Alert, Input, Modal, Typography } from 'antd';
import React, { useEffect, useState } from 'react';

import type { PermissionAuditEntry } from '@/features/audit/api/permissionAudit';
import { useI18n } from '@/i18n';

type Props = {
  open: boolean;
  entry: PermissionAuditEntry | null;
  confirmLoading?: boolean;
  newerChangesWarning?: string | null;
  onCancel: () => void;
  onConfirm: (reason: string, force: boolean) => void;
};

function stateLabel(
  state: string | null | undefined,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  if (!state) return '—';
  const key = `users.permissionAudit.state.${state}`;
  const labeled = t(key);
  return labeled === key ? state : labeled;
}

/**
 * Confirmation dialog for reverting a permission audit entry.
 */
export function PermissionAuditRevertModal({
  open,
  entry,
  confirmLoading,
  newerChangesWarning,
  onCancel,
  onConfirm,
}: Props) {
  const { t } = useI18n();
  const [reason, setReason] = useState('');

  useEffect(() => {
    if (open) setReason('');
  }, [open, entry?.id]);

  const force = Boolean(newerChangesWarning);

  return (
    <Modal
      title={t('users.permissionAudit.revertDialog.title')}
      open={open}
      onCancel={onCancel}
      okText={t('users.permissionAudit.revertDialog.confirm')}
      cancelText={t('users.permissionAudit.revertDialog.cancel')}
      okButtonProps={{ danger: true, loading: confirmLoading }}
      onOk={() => onConfirm(reason.trim(), force)}
      destroyOnHidden
    >
      <Typography.Paragraph>
        {t('users.permissionAudit.revertDialog.body')}
      </Typography.Paragraph>

      {entry ? (
        <Typography.Paragraph>
          <ul style={{ margin: 0, paddingLeft: 20 }}>
            <li>
              <Typography.Text code>{entry.permissionKey || entry.roleName}</Typography.Text>
              {' → '}
              {stateLabel(entry.newValue, t)}
              {' → '}
              {stateLabel(entry.oldValue, t)}
            </li>
          </ul>
        </Typography.Paragraph>
      ) : null}

      {newerChangesWarning ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          title={newerChangesWarning}
        />
      ) : null}

      <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
        {t('users.permissionAudit.revertDialog.reasonLabel')}
      </Typography.Text>
      <Input.TextArea
        rows={2}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        placeholder={t('users.permissionAudit.revertDialog.reasonPlaceholder')}
      />
    </Modal>
  );
}
