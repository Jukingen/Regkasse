'use client';

import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Descriptions, Form, Input, Modal, Space, Spin, Typography } from 'antd';
/**
 * Second Super Admin: approve/reject manual restore with 6-digit OTP token.
 */
import React, { useCallback, useEffect, useRef, useState } from 'react';

import { CardSkeleton } from '@/components/Skeleton';
import { useManualRestoreStatusPoll } from '@/features/backup-dr/hooks/useManualRestoreStatusPoll';
import {
  type RestoreRequestStatusDto,
  postManualRestoreApproval,
} from '@/features/backup-dr/logic/manualRestoreApi';
import { manualRestoreErrorMessage } from '@/features/backup-dr/logic/manualRestoreErrorMessage';
import { useAntdApp } from '@/hooks/useAntdApp';

export interface RestoreApprovalModalProps {
  requestId: string;
  open: boolean;
  /** Called when validation restore completes successfully. */
  onApproved: () => void;
  onClose: () => void;
  t: (key: string, options?: Record<string, string | number>) => string;
}

const POLL_MS = 2000;

function isCompletedStatus(status: string | undefined): boolean {
  return (status ?? '').toLowerCase() === 'completed';
}

function isExecutingStatus(status: string | undefined): boolean {
  const s = (status ?? '').toLowerCase();
  return s === 'executing' || s === 'approved';
}

function isFailedStatus(status: string | undefined): boolean {
  const s = (status ?? '').toLowerCase();
  return s === 'failed' || s === 'rejected';
}

export function RestoreApprovalModal({
  requestId,
  open,
  onApproved,
  onClose,
  t,
}: RestoreApprovalModalProps) {
  const { message, modal } = useAntdApp();

  const queryClient = useQueryClient();
  const [token, setToken] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const approvedNotifiedRef = useRef(false);

  const statusQuery = useManualRestoreStatusPoll(requestId, open, POLL_MS, true);
  const status: RestoreRequestStatusDto | null | undefined = statusQuery.data;

  useEffect(() => {
    if (!open) {
      setToken('');
      approvedNotifiedRef.current = false;
    }
  }, [open, requestId]);

  useEffect(() => {
    if (!open || !status?.status || approvedNotifiedRef.current) return;
    if (isCompletedStatus(status.status)) {
      approvedNotifiedRef.current = true;
      void queryClient.invalidateQueries({ queryKey: ['/api/admin/restore'] });
      void queryClient.invalidateQueries({ queryKey: ['/api/admin/restore-verification'] });
      void queryClient.invalidateQueries({ queryKey: ['/api/admin/backup'] });
      onApproved();
    }
  }, [open, onApproved, queryClient, status?.status]);

  const handleApprove = useCallback(async () => {
    if (token.length !== 6) {
      message.error(t('backupDr.manualRestore.errors.invalidToken'));
      return;
    }
    setSubmitting(true);
    try {
      await postManualRestoreApproval(requestId, {
        approvalToken: token,
        action: 'approve',
      });
      message.success(t('backupDr.manualRestore.messages.approvalSubmitted'));
      void statusQuery.refetch();
    } catch (err) {
      message.error(manualRestoreErrorMessage(err, t));
    } finally {
      setSubmitting(false);
    }
  }, [requestId, statusQuery, t, token]);

  const handleReject = useCallback(() => {
    if (token.length !== 6) {
      message.error(t('backupDr.manualRestore.errors.invalidToken'));
      return;
    }
    let rejectReason = '';
    modal.confirm({
      title: t('backupDr.manualRestore.approvalModal.rejectTitle'),
      content: (
        <Input.TextArea
          rows={3}
          placeholder={t('backupDr.manualRestore.fields.rejectReason')}
          onChange={(e) => {
            rejectReason = e.target.value;
          }}
        />
      ),
      okText: t('backupDr.manualRestore.actions.confirmReject'),
      cancelText: t('backupDr.manualRestore.actions.close'),
      okButtonProps: { danger: true },
      onOk: async () => {
        if (!rejectReason.trim()) {
          message.error(t('backupDr.manualRestore.errors.rejectReasonRequired'));
          return Promise.reject();
        }
        await postManualRestoreApproval(requestId, {
          approvalToken: token,
          action: 'reject',
          reason: rejectReason.trim(),
        });
        message.info(t('backupDr.manualRestore.messages.rejected'));
        void statusQuery.refetch();
        onClose();
      },
    });
  }, [onClose, requestId, statusQuery, t, token]);

  const showExecuting = isExecutingStatus(status?.status);
  const showSuccess = isCompletedStatus(status?.status);
  const showFailed = isFailedStatus(status?.status);

  const displayReason =
    status?.reason?.trim() || (showFailed ? status?.rejectionReason : null) || '—';

  return (
    <Modal
      title={t('backupDr.manualRestore.approvalModal.title')}
      open={open}
      closable={false}
      mask={{ closable: false }}
      onCancel={onClose}
      footer={
        <Space>
          <Button danger onClick={() => void handleReject()} disabled={submitting || showExecuting}>
            {t('backupDr.manualRestore.approvalModal.reject')}
          </Button>
          <Button
            type="primary"
            onClick={() => void handleApprove()}
            disabled={token.length !== 6 || submitting || showExecuting || showSuccess}
            loading={submitting}
          >
            {t('backupDr.manualRestore.approvalModal.approveExecute')}
          </Button>
        </Space>
      }
      width={520}
      destroyOnHidden
    >
      {statusQuery.isLoading && !status ? (
        <CardSkeleton count={1} />
      ) : (
        <Descriptions bordered size="small" column={1}>
          <Descriptions.Item label={t('backupDr.manualRestore.approvalModal.requestedBy')}>
            {status?.requestedByEmail ?? status?.requestedByUserId ?? '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.manualRestore.fields.backup')}>
            {status?.backupRunId ?? '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.manualRestore.fields.targetDatabase')}>
            {status?.targetDatabaseName ?? '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.manualRestore.fields.reason')}>
            {displayReason}
          </Descriptions.Item>
        </Descriptions>
      )}

      {!showSuccess && !showFailed ? (
        <Form layout="vertical" style={{ marginTop: 16 }}>
          <Form.Item
            label={t('backupDr.manualRestore.fields.approvalToken')}
            required
            extra={t('backupDr.manualRestore.approvalModal.tokenExtra')}
          >
            <Input.OTP
              length={6}
              value={token}
              onChange={(value) => setToken(value.replace(/\D/g, '').slice(0, 6))}
              disabled={submitting || showExecuting}
            />
          </Form.Item>
        </Form>
      ) : null}

      {showExecuting ? (
        <div style={{ textAlign: 'center', marginTop: 16 }}>
          <Spin />
          <Typography.Paragraph style={{ marginTop: 8, marginBottom: 0 }}>
            {t('backupDr.manualRestore.approvalModal.executing')}
          </Typography.Paragraph>
        </div>
      ) : null}

      {showSuccess ? (
        <Alert
          type="success"
          showIcon
          style={{ marginTop: 16 }}
          title={t('backupDr.manualRestore.approvalModal.completedTitle')}
          description={status?.result ?? undefined}
        />
      ) : null}

      {showFailed && !showSuccess ? (
        <Alert
          type="error"
          showIcon
          style={{ marginTop: 16 }}
          title={t('backupDr.manualRestore.approvalModal.failedTitle')}
          description={status?.result ?? status?.rejectionReason ?? undefined}
        />
      ) : null}
    </Modal>
  );
}
