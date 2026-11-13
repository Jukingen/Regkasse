'use client';

import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExperimentOutlined,
  WarningOutlined,
  WifiOutlined,
} from '@ant-design/icons';
import { Alert, Card, Collapse, Spin, Tag, Typography } from 'antd';
import React from 'react';

import { useRksvStatus } from '@/features/rksv/hooks/useRksvBackendEnvironment';
import { useEnvironment } from '@/hooks/useEnvironment';
import { useI18n } from '@/i18n';
import { formatDateTimeSeconds } from '@/lib/dateUtils';

import { useSignatureDebugQuery } from '../hooks/useSignatureDebugQuery';
import type { SignatureDiagnosticStepDto } from '../types/signature-debug';

const { Text } = Typography;

export interface ReceiptOfflineTraceProps {
  hasOfflineOrigin: boolean;
  offlineTransactionId?: string | null;
  offlineCreatedAtUtc?: string | null;
  fiscalizedAtUtc?: string | null;
  issuedAt?: string | null;
}

interface SignatureStatusPanelProps {
  paymentId: string | null;
  /** RKSV trace: offline queue → replay → fiscal receipt timeline. */
  offlineTrace?: ReceiptOfflineTraceProps | null;
}

function resolveDisplayStatus(
  status: string,
  tseSimulated: boolean
): SignatureDiagnosticStepDto['status'] | string {
  if (status === 'SIMULATED') return 'SIMULATED';
  // Defense in depth: older APIs may still return FAIL under FakeTseProvider.
  if (tseSimulated && status === 'FAIL') return 'SIMULATED';
  return status;
}

function StatusTag({ status, simulatedLabel }: { status: string; simulatedLabel: string }) {
  if (status === 'PASS') {
    return (
      <Tag icon={<CheckCircleOutlined />} color="success">
        PASS
      </Tag>
    );
  }
  if (status === 'SIMULATED') {
    return (
      <Tag icon={<ExperimentOutlined />} color="orange">
        {simulatedLabel}
      </Tag>
    );
  }
  if (status === 'FAIL') {
    return (
      <Tag icon={<CloseCircleOutlined />} color="error">
        FAIL
      </Tag>
    );
  }
  if (status === 'WARN') {
    return (
      <Tag icon={<WarningOutlined />} color="warning">
        WARN
      </Tag>
    );
  }
  return <Tag>{status}</Tag>;
}

