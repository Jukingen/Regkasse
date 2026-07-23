'use client';

import { CloseOutlined, SendOutlined } from '@ant-design/icons';
import { Alert, Button, Checkbox, DatePicker, Form, Input, Modal, Space, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import { useEffect, useMemo, useState } from 'react';

import { useSendExportEmail } from '@/features/export-email/api/exportEmailApi';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { formatMobileFileSize } from '@/lib/download/mobileDownload';

/** Align with backend ExportEmail:MaxAttachmentBytes (10 MiB). */
const EMAIL_ATTACHMENT_MAX_BYTES = 10 * 1024 * 1024;

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export type SendExportEmailModalProps = {
  open: boolean;
  onClose: () => void;
  fileName: string;
  /** Known or estimated size for link-vs-attachment hint. */
  sizeBytes?: number;
  defaultTo?: string;
  defaultSubject?: string;
  sourceKind?: string | null;
  sourceId?: string | null;
  /** Resolve file bytes just before send (required unless sourceKind+sourceId). */
  resolveContent?: () => Promise<Blob>;
  onSent?: () => void;
};

/**
 * Modal: send export as email (attachment or timed download link for large files).
 */
export function SendExportEmailModal({
  open,
  onClose,
  fileName,
  sizeBytes,
  defaultTo = '',
  defaultSubject,
  sourceKind,
  sourceId,
  resolveContent,
  onSent,
}: SendExportEmailModalProps) {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const sendMutation = useSendExportEmail();
  const [to, setTo] = useState(defaultTo);
  const [subject, setSubject] = useState('');
  const [message, setMessage] = useState('');
  const [scheduleEnabled, setScheduleEnabled] = useState(false);
  const [scheduledAt, setScheduledAt] = useState<Dayjs | null>(null);
  const [preferLink, setPreferLink] = useState(false);

  const large =
    sizeBytes != null && Number.isFinite(sizeBytes) && sizeBytes > EMAIL_ATTACHMENT_MAX_BYTES;
  const sizeHint =
    sizeBytes != null && Number.isFinite(sizeBytes)
      ? formatMobileFileSize(sizeBytes, formatLocale)
      : null;

  const subjectDefault = useMemo(
    () =>
      defaultSubject?.trim() ||
      t('common.exportEmail.defaultSubject', { fileName }),
    [defaultSubject, fileName, t]
  );

  useEffect(() => {
    if (!open) return;
    setTo(defaultTo);
    setSubject(subjectDefault);
    setMessage('');
    setScheduleEnabled(false);
    setScheduledAt(null);
    setPreferLink(large);
  }, [open, defaultTo, subjectDefault, large]);

  const canSubmit =
    EMAIL_RE.test(to.trim()) &&
    subject.trim().length > 0 &&
    (!scheduleEnabled || scheduledAt != null) &&
    (Boolean(resolveContent) || Boolean(sourceKind && sourceId));

  const handleSend = async () => {
    if (!canSubmit) return;
    try {
      let blob: Blob | null = null;
      if (resolveContent) {
        blob = await resolveContent();
      }

      const result = await sendMutation.mutateAsync({
        to: to.trim(),
        subject: subject.trim(),
        message: message.trim() || undefined,
        scheduledForUtc: scheduleEnabled && scheduledAt ? scheduledAt.toISOString() : null,
        sourceKind: sourceKind ?? undefined,
        sourceId: sourceId ?? undefined,
        preferLink: preferLink || large,
        blob,
        fileName,
      });

      if (result.status === 'scheduled') {
        notify.success(t('common.exportEmail.scheduledSuccess'));
      } else if (result.status === 'sent') {
        notify.success(
          result.deliveryMode === 'link'
            ? t('common.exportEmail.sentLinkSuccess')
            : t('common.exportEmail.sentAttachmentSuccess')
        );
      } else if (result.status === 'failed') {
        notify.error(t('common.exportEmail.sendFailed'));
        return;
      } else {
        notify.success(t('common.exportEmail.sentAttachmentSuccess'));
      }

      onSent?.();
      onClose();
    } catch {
      notify.error(t('common.exportEmail.sendFailed'));
    }
  };

  return (
    <Modal
      open={open}
      title={t('common.exportEmail.title')}
      onCancel={sendMutation.isPending ? undefined : onClose}
      destroyOnHidden
      maskClosable={!sendMutation.isPending}
      footer={
        <Space wrap style={{ width: '100%', justifyContent: 'flex-end' }}>
          <Button
            type="primary"
            icon={<SendOutlined />}
            loading={sendMutation.isPending}
            disabled={!canSubmit}
            onClick={() => void handleSend()}
          >
            {t('common.exportEmail.send')}
          </Button>
          <Button
            icon={<CloseOutlined />}
            onClick={onClose}
            disabled={sendMutation.isPending}
          >
            {t('common.exportEmail.cancel')}
          </Button>
        </Space>
      }
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        {(large || preferLink) && (
          <Alert
            type="info"
            showIcon
            title={t('common.exportEmail.linkModeTitle')}
            description={t('common.exportEmail.linkModeBody', {
              size: sizeHint ?? '—',
            })}
          />
        )}

        <Typography.Text type="secondary">
          {t('common.exportEmail.fileLabel')}: {fileName}
          {sizeHint ? ` (${sizeHint})` : ''}
        </Typography.Text>

        <Form layout="vertical">
          <Form.Item label={t('common.exportEmail.to')} required>
            <Input
              value={to}
              onChange={(e) => setTo(e.target.value)}
              placeholder="admin@example.at"
              autoComplete="email"
              disabled={sendMutation.isPending}
            />
          </Form.Item>
          <Form.Item label={t('common.exportEmail.subject')} required>
            <Input
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              disabled={sendMutation.isPending}
            />
          </Form.Item>
          <Form.Item label={t('common.exportEmail.message')}>
            <Input.TextArea
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              rows={4}
              disabled={sendMutation.isPending}
            />
          </Form.Item>
          <Form.Item>
            <Checkbox
              checked={preferLink}
              onChange={(e) => setPreferLink(e.target.checked)}
              disabled={sendMutation.isPending || large}
            >
              {t('common.exportEmail.preferLink')}
            </Checkbox>
          </Form.Item>
          <Form.Item>
            <Checkbox
              checked={scheduleEnabled}
              onChange={(e) => setScheduleEnabled(e.target.checked)}
              disabled={sendMutation.isPending}
            >
              {t('common.exportEmail.schedule')}
            </Checkbox>
          </Form.Item>
          {scheduleEnabled ? (
            <Form.Item label={t('common.exportEmail.scheduleAt')} required>
              <DatePicker
                showTime
                style={{ width: '100%' }}
                value={scheduledAt}
                onChange={(v) => setScheduledAt(v)}
                disabled={sendMutation.isPending}
              />
            </Form.Item>
          ) : null}
        </Form>
      </Space>
    </Modal>
  );
}
