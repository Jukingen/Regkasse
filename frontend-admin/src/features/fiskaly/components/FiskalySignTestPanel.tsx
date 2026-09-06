'use client';

import { InfoCircleOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Descriptions, Form, QRCode, Select, Space, Tag, Tooltip, Typography } from 'antd';
import { useMemo, useState } from 'react';

import { useMutation, useQuery } from '@tanstack/react-query';

import {
  getFiskalySignScenarios,
  signFiskalyTestReceipt,
  verifyFiskalyTestReceipt,
  type FiskalyReceiptChecks,
  type FiskalySignTestResult,
  type FiskalyVerifyTestResult,
} from '@/features/fiskaly/api/fiskalySignTest';
import { parseFiskalyReceiptError, type FiskalyReceiptError } from '@/features/fiskaly/api/fiskalyReceipts';
import {
  getFiskalySetup,
  isFiskalyResourceInitialized,
} from '@/features/fiskaly/api/fiskalySetup';
import { previousViennaMonth, previousViennaYear } from '@/features/fiskaly/fiskalySignTestPeriod';
import { displayFiskalyTestError } from '@/features/fiskaly/fiskalyTestErrorMessage';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type ResultView = FiskalySignTestResult | FiskalyVerifyTestResult;

export function FiskalyReceiptChecksList({ checks }: { checks: FiskalyReceiptChecks }) {
  const { t } = useI18n();
  const items: Array<{ key: keyof FiskalyReceiptChecks; ok: boolean }> = [
    { key: 'qrFormatValid', ok: checks.qrFormatValid },
    { key: 'hasReceiptNumber', ok: checks.hasReceiptNumber },
    { key: 'receiptNumberLooksSequential', ok: checks.receiptNumberLooksSequential },
    { key: 'hasTimeSignature', ok: checks.hasTimeSignature },
    { key: 'hasCashRegisterSerial', ok: checks.hasCashRegisterSerial },
    { key: 'signed', ok: checks.signed },
  ];

  return (
    <Space wrap>
      {items.map((item) => (
        <Tag key={item.key} color={item.ok ? 'green' : 'red'}>
          {t(`tseFiskaly.test.checks.${item.key}`)}
        </Tag>
      ))}
    </Space>
  );
}

