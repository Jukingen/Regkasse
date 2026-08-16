'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Input, Select, Space, Steps, Typography } from 'antd';
import { useMemo, useState } from 'react';

import {
  authenticateFiskalyFon,
  initializeFiskalyCashRegister,
  initializeFiskalyScu,
  isFiskalyFonAuthenticated,
  isFiskalyResourceInitialized,
  type FiskalySetupStatusDto,
} from '@/features/fiskaly/api/fiskalySetup';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

const SETUP_QUERY_KEY = ['admin', 'fiskaly', 'setup'] as const;

type FonFormValues = {
  participantId: string;
  userId: string;
  pin: string;
};

function resolveInitialStep(status?: FiskalySetupStatusDto): number {
  if (!isFiskalyFonAuthenticated(status?.fon)) return 0;
  if (!isFiskalyResourceInitialized(status?.scu.state)) return 2;
  return 3;
}

export function FiskalySetupWizard({ status }: { status?: FiskalySetupStatusDto }) {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<FonFormValues>();
  const [step, setStep] = useState(() => resolveInitialStep(status));
  const [fonData, setFonData] = useState<FonFormValues>({
    participantId: status?.fon.participantId ?? '',
    userId: status?.fon.userId ?? '',
    pin: '',
  });
  const [selectedRegisterId, setSelectedRegisterId] = useState<string | undefined>(
    status?.cashRegisters.find((r) => !isFiskalyResourceInitialized(r.state))?.cashRegisterId
  );

  const stepItems = useMemo(
    () => [
      { title: t('tseFiskaly.setup.steps.credentials'), description: t('tseFiskaly.setup.steps.credentialsHint') },
      { title: t('tseFiskaly.setup.steps.authenticate'), description: t('tseFiskaly.setup.steps.authenticateHint') },
      { title: t('tseFiskaly.setup.steps.scu'), description: t('tseFiskaly.setup.steps.scuHint') },
      { title: t('tseFiskaly.setup.steps.cashRegister'), description: t('tseFiskaly.setup.steps.cashRegisterHint') },
    ],
    [t]
  );

  const invalidateSetup = async () => {
    await queryClient.invalidateQueries({ queryKey: SETUP_QUERY_KEY });
    await queryClient.invalidateQueries({ queryKey: ['admin', 'fiskaly'] });
  };

  const fonMutation = useMutation({
    mutationFn: () =>
      authenticateFiskalyFon({
        fonParticipantId: fonData.participantId.trim(),
        fonUserId: fonData.userId.trim(),
        fonUserPin: fonData.pin,
      }),
    onSuccess: async () => {
      setFonData((prev) => ({ ...prev, pin: '' }));
      form.setFieldValue('pin', '');
      notify.successKey('tseFiskaly.setup.fonSuccess');
      await invalidateSetup();
      setStep(2);
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FiskalySetup.authenticateFon',
        fallbackKey: 'tseFiskaly.setup.fonFailed',
      });
    },
  });

  const scuMutation = useMutation({
    mutationFn: () => initializeFiskalyScu(),
    onSuccess: async () => {
      notify.successKey('tseFiskaly.setup.scuSuccess');
      await invalidateSetup();
      setStep(3);
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FiskalySetup.initializeScu',
        fallbackKey: 'tseFiskaly.setup.scuFailed',
      });
    },
  });

  const cashRegisterMutation = useMutation({
    mutationFn: () => initializeFiskalyCashRegister(selectedRegisterId!),
    onSuccess: async () => {
      notify.successKey('tseFiskaly.setup.cashRegisterSuccess');
      await invalidateSetup();
      setStep(4);
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FiskalySetup.initializeCashRegister',
        fallbackKey: 'tseFiskaly.setup.cashRegisterFailed',
      });
    },
  });

  const pending =
    fonMutation.isPending || scuMutation.isPending || cashRegisterMutation.isPending;

  return (
    <Card>
      <Steps current={Math.min(step, 3)} items={stepItems} style={{ marginBottom: 24 }} />

      {step === 0 && (
        <Form
          form={form}
          layout="vertical"
          initialValues={fonData}
          onFinish={(values) => {
            setFonData(values);
            setStep(1);
          }}
        >
          <Alert type="info" showIcon style={{ marginBottom: 16 }} title={t('tseFiskaly.setup.pinNeverStored')} />
          <Form.Item
            name="participantId"
            label={t('tseFiskaly.setup.participantId')}
            rules={[
              { required: true, message: t('tseFiskaly.setup.participantRequired') },
              { pattern: /^[0-9A-Za-z]{8,12}$/, message: t('tseFiskaly.setup.participantPattern') },
            ]}
          >
            <Input autoComplete="off" maxLength={12} />
          </Form.Item>
          <Form.Item
            name="userId"
            label={t('tseFiskaly.setup.userId')}
            rules={[
              { required: true, message: t('tseFiskaly.setup.userIdRequired') },
              { min: 5, max: 12, message: t('tseFiskaly.setup.userIdPattern') },
            ]}
          >
            <Input autoComplete="off" maxLength={12} />
          </Form.Item>
          <Form.Item
            name="pin"
            label={t('tseFiskaly.setup.pin')}
            rules={[
              { required: true, message: t('tseFiskaly.setup.pinRequired') },
              { min: 5, max: 128, message: t('tseFiskaly.setup.pinPattern') },
            ]}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit">
            {t('tseFiskaly.setup.next')}
          </Button>
        </Form>
      )}

      {step === 1 && (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Alert type="warning" showIcon title={t('tseFiskaly.setup.authenticateConfirm')} />
          <Typography.Paragraph>
            {t('tseFiskaly.setup.participantId')}: <Typography.Text strong>{fonData.participantId}</Typography.Text>
          </Typography.Paragraph>
          <Typography.Paragraph>
            {t('tseFiskaly.setup.userId')}: <Typography.Text strong>{fonData.userId}</Typography.Text>
          </Typography.Paragraph>
          <Typography.Paragraph type="secondary">{t('tseFiskaly.setup.pinMasked')}</Typography.Paragraph>
          <Space>
            <Button onClick={() => setStep(0)}>{t('tseFiskaly.setup.back')}</Button>
            <Button type="primary" loading={fonMutation.isPending} onClick={() => fonMutation.mutate()}>
              {t('tseFiskaly.setup.authenticateAction')}
            </Button>
          </Space>
        </Space>
      )}

      {step === 2 && (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Alert type="info" showIcon title={t('tseFiskaly.setup.scuHint')} />
          {status?.scu.scuId ? (
            <Typography.Paragraph>
              {t('tseFiskaly.setup.scuId')}: <Typography.Text code>{status.scu.scuId}</Typography.Text>
            </Typography.Paragraph>
          ) : null}
          <Typography.Paragraph>
            {t('tseFiskaly.setup.currentState')}: {status?.scu.state ?? t('tseFiskaly.setup.unknown')}
          </Typography.Paragraph>
          <Space>
            <Button onClick={() => setStep(1)} disabled={pending}>
              {t('tseFiskaly.setup.back')}
            </Button>
            <Button type="primary" loading={scuMutation.isPending} onClick={() => scuMutation.mutate()}>
              {t('tseFiskaly.setup.initializeScu')}
            </Button>
          </Space>
        </Space>
      )}

      {step === 3 && (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          {(status?.cashRegisters.length ?? 0) === 0 ? (
            <Alert type="warning" showIcon title={t('tseFiskaly.setup.noCashRegisters')} />
          ) : (
            <>
              <Typography.Paragraph>{t('tseFiskaly.setup.cashRegisterHint')}</Typography.Paragraph>
              <Select
                style={{ maxWidth: 420 }}
                placeholder={t('tseFiskaly.setup.selectCashRegister')}
                value={selectedRegisterId}
                onChange={setSelectedRegisterId}
                options={(status?.cashRegisters ?? []).map((register) => ({
                  value: register.cashRegisterId,
                  label: `${register.registerNumber ?? register.cashRegisterId} (${register.state})`,
                  disabled: isFiskalyResourceInitialized(register.state),
                }))}
              />
            </>
          )}
          <Space>
            <Button onClick={() => setStep(2)} disabled={pending}>
              {t('tseFiskaly.setup.back')}
            </Button>
            <Button
              type="primary"
              loading={cashRegisterMutation.isPending}
              disabled={!selectedRegisterId}
              onClick={() => cashRegisterMutation.mutate()}
            >
              {t('tseFiskaly.setup.initializeCashRegister')}
            </Button>
          </Space>
        </Space>
      )}

      {step === 4 && (
        <Alert type="success" showIcon title={t('tseFiskaly.setup.completeTitle')} description={t('tseFiskaly.setup.completeHint')} />
      )}
    </Card>
  );
}
