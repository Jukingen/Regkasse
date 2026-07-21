'use client';

/**
 * Super Admin: RKSV-compliant validation restore request modal.
 * Requires dual acknowledgement before enqueueing dual-approval restore
 * into an isolated restore_validation_* database (never production).
 */
import { DatabaseOutlined, WarningOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Checkbox, Divider, Form, Input, Modal, Space, Typography } from 'antd';
import React, { useCallback, useEffect, useState } from 'react';

import {
  type RestoreRequestStatusDto,
  postManualRestoreRequest,
} from '@/features/backup-dr/logic/manualRestoreApi';
import { manualRestoreErrorMessage } from '@/features/backup-dr/logic/manualRestoreErrorMessage';
import {
  defaultValidationDatabaseName,
  isValidValidationDatabaseName,
} from '@/features/backup-dr/logic/manualRestorePresentation';
import { RestorePreview } from '@/features/backup/components/RestorePreview';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

export interface RestoreModalBackup {
  backupRunId: string;
  /** Optional display name (file or run label). */
  fileName?: string | null;
  /** Access-scope label when present; shared dumps may omit this. */
  tenantSlug?: string | null;
  /** Operating / source tenant id when known (same-tenant gate). */
  tenantId?: string | null;
  backupDate?: string | null;
}

export interface RestoreModalProps {
  backup: RestoreModalBackup;
  open: boolean;
  onClose: () => void;
  /** Fired after request is queued for second-admin approval. */
  onRequestCreated?: (result: RestoreRequestStatusDto) => void;
}