export function FiskalySignTestPanel() {
  const { t } = useI18n();
  const notify = useNotify();
  const [registerId, setRegisterId] = useState<string>();
  const [scenario, setScenario] = useState<string>('normal');
  const [result, setResult] = useState<ResultView | null>(null);
  const [lastError, setLastError] = useState<FiskalyReceiptError | null>(null);

  const setupQuery = useQuery({
    queryKey: ['admin', 'fiskaly', 'setup'],
    queryFn: ({ signal }) => getFiskalySetup(signal),
    staleTime: 10_000,
  });

  const scenariosQuery = useQuery({
    queryKey: ['admin', 'fiskaly', 'sign-scenarios'],
    queryFn: ({ signal }) => getFiskalySignScenarios(signal),
    staleTime: 60_000,
    retry: false,
  });

  const initializedRegisters = useMemo(
    () => (setupQuery.data?.cashRegisters ?? []).filter((r) => isFiskalyResourceInitialized(r.state)),
    [setupQuery.data?.cashRegisters]
  );

  const selectedScenario = scenariosQuery.data?.find((s) => s.id === scenario);
  const canSign = Boolean(registerId && selectedScenario?.canSign);

  const signMutation = useMutation({
    mutationFn: () => {
      const extras =
        scenario === 'monthly_close'
          ? previousViennaMonth()
          : scenario === 'yearly_close'
            ? { year: previousViennaYear() }
            : undefined;
      return signFiskalyTestReceipt(registerId!, scenario, extras);
    },
    onSuccess: (data) => {
      if (data.success === false) {
        setResult(null);
        setLastError({
          code: 'FISKALY_API_ERROR',
          message: t('tseFiskaly.test.signFailed'),
        });
        notify.errorKey('tseFiskaly.test.signFailed');
        return;
      }
      setLastError(null);
      setResult(data);
      notify.successKey('tseFiskaly.test.signSuccess');
    },
    onError: (err) => {
      setResult(null);
      const parsed = parseFiskalyReceiptError(err) ?? {
        code: 'FISKALY_API_ERROR',
        message: t('tseFiskaly.test.signFailed'),
      };
      setLastError(parsed);
      notify.apiError(err, { logContext: 'FiskalySignTest.sign', fallbackKey: 'tseFiskaly.test.signFailed' });
    },
  });

  const verifyMutation = useMutation({
    mutationFn: (receiptId: string) => verifyFiskalyTestReceipt(registerId!, receiptId),
    onSuccess: (data) => {
      setLastError(null);
      setResult(data);
      notify.successKey('tseFiskaly.test.verifySuccess');
    },
    onError: (err) => {
      setResult(null);
      const parsed = parseFiskalyReceiptError(err) ?? {
        code: 'FISKALY_API_ERROR',
        message: t('tseFiskaly.test.verifyFailed'),
      };
      setLastError(parsed);
      notify.apiError(err, {
        logContext: 'FiskalySignTest.verify',
        fallbackKey: 'tseFiskaly.test.verifyFailed',
      });
    },
  });

  const lastReceiptId = result && 'receiptId' in result ? result.receiptId : undefined;

  if (scenariosQuery.isError) {
    return (
      <Alert type="warning" showIcon title={t('tseFiskaly.test.devOnly')} description={t('tseFiskaly.test.devOnlyHint')} />
    );
  }

  const errorView = displayFiskalyTestError(t, lastError, 'tseFiskaly.test.signFailed');

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Card loading={setupQuery.isLoading || scenariosQuery.isLoading}>
        <Form layout="vertical">
          <Form.Item label={t('tseFiskaly.test.registerLabel')} required>
            <Select
              value={registerId}
              onChange={setRegisterId}
              placeholder={t('tseFiskaly.setup.selectCashRegister')}
              options={initializedRegisters.map((r) => ({
                value: r.cashRegisterId,
                label: `${r.registerNumber ?? r.cashRegisterId}${r.location ? ` — ${r.location}` : ''}`,
              }))}
              notFoundContent={t('tseFiskaly.test.noInitializedRegisters')}
            />
          </Form.Item>
          <Form.Item
            label={
              <Space size={6}>
                <span>{t('tseFiskaly.test.scenarioLabel')}</span>
                <Tooltip title={t('tseFiskaly.test.nullbelegVsStartbeleg')}>
                  <InfoCircleOutlined aria-label={t('tseFiskaly.test.nullbelegVsStartbelegAria')} />
                </Tooltip>
              </Space>
            }
            required
          >
            <Select
              value={scenario}
              onChange={setScenario}
              options={(scenariosQuery.data ?? []).map((s) => ({
                value: s.id,
                label: t(`tseFiskaly.test.scenarios.${s.id}`),
              }))}
            />
          </Form.Item>
          {selectedScenario ? (
            <Alert
              type={selectedScenario.canSign ? 'info' : 'warning'}
              showIcon
              title={t(`tseFiskaly.test.scenarios.${selectedScenario.id}`)}
              description={t(`tseFiskaly.test.scenarioHints.${selectedScenario.id}`)}
              style={{ marginBottom: 16 }}
            />
          ) : null}
          <Space>
            <Button
              type="primary"
              onClick={() => signMutation.mutate()}
              loading={signMutation.isPending}
              disabled={!canSign}
            >
              {t(
                scenario === 'tagesabschluss'
                  ? 'tseFiskaly.test.signTagesabschlussAction'
                  : scenario === 'monthly_close'
                    ? 'tseFiskaly.test.signMonatsbelegAction'
                    : scenario === 'yearly_close'
                      ? 'tseFiskaly.test.signJahresbelegAction'
                      : 'tseFiskaly.test.signAction'
              )}
            </Button>
            <Button
              onClick={() => lastReceiptId && verifyMutation.mutate(lastReceiptId)}
              loading={verifyMutation.isPending}
              disabled={!registerId || !lastReceiptId}
            >
              {t('tseFiskaly.test.verifyAction')}
            </Button>
          </Space>
        </Form>
      </Card>

      {lastError ? (
        <Alert
          type="error"
          showIcon
          title={errorView.title}
          description={
            <Space orientation="vertical" size={4}>
              {errorView.code ? (
                <Typography.Text>
                  <Typography.Text strong>{t('tseFiskaly.operations.errorCode')}: </Typography.Text>
                  {errorView.code}
                </Typography.Text>
              ) : null}
              {lastError.message ? (
                <Typography.Text>
                  <Typography.Text strong>{t('tseFiskaly.operations.errorMessage')}: </Typography.Text>
                  {lastError.message}
                </Typography.Text>
              ) : null}
              {errorView.details ? (
                <Typography.Paragraph style={{ marginBottom: 0 }} type="secondary">
                  {errorView.details}
                </Typography.Paragraph>
              ) : null}
            </Space>
          }
        />
      ) : null}

      {result ? (
        <Card title={t('tseFiskaly.test.resultTitle')}>
          {'success' in result && result.success === false ? (
            <Alert type="error" showIcon title={t('tseFiskaly.test.signFailed')} />
          ) : (
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
              <Alert type="success" showIcon title={t('tseFiskaly.test.signedTitle')} />
              <Descriptions column={1} size="small">
                <Descriptions.Item label={t('tseFiskaly.test.receiptNumber')}>
                  {result.receiptNumber ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('tseFiskaly.test.receiptId')}>
                  <Typography.Text copyable>{result.receiptId}</Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('tseFiskaly.test.timeSignature')}>
                  {result.timeSignature ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('tseFiskaly.test.cashRegisterSerial')}>
                  {result.cashRegisterSerial ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('tseFiskaly.test.receiptType')}>
                  {result.receiptType ?? '—'}
                </Descriptions.Item>
              </Descriptions>
              {result.checks ? (
                <div>
                  <Typography.Text strong>{t('tseFiskaly.test.checksTitle')}</Typography.Text>
                  <div style={{ marginTop: 8 }}>
                    <FiskalyReceiptChecksList checks={result.checks} />
                  </div>
                </div>
              ) : null}
              {result.qrCodeData ? (
                <Space orientation="vertical">
                  <Typography.Text strong>{t('tseFiskaly.test.qrTitle')}</Typography.Text>
                  <QRCode value={result.qrCodeData} size={180} />
                  <Typography.Paragraph copyable type="secondary" style={{ maxWidth: 560 }}>
                    {result.qrCodeData}
                  </Typography.Paragraph>
                </Space>
              ) : null}
              {result.hints && result.hints.length > 0 ? (
                <Alert type="warning" showIcon title={t('tseFiskaly.test.hints')} description={result.hints.join(' · ')} />
              ) : null}
            </Space>
          )}
        </Card>
      ) : null}
    </Space>
  );
}