export default function SignatureStatusPanel({
  paymentId,
  offlineTrace,
}: SignatureStatusPanelProps) {
  const { t } = useI18n();
  const s = (key: string) => t(`receipts.detail.signature.${key}`);
  const { isDevelopment } = useEnvironment();
  const { isDemo: tseSimulatedFromApi, isLoading: envLoading } = useRksvStatus();
  // Prefer backend TSE/demo flag; fall back to frontend NODE_ENV while RKSV env loads.
  const usesSimulatedTse = envLoading ? isDevelopment : tseSimulatedFromApi;
  const { data, isLoading, isError, error } = useSignatureDebugQuery(paymentId);
  const isOffline = typeof navigator !== 'undefined' && !navigator.onLine;

  const environmentBadge = (
    <Alert
      type={usesSimulatedTse ? 'warning' : 'info'}
      showIcon
      icon={usesSimulatedTse ? <ExperimentOutlined /> : undefined}
      style={{ marginBottom: 16 }}
      title={usesSimulatedTse ? s('envBadgeDevTitle') : s('envBadgeProdTitle')}
      description={usesSimulatedTse ? s('envBadgeDevDescription') : s('envBadgeProdDescription')}
    />
  );

  if (!paymentId) {
    return (
      <Card title={s('cardTitle')}>
        {environmentBadge}
        <Alert type="info" title={s('noPaymentTitle')} description={s('noPaymentDescription')} />
      </Card>
    );
  }

  const showOfflineTimeline =
    offlineTrace?.hasOfflineOrigin &&
    (offlineTrace.offlineCreatedAtUtc || offlineTrace.fiscalizedAtUtc || offlineTrace.issuedAt);

  if (isOffline) {
    return (
      <Card title={s('cardTitle')}>
        {environmentBadge}
        <Alert
          type="warning"
          icon={<WifiOutlined />}
          title={s('offlineTitle')}
          description={s('offlineDescription')}
          showIcon
        />
      </Card>
    );
  }

  if (isLoading) {
    return (
      <Card title={s('cardTitle')}>
        {environmentBadge}
        <Spin description={s('verifyingTip')} />
      </Card>
    );
  }

  if (isError) {
    return (
      <Card title={s('cardTitle')}>
        {environmentBadge}
        <Alert
          type="error"
          title={s('verificationFailed')}
          description={(error as Error)?.message ?? s('loadDiagnosticFallback')}
          showIcon
        />
      </Card>
    );
  }

  const payload = data?.data ?? { steps: [], compactJws: null };
  const steps = payload.steps.map((step) => ({
    ...step,
    status: resolveDisplayStatus(
      step.status,
      usesSimulatedTse
    ) as SignatureDiagnosticStepDto['status'],
  }));
  const compactJws = payload.compactJws;
  const hasFail = steps.some((st) => st.status === 'FAIL');
  const hasSimulated = steps.some((st) => st.status === 'SIMULATED');
  const failSteps = steps.filter((st) => st.status === 'FAIL');
  const simulatedSteps = steps.filter((st) => st.status === 'SIMULATED');
  const simulatedLabel = s('simulatedTag');

  return (
    <Card title={s('cardTitle')}>
      {environmentBadge}
      {showOfflineTimeline ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          title={s('timelineTitle')}
          description={
            <div style={{ fontSize: 12 }}>
              {offlineTrace?.offlineCreatedAtUtc ? (
                <div>
                  <strong>{s('offlineCapturedStrong')}</strong>{' '}
                  {formatDateTimeSeconds(offlineTrace.offlineCreatedAtUtc)}
                </div>
              ) : null}
              {offlineTrace?.fiscalizedAtUtc ? (
                <div style={{ marginTop: 4 }}>
                  <strong>{s('fiscalizedAfterReplayStrong')}</strong>{' '}
                  {formatDateTimeSeconds(offlineTrace.fiscalizedAtUtc)}
                </div>
              ) : null}
              {offlineTrace?.issuedAt ? (
                <div style={{ marginTop: 4 }}>
                  <strong>{s('issuedAtFiscalStrong')}</strong>{' '}
                  {formatDateTimeSeconds(offlineTrace.issuedAt)}
                </div>
              ) : null}
            </div>
          }
        />
      ) : null}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {steps.map((step: SignatureDiagnosticStepDto) => (
          <div
            key={step.stepId}
            style={{
              display: 'flex',
              alignItems: 'flex-start',
              justifyContent: 'space-between',
              gap: 12,
              padding: '8px 0',
              borderBottom: step.stepId < steps.length ? '1px solid #f0f0f0' : undefined,
            }}
          >
            <div style={{ flex: 1 }}>
              <Text strong>{step.name}</Text>
              {step.evidence && (
                <div style={{ marginTop: 4, fontSize: 12, color: '#666' }}>{step.evidence}</div>
              )}
            </div>
            <StatusTag status={step.status} simulatedLabel={simulatedLabel} />
          </div>
        ))}
      </div>

      {hasFail && failSteps.length > 0 && (
        <Collapse
          style={{ marginTop: 16 }}
          items={[
            {
              key: '1',
              label: s('collapseFailedSteps'),
              children: (
                <div style={{ fontFamily: 'monospace', fontSize: 12 }}>
                  {failSteps.map((st) => (
                    <div key={st.stepId} style={{ marginBottom: 8 }}>
                      <strong>
                        {t('receipts.detail.signature.stepLine', {
                          stepId: st.stepId,
                          name: st.name,
                        })}
                      </strong>
                      <br />
                      {st.evidence ?? s('noEvidence')}
                    </div>
                  ))}
                </div>
              ),
            },
          ]}
        />
      )}

      {hasSimulated && simulatedSteps.length > 0 && !hasFail ? (
        <Collapse
          style={{ marginTop: 16 }}
          items={[
            {
              key: 'simulated',
              label: s('collapseSimulatedSteps'),
              children: (
                <div style={{ fontFamily: 'monospace', fontSize: 12 }}>
                  {simulatedSteps.map((st) => (
                    <div key={st.stepId} style={{ marginBottom: 8 }}>
                      <strong>
                        {t('receipts.detail.signature.stepLine', {
                          stepId: st.stepId,
                          name: st.name,
                        })}
                      </strong>
                      <br />
                      {st.evidence ?? s('noEvidence')}
                    </div>
                  ))}
                </div>
              ),
            },
          ]}
        />
      ) : null}

      {compactJws ? (
        <Collapse
          style={{ marginTop: 12 }}
          items={[
            {
              key: 'jws',
              label: s('compactJws'),
              children: (
                <Text
                  copyable
                  style={{ fontFamily: 'monospace', fontSize: 11, wordBreak: 'break-all' }}
                >
                  {compactJws}
                </Text>
              ),
            },
          ]}
        />
      ) : null}
    </Card>
  );
}
