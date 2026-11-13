'use client';

import { StopOutlined, UndoOutlined } from '@ant-design/icons';
import { Alert, Button, Checkbox, Form, Input, Modal, Space, Typography } from 'antd';
import Link from 'next/link';
import React from 'react';

import type { CashRegister } from '@/api/generated/model';
import { GracePeriodIndicator } from '@/components/GracePeriodIndicator';
import type { GracePeriodPending } from '@/features/cash-registers/api/gracePeriods';
import {
  canDecommissionRegister,
  rawRegisterStatus,
} from '@/features/cash-registers/utils/registerStatus';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { useI18n } from '@/i18n';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';

export type DecommissionModalProps = {
  open: boolean;
  register: CashRegister | null;
  reason: string;
  onReasonChange: (value: string) => void;
  onCancel: () => void;
  onConfirm: (reason?: string) => void;
  confirmLoading?: boolean;
  /** When set, modal shows grace-period undo UI instead of confirm form. */
  gracePeriod?: GracePeriodPending | null;
  onUndoGracePeriod?: () => void;
  undoLoading?: boolean;
};

export function DecommissionModal(props: DecommissionModalProps) {
  if (!props.open) {
    return null;
  }
  return <DecommissionModalContent {...props} />;
}

function DecommissionModalContent({
  open,
  register,
  reason,
  onReasonChange,
  onCancel,
  onConfirm,
  confirmLoading,
  gracePeriod,
  onUndoGracePeriod,
  undoLoading,
}: DecommissionModalProps) {
  const { t } = useI18n();
  const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);
  const [form] = Form.useForm<{ reason?: string; confirm?: boolean }>();
  const [canUndo, setCanUndo] = React.useState(true);
  const status = register ? rawRegisterStatus(register) : undefined;
  const canProceed = canDecommissionRegister(status);
  const name = register?.location?.trim() || '—';
  const number = register?.registerNumber?.trim() || '—';
  const graceActive = Boolean(gracePeriod?.id);

  React.useEffect(() => {
    if (!open) {
      return;
    }

    form.setFieldsValue({
      reason,
      confirm: false,
    });
    setCanUndo(true);
  }, [form, open, reason, gracePeriod?.id]);

  const handleCancel = () => {
    form.resetFields();
    onCancel();
  };

  const handleSubmit = (values: { reason?: string; confirm?: boolean }) => {
    const nextReason = values.reason?.trim() ?? '';
    onReasonChange(nextReason);
    onConfirm(nextReason);
  };

  const totalSeconds = gracePeriod
    ? Math.max(
        1,
        Math.floor(
          (new Date(gracePeriod.expiresAt).getTime() - new Date(gracePeriod.createdAt).getTime()) /
            1000
        )
      )
    : undefined;

  return (
    <Modal
      title={
        <Space>
          <StopOutlined style={{ color: '#ff4d4f' }} />
          <span>{t('cashRegisters.decommission.modalTitleWithNumber', { number })}</span>
        </Space>
      }
      open={open}
      onCancel={handleCancel}
      footer={
        graceActive
          ? [
              <Button key="close" onClick={handleCancel}>
                {t('common.buttons.close')}
              </Button>,
              <Button
                key="undo"
                danger
                icon={<UndoOutlined />}
                disabled={!canUndo || !gracePeriod?.canCancel}
                loading={undoLoading}
                onClick={() => onUndoGracePeriod?.()}
              >
                {t('common.gracePeriod.undoButton')}
              </Button>,
            ]
          : [
              <Button key="cancel" onClick={handleCancel}>
                {t('cashRegisters.decommission.cancel')}
              </Button>,
              <Button
                key="submit"
                type="primary"
                danger
                loading={confirmLoading}
                disabled={!canProceed}
                onClick={() => form.submit()}
              >
                {t('cashRegisters.decommission.confirm')}
              </Button>,
            ]
      }
      forceRender
      width={500}
    >
      <Typography.Paragraph strong style={{ marginBottom: 16 }}>
        {t('cashRegisters.decommission.registerLine', { name, number })}
      </Typography.Paragraph>

      {graceActive && gracePeriod ? (
        <>
          <Alert
            type="error"
            showIcon
            style={{ marginBottom: 16 }}
            title={t('common.gracePeriod.schlussbelegTitle')}
            description={t('common.gracePeriod.schlussbelegDescription')}
          />
          <GracePeriodIndicator
            expiresAt={gracePeriod.expiresAt}
            totalSeconds={totalSeconds}
            onExpire={() => setCanUndo(false)}
            style={{ marginBottom: 16 }}
          />
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('common.gracePeriod.schlussbelegDeferredNote')}
          </Typography.Paragraph>
        </>
      ) : (
        <>
          {!canProceed ? (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 16 }}
              title={t('cashRegisters.decommission.mustCloseFirst')}
            />
          ) : null}

          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 16 }}
            title={t('cashRegisters.decommission.irreversibleWarning')}
            description={
              <ul style={{ margin: '8px 0 0 20px', padding: 0 }}>
                <li>{t('cashRegisters.decommission.warningNoPayments')}</li>
                <li>{t('cashRegisters.decommission.warningAutoReceipt')}</li>
                <li>{t('cashRegisters.decommission.warningNoRestore')}</li>
                <li>{t('cashRegisters.decommission.warningRetention')}</li>
              </ul>
            }
          />
          {canOpenSonderbelege ? (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 16 }}
              title={
                <span>
                  {t('cashRegisters.decommission.hintSchlussbeleg')}{' '}
                  <Link href="/rksv/sonderbelege?focus=schlussbeleg">
                    {t('cashRegisters.decommission.hintSchlussbelegLink')}
                  </Link>
                </span>
              }
            />
          ) : null}

          <Form
            form={form}
            layout="vertical"
            onFinish={handleSubmit}
            onValuesChange={(changedValues) => {
              if (typeof changedValues.reason === 'string') {
                onReasonChange(changedValues.reason);
              }
            }}
          >
            <Form.Item
              name="reason"
              label={t('cashRegisters.decommission.reasonLabel')}
              tooltip={t('cashRegisters.decommission.reasonTooltip')}
            >
              <Input.TextArea
                rows={3}
                placeholder={t('cashRegisters.decommission.reasonPlaceholder')}
                maxLength={450}
                disabled={!canProceed}
              />
            </Form.Item>

            <Form.Item
              name="confirm"
              valuePropName="checked"
              rules={[
                {
                  validator: async (_, value) => {
                    if (value) {
                      return;
                    }
                    throw new Error(t('cashRegisters.decommission.confirmRequired'));
                  },
                },
              ]}
            >
              <Checkbox disabled={!canProceed}>
                {t('cashRegisters.decommission.confirmCheckbox')}
              </Checkbox>
            </Form.Item>
          </Form>
        </>
      )}
    </Modal>
  );
}
