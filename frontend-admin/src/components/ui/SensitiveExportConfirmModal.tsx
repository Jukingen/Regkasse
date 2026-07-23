'use client';

import { DownloadOutlined, StopOutlined } from '@ant-design/icons';
import { Alert, Button, Checkbox, Modal, Space, Typography } from 'antd';
import { useEffect, useState } from 'react';

import { useI18n } from '@/i18n/I18nProvider';

export type SensitiveExportConfirmModalProps = {
  open: boolean;
  confirmLoading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
};

/**
 * Privacy acknowledgement before sensitive exports (GDPR / system backup / audit).
 */
export function SensitiveExportConfirmModal({
  open,
  confirmLoading,
  onConfirm,
  onCancel,
}: SensitiveExportConfirmModalProps) {
  const { t } = useI18n();
  const [acked, setAcked] = useState(false);

  useEffect(() => {
    if (!open) setAcked(false);
  }, [open]);

  return (
    <Modal
      open={open}
      title={t('common.sensitiveExport.title')}
      onCancel={onCancel}
      destroyOnHidden
      footer={
        <Space>
          <Button icon={<StopOutlined />} onClick={onCancel}>
            {t('common.sensitiveExport.cancel')}
          </Button>
          <Button
            type="primary"
            icon={<DownloadOutlined />}
            disabled={!acked}
            loading={confirmLoading}
            onClick={onConfirm}
          >
            {t('common.sensitiveExport.download')}
          </Button>
        </Space>
      }
    >
      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 16 }}
        message={t('common.sensitiveExport.banner')}
        description={
          <Typography.Paragraph style={{ marginBottom: 0 }}>
            {t('common.sensitiveExport.body')}
          </Typography.Paragraph>
        }
      />
      <Checkbox checked={acked} onChange={(e) => setAcked(e.target.checked)}>
        {t('common.sensitiveExport.ackLabel')}
      </Checkbox>
    </Modal>
  );
}