export function RestoreModal({ backup, open, onClose, onRequestCreated }: RestoreModalProps) {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const queryClient = useQueryClient();

  const [targetDatabase, setTargetDatabase] = useState('');
  const [reason, setReason] = useState('');
  const [confirmedSameTenant, setConfirmedSameTenant] = useState(false);
  const [acknowledgedRksv, setAcknowledgedRksv] = useState(false);
  const [complianceOk, setComplianceOk] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setTargetDatabase(defaultValidationDatabaseName());
    setReason('');
    setConfirmedSameTenant(false);
    setAcknowledgedRksv(false);
    setComplianceOk(false);
  }, [open, backup.backupRunId]);

  const canSubmit =
    confirmedSameTenant && acknowledgedRksv && complianceOk && Boolean(backup.backupRunId);

  const handleRestore = useCallback(async () => {
    if (!backup.backupRunId) {
      message.error(t('backupDr.manualRestore.errors.missingRunId'));
      return;
    }
    if (!confirmedSameTenant || !acknowledgedRksv) {
      message.error(t('backupDr.manualRestore.errors.acknowledgementsRequired'));
      return;
    }
    if (!complianceOk) {
      message.error(t('backupDr.manualRestore.errors.complianceRequired'));
      return;
    }
    const db = targetDatabase.trim().toLowerCase();
    if (!isValidValidationDatabaseName(db)) {
      message.error(t('backupDr.manualRestore.errors.invalidTargetDb'));
      return;
    }

    setSubmitting(true);
    try {
      const result = await postManualRestoreRequest({
        backupRunId: backup.backupRunId,
        targetDatabaseName: db,
        reason: reason.trim() || undefined,
        validationOnly: true,
      });
      message.success(t('backupDr.manualRestore.messages.requestCreated'));
      void queryClient.invalidateQueries({ queryKey: ['/api/admin/restore'] });
      onRequestCreated?.(result);
      onClose();
    } catch (err) {
      message.error(manualRestoreErrorMessage(err, t));
    } finally {
      setSubmitting(false);
    }
  }, [
    acknowledgedRksv,
    backup.backupRunId,
    complianceOk,
    confirmedSameTenant,
    message,
    onClose,
    onRequestCreated,
    queryClient,
    reason,
    t,
    targetDatabase,
  ]);

  return (
    <Modal
      title={t('backupDr.manualRestore.restoreModal.title')}
      open={open}
      onCancel={onClose}
      width={800}
      destroyOnHidden
      footer={
        <Space>
          <Button key="cancel" onClick={onClose}>
            {t('backupDr.manualRestore.actions.cancel')}
          </Button>
          <Button
            key="restore"
            type="primary"
            danger
            loading={submitting}
            disabled={!canSubmit}
            onClick={() => void handleRestore()}
          >
            {t('backupDr.manualRestore.actions.restore')}
          </Button>
        </Space>
      }
    >
      <Alert
        type="warning"
        showIcon
        title={t('backupDr.manualRestore.rksvAlert.title')}
        description={
          <ul style={{ margin: '8px 0 0', paddingLeft: 18 }}>
            <li>{t('backupDr.manualRestore.rksvAlert.sameTenant')}</li>
            <li>{t('backupDr.manualRestore.rksvAlert.originalTimestamps')}</li>
            <li>{t('backupDr.manualRestore.rksvAlert.auditLogged')}</li>
            <li>{t('backupDr.manualRestore.rksvAlert.validationOnly')}</li>
          </ul>
        }
      />

      <div style={{ marginTop: 16 }}>
        <RestorePreview
          backup={{
            id: backup.backupRunId,
            fileName: backup.fileName,
            tenantId: backup.tenantId,
            tenantName: backup.tenantSlug,
            backupDate: backup.backupDate,
          }}
          enabled={open}
          onComplianceChange={setComplianceOk}
        />
      </div>

      <Form layout="vertical" style={{ marginTop: 16 }}>
        <Form.Item label={t('backupDr.manualRestore.fields.backup')} required>
          <Input value={backup.backupRunId} disabled />
        </Form.Item>
        {backup.fileName || backup.tenantSlug ? (
          <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
            {backup.fileName ? (
              <span>
                {t('backupDr.manualRestore.fields.fileName')}: {backup.fileName}
              </span>
            ) : null}
            {backup.tenantSlug ? (
              <span>
                {backup.fileName ? ' · ' : null}
                {t('backupDr.manualRestore.fields.tenant')}: {backup.tenantSlug}
              </span>
            ) : (
              <span>
                {backup.fileName ? ' · ' : null}
                {t('backupDr.manualRestore.fields.sharedDump')}
              </span>
            )}
          </Typography.Paragraph>
        ) : null}
        <Form.Item
          label={t('backupDr.manualRestore.fields.targetDatabase')}
          required
          help={t('backupDr.manualRestore.fields.targetDatabaseHelp')}
        >
          <Input
            placeholder="restore_validation_20241231"
            value={targetDatabase}
            onChange={(e) => setTargetDatabase(e.target.value)}
            prefix={<DatabaseOutlined />}
          />
        </Form.Item>
        <Form.Item label={t('backupDr.manualRestore.fields.reason')}>
          <Input.TextArea
            rows={3}
            placeholder={t('backupDr.manualRestore.fields.reasonPlaceholder')}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
        </Form.Item>
      </Form>

      <div style={{ marginTop: 8 }}>
        <Checkbox
          checked={confirmedSameTenant}
          onChange={(e) => setConfirmedSameTenant(e.target.checked)}
        >
          {t('backupDr.manualRestore.acknowledgements.sameTenant')}
        </Checkbox>
      </div>
      <div style={{ marginTop: 8 }}>
        <Checkbox
          checked={acknowledgedRksv}
          onChange={(e) => setAcknowledgedRksv(e.target.checked)}
        >
          {t('backupDr.manualRestore.acknowledgements.rksvUnderstood')}
        </Checkbox>
      </div>

      <Divider />
      <Typography.Text type="secondary" style={{ fontSize: 13 }}>
        <WarningOutlined style={{ marginRight: 8 }} />
        {t('backupDr.manualRestore.tokenHint')}
      </Typography.Text>
    </Modal>
  );
}
