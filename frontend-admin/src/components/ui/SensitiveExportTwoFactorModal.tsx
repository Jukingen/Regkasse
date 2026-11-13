'use client';

import { Modal, Input, Typography, Space, Button } from 'antd';
import { useEffect, useState } from 'react';

import { useI18n } from '@/i18n/I18nProvider';

export type SensitiveExportTwoFactorModalProps = {
  open: boolean;
  confirmLoading?: boolean;
  onConfirm: (code: string) => void;
  onCancel: () => void;
};

export function SensitiveExportTwoFactorModal({
  open,
  confirmLoading,
  onConfirm,
  onCancel,
}: SensitiveExportTwoFactorModalProps) {
  const { t } = useI18n();
  const [code, setCode] = useState('');

  useEffect(() => {
    if (!open) setCode('');
  }, [open]);

  return (
    <Modal
      open={open}
      title={t('common.sensitiveExport.twoFactorTitle')}
      onCancel={onCancel}
      destroyOnHidden
      footer={
        <Space>
          <Button onClick={onCancel}>{t('common.sensitiveExport.cancel')}</Button>
          <Button
            type="primary"
            disabled={code.trim().length < 6}
            loading={confirmLoading}
            onClick={() => onConfirm(code.trim())}
          >
            {t('common.sensitiveExport.twoFactorConfirm')}
          </Button>
        </Space>
      }
    >
      <Typography.Paragraph>{t('common.sensitiveExport.twoFactorBody')}</Typography.Paragraph>
      <Input
        value={code}
        onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 8))}
        placeholder={t('common.sensitiveExport.twoFactorPlaceholder')}
        maxLength={8}
        autoComplete="one-time-code"
        inputMode="numeric"
      />
    </Modal>
  );
}
