'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Super Admin: create validation-only manual restore request (approval is a separate modal).
 */

import React, { useCallback, useEffect, useState } from 'react';
import { Modal, Alert, Button, Divider, Form, Input, Space, Typography } from 'antd';
import { DatabaseOutlined, WarningOutlined } from '@ant-design/icons';
import type { BackupRunResponseDto } from '@/api/generated/model';
import { useQueryClient } from '@tanstack/react-query';
import {
  postManualRestoreRequest,
  type RestoreRequestStatusDto,
} from '@/features/backup-dr/logic/manualRestoreApi';
import { manualRestoreErrorMessage } from '@/features/backup-dr/logic/manualRestoreErrorMessage';
import {
  defaultValidationDatabaseName,
  isValidValidationDatabaseName,
} from '@/features/backup-dr/logic/manualRestorePresentation';

export interface RestoreRequestModalProps {
  backupRun: BackupRunResponseDto;
  open: boolean;
  onClose: () => void;
  /** Fired after request is queued for second-admin approval. */
  onRequestCreated?: (result: RestoreRequestStatusDto) => void;
  t: (key: string, options?: Record<string, string | number>) => string;
}

export function RestoreRequestModal({
  backupRun,
  open,
  onClose,
  onRequestCreated,
  t,
}: RestoreRequestModalProps) {
  const { message } = useAntdApp();

  const queryClient = useQueryClient();
  const [targetDatabase, setTargetDatabase] = useState('');
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setTargetDatabase(defaultValidationDatabaseName());
    setReason('');
  }, [open, backupRun.id]);

  const backupRunId = backupRun.id ?? '';

  const handleRequest = useCallback(async () => {
    if (!backupRunId) {
      message.error(t('backupDr.manualRestore.errors.missingRunId'));
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
        backupRunId,
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
  }, [backupRunId, onClose, onRequestCreated, queryClient, reason, t, targetDatabase]);

  return (
    <Modal
      title={t('backupDr.manualRestore.title')}
      open={open}
      onCancel={onClose}
      width={560}
      destroyOnHidden
      footer={
        <Space>
          <Button onClick={onClose}>{t('backupDr.manualRestore.actions.close')}</Button>
          <Button type="primary" danger loading={submitting} onClick={() => void handleRequest()}>
            {t('backupDr.manualRestore.actions.submitRequest')}
          </Button>
        </Space>
      }
    >
      <Alert
        type="warning"
        showIcon
        title={t('backupDr.manualRestore.alert.title')}
        description={t('backupDr.manualRestore.alert.description')}
      />

      <Form layout="vertical" style={{ marginTop: 16 }}>
        <Form.Item label={t('backupDr.manualRestore.fields.backup')} required>
          <Input value={backupRunId} disabled />
        </Form.Item>
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

      <Divider />
      <div className="bg-yellow-50 p-3 rounded">
        <Typography.Text className="text-sm">
          <WarningOutlined className="text-yellow-600 mr-2" />
          {t('backupDr.manualRestore.tokenHint')}
        </Typography.Text>
      </div>
    </Modal>
  );
}
