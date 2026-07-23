'use client';

import { Alert, Button, Checkbox, Input, Modal, Space, Typography } from 'antd';
import { useEffect, useState } from 'react';

import { useI18n } from '@/i18n/I18nProvider';

export type CriticalActionConfirmPayload = {
  /** Present when requireSecondAuth was true and the operator entered a 2FA code. */
  secondAuthCode?: string;
};

export type CriticalActionModalProps = {
  open: boolean;
  title: string;
  description: string;
  warning: string;
  /** Exact phrase the operator must type (case-sensitive). */
  confirmText: string;
  requireSecondAuth: boolean;
  confirmLoading?: boolean;
  onConfirm: (payload: CriticalActionConfirmPayload) => void;
  onCancel: () => void;
};

/**
 * Two-step confirmation for irreversible tenant / admin actions.
 * Step 1: warning + checkbox + typed confirm phrase.
 * Step 2 (optional): 2FA code (or note that Super Admin can approve offline).
 */
export function CriticalActionModal({
  open,
  title,
  description,
  warning,
  confirmText,
  requireSecondAuth,
  confirmLoading = false,
  onConfirm,
  onCancel,
}: CriticalActionModalProps) {
  const { t } = useI18n();
  const [step, setStep] = useState(1);
  const [confirmation, setConfirmation] = useState('');
  const [agreed, setAgreed] = useState(false);
  const [secondAuthCode, setSecondAuthCode] = useState('');

  useEffect(() => {
    if (!open) {
      setStep(1);
      setConfirmation('');
      setAgreed(false);
      setSecondAuthCode('');
    }
  }, [open]);

  const step1Ready = agreed && confirmation === confirmText;
  const step2Ready = secondAuthCode.trim().length >= 6;

  const handleCancel = () => {
    if (confirmLoading) return;
    onCancel();
  };

  if (step === 2 && requireSecondAuth) {
    return (
      <Modal
        title={t('common.criticalAction.secondAuthTitle')}
        open={open}
        onCancel={handleCancel}
        footer={null}
        width={500}
        destroyOnHidden
        maskClosable={!confirmLoading}
        keyboard={!confirmLoading}
      >
        <Alert
          type="warning"
          showIcon
          title={t('common.criticalAction.secondAuthAlertTitle')}
          description={t('common.criticalAction.secondAuthAlertBody')}
          style={{ marginBottom: 16 }}
        />
        <Input
          value={secondAuthCode}
          onChange={(e) => setSecondAuthCode(e.target.value.replace(/\D/g, '').slice(0, 8))}
          placeholder={t('common.criticalAction.secondAuthPlaceholder')}
          maxLength={8}
          autoComplete="one-time-code"
          inputMode="numeric"
          disabled={confirmLoading}
        />
        <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
          {t('common.criticalAction.superAdminHint')}
        </Typography.Text>
        <Space style={{ marginTop: 16, width: '100%', justifyContent: 'flex-end' }}>
          <Button onClick={() => setStep(1)} disabled={confirmLoading}>
            {t('common.criticalAction.back')}
          </Button>
          <Button onClick={handleCancel} disabled={confirmLoading}>
            {t('common.buttons.cancel')}
          </Button>
          <Button
            type="primary"
            danger
            disabled={!step2Ready}
            loading={confirmLoading}
            onClick={() => onConfirm({ secondAuthCode: secondAuthCode.trim() })}
          >
            {t('common.criticalAction.confirmAction')}
          </Button>
        </Space>
      </Modal>
    );
  }

  return (
    <Modal
      title={title}
      open={open}
      onCancel={handleCancel}
      footer={null}
      width={600}
      destroyOnHidden
      maskClosable={!confirmLoading}
      keyboard={!confirmLoading}
    >
      <Alert
        type="error"
        showIcon
        title={t('common.criticalAction.alertTitle')}
        description={warning}
        style={{ marginBottom: 16 }}
      />
      <Typography.Paragraph style={{ marginBottom: 16 }}>{description}</Typography.Paragraph>
      <div
        style={{
          marginBottom: 16,
          padding: 16,
          background: 'var(--ant-color-warning-bg, #fffbe6)',
          border: '1px solid var(--ant-color-warning-border, #ffe58f)',
          borderRadius: 8,
        }}
      >
        <Checkbox
          checked={agreed}
          disabled={confirmLoading}
          onChange={(e) => setAgreed(e.target.checked)}
        >
          {t('common.criticalAction.irreversibleAck')}
        </Checkbox>
      </div>
      <Input
        placeholder={t('common.criticalAction.typeToConfirm', { phrase: confirmText })}
        value={confirmation}
        disabled={confirmLoading}
        onChange={(e) => setConfirmation(e.target.value)}
        autoComplete="off"
      />
      <Space style={{ marginTop: 16, width: '100%', justifyContent: 'flex-end' }}>
        <Button onClick={handleCancel} disabled={confirmLoading}>
          {t('common.buttons.cancel')}
        </Button>
        <Button
          type="primary"
          danger
          disabled={!step1Ready}
          loading={!requireSecondAuth && confirmLoading}
          onClick={() => {
            if (requireSecondAuth) {
              setStep(2);
              return;
            }
            onConfirm({});
          }}
        >
          {requireSecondAuth ? t('common.criticalAction.next') : confirmText}
        </Button>
      </Space>
    </Modal>
  );
}
