'use client';

import { Alert, Button, Checkbox, Input, Modal, Space, Steps, Typography } from 'antd';
import { useEffect, useState } from 'react';

import {
  useConfirmTenantDataDeletion,
  useRequestTenantDataDeletion,
} from '@/features/data-management/hooks/useTenantDataManagement';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

type Props = {
  tenantId: string;
  open: boolean;
  onClose: () => void;
  /** Optional seed reason from the parent panel. */
  initialReason?: string;
};

/**
 * Multi-step data-deletion request wizard (RKSV acknowledgements → submit → success).
 * Submit calls request then confirm so the 7-day purge wait starts (matches backend email flow).
 */
export function DataDeletionRequestModal({ tenantId, open, onClose, initialReason = '' }: Props) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const requestMutation = useRequestTenantDataDeletion(tenantId);
  const confirmMutation = useConfirmTenantDataDeletion(tenantId);

  const [step, setStep] = useState(0);
  const [ackRksv, setAckRksv] = useState(false);
  const [ackCommercial, setAckCommercial] = useState(false);
  const [reason, setReason] = useState(initialReason);

  useEffect(() => {
    if (!open) return;
    setStep(0);
    setAckRksv(false);
    setAckCommercial(false);
    setReason(initialReason);
  }, [open, initialReason]);

  const submitting = requestMutation.isPending || confirmMutation.isPending;
  const canProceedStep1 = ackRksv && ackCommercial;

  const handleClose = () => {
    if (submitting) return;
    onClose();
  };

  const onSubmit = async () => {
    try {
      const row = await requestMutation.mutateAsync(reason.trim() || undefined);
      await confirmMutation.mutateAsync(row.id);
      setStep(2);
    } catch {
      message.error(t('dataManagement.deleteFailed'));
    }
  };

  const footer =
    step === 0 ? (
      <Space>
        <Button onClick={handleClose}>{t('common.buttons.cancel')}</Button>
        <Button type="primary" danger disabled={!canProceedStep1} onClick={() => setStep(1)}>
          {t('dataManagement.wizard.next')}
        </Button>
      </Space>
    ) : step === 1 ? (
      <Space>
        <Button disabled={submitting} onClick={() => setStep(0)}>
          {t('dataManagement.wizard.back')}
        </Button>
        <Button type="primary" danger loading={submitting} onClick={() => void onSubmit()}>
          {t('dataManagement.wizard.submit')}
        </Button>
      </Space>
    ) : (
      <Button type="primary" onClick={handleClose}>
        {t('dataManagement.wizard.done')}
      </Button>
    );

  return (
    <Modal
      title={t('dataManagement.wizard.title')}
      open={open}
      onCancel={handleClose}
      footer={footer}
      destroyOnHidden
      maskClosable={!submitting}
      closable={!submitting}
      width={560}
    >
      <Steps
        size="small"
        current={step}
        style={{ marginBottom: 20 }}
        items={[
          { title: t('dataManagement.wizard.stepAck') },
          { title: t('dataManagement.wizard.stepReview') },
          { title: t('dataManagement.wizard.stepDone') },
        ]}
      />

      {step === 0 ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Alert
            type="warning"
            showIcon
            title={t('dataManagement.wizard.irreversibleTitle')}
            description={t('dataManagement.wizard.irreversibleBody')}
          />
          <Checkbox checked={ackRksv} onChange={(e) => setAckRksv(e.target.checked)}>
            {t('dataManagement.wizard.ackRksv')}
          </Checkbox>
          <Checkbox checked={ackCommercial} onChange={(e) => setAckCommercial(e.target.checked)}>
            {t('dataManagement.wizard.ackCommercial')}
          </Checkbox>
          <Input.TextArea
            rows={2}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t('dataManagement.reasonPlaceholder')}
            maxLength={500}
          />
        </Space>
      ) : null}

      {step === 1 ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Typography.Paragraph>{t('dataManagement.wizard.reviewPreparing')}</Typography.Paragraph>
          <Typography.Paragraph>{t('dataManagement.wizard.reviewWait')}</Typography.Paragraph>
          <Typography.Paragraph>{t('dataManagement.wizard.reviewEmail')}</Typography.Paragraph>
          <Alert type="info" showIcon title={t('dataManagement.wizard.reviewNote')} />
        </Space>
      ) : null}

      {step === 2 ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Alert
            type="success"
            showIcon
            title={t('dataManagement.wizard.successTitle')}
            description={t('dataManagement.wizard.successBody')}
          />
          <Typography.Paragraph type="secondary">
            {t('dataManagement.wizard.successRksv')}
          </Typography.Paragraph>
        </Space>
      ) : null}
    </Modal>
  );
}
