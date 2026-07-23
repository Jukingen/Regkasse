'use client';

import { useMutation } from '@tanstack/react-query';
import { Alert, Button, Card, DatePicker, InputNumber, Select, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import utc from 'dayjs/plugin/utc';
import React, { useState } from 'react';

import { isDevelopment } from '@/features/auth/services/devTenant';
import {
  resetTseSimulation,
  simulateTseCertificateExpiry,
  simulateTseFailure,
  simulateTseLatency,
  type TseSimulatorFailureType,
} from '@/features/tse-management/api/tseManagement';
import type { TseDeviceFleetItem } from '@/features/tse-management/types';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

dayjs.extend(utc);
type Props = {
  devices: TseDeviceFleetItem[];
  onApplied?: () => void | Promise<void>;
};

export function TseSimulationToolsCard({ devices, onApplied }: Props) {
  const { t } = useI18n();
  const notify = useNotify();
  const [deviceId, setDeviceId] = useState<string | undefined>();
  const [latencyMs, setLatencyMs] = useState<number>(3000);
  const [expiry, setExpiry] = useState<Dayjs | null>(dayjs().subtract(1, 'day'));

  const requireDevice = (): string | null => {
    if (!deviceId) {
      notify.error(t('tseManagement.simulator.deviceRequired'));
      return null;
    }
    return deviceId;
  };

  const after = async (ok: boolean, resetMsg?: boolean) => {
    if (!ok) return;
    notify.success(
      resetMsg ? t('tseManagement.simulator.resetSuccess') : t('tseManagement.simulator.success')
    );
    await onApplied?.();
  };

  const failureMutation = useMutation({
    mutationFn: ({ id, type }: { id: string; type: TseSimulatorFailureType }) =>
      simulateTseFailure(id, type),
    onSuccess: async (res) => {
      if (!res.success) {
        notify.error(res.error || res.message);
        return;
      }
      await after(true);
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseSimulator.failure',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const latencyMutation = useMutation({
    mutationFn: ({ id, ms }: { id: string; ms: number }) => simulateTseLatency(id, ms),
    onSuccess: async (res) => {
      if (!res.success) {
        notify.error(res.error || res.message);
        return;
      }
      await after(true);
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseSimulator.latency',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const expiryMutation = useMutation({
    mutationFn: ({ id, at }: { id: string; at: string }) => simulateTseCertificateExpiry(id, at),
    onSuccess: async (res) => {
      if (!res.success) {
        notify.error(res.error || res.message);
        return;
      }
      await after(true);
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseSimulator.expiry',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const resetMutation = useMutation({
    mutationFn: (id: string) => resetTseSimulation(id),
    onSuccess: async (res) => {
      if (!res.success) {
        notify.error(res.error || res.message);
        return;
      }
      await after(true, true);
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseSimulator.reset',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  if (!isDevelopment()) {
    return null;
  }

  const busy =
    failureMutation.isPending ||
    latencyMutation.isPending ||
    expiryMutation.isPending ||
    resetMutation.isPending;

  const runFailure = (type: TseSimulatorFailureType) => {
    const id = requireDevice();
    if (!id) return;
    failureMutation.mutate({ id, type });
  };

  return (
    <Card title={t('tseManagement.simulator.title')} style={{ marginBottom: 16 }}>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={t('tseManagement.simulator.devOnly')}
      />

      <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <Select
          style={{ minWidth: 320, maxWidth: 480 }}
          placeholder={t('tseManagement.simulator.selectDevice')}
          value={deviceId}
          onChange={setDeviceId}
          options={devices.map((d) => ({
            value: d.id,
            label: `${d.serialNumber} · ${d.tenantName || d.tenantSlug || '—'}`,
          }))}
        />

        <Space wrap>
          <Button danger loading={busy} onClick={() => runFailure('NetworkTimeout')}>
            {t('tseManagement.simulator.networkTimeout')}
          </Button>
          <Button danger loading={busy} onClick={() => runFailure('ConnectionLost')}>
            {t('tseManagement.simulator.connectionLost')}
          </Button>
          <Button danger loading={busy} onClick={() => runFailure('CertificateInvalid')}>
            {t('tseManagement.simulator.certificateInvalid')}
          </Button>
          <Button danger loading={busy} onClick={() => runFailure('SignatureError')}>
            {t('tseManagement.simulator.signatureError')}
          </Button>
          <Button danger loading={busy} onClick={() => runFailure('RateLimitExceeded')}>
            {t('tseManagement.simulator.rateLimit')}
          </Button>
          <Button danger loading={busy} onClick={() => runFailure('InternalServerError')}>
            {t('tseManagement.simulator.internalError')}
          </Button>
        </Space>

        <Space wrap align="center">
          <Typography.Text>{t('tseManagement.simulator.latencyLabel')}</Typography.Text>
          <InputNumber
            min={0}
            max={60000}
            step={500}
            value={latencyMs}
            onChange={(v) => setLatencyMs(typeof v === 'number' ? v : 0)}
          />
          <Button
            loading={busy}
            onClick={() => {
              const id = requireDevice();
              if (!id) return;
              latencyMutation.mutate({ id, ms: latencyMs });
            }}
          >
            {t('tseManagement.simulator.applyLatency')}
          </Button>
        </Space>

        <Space wrap align="center">
          <Typography.Text>{t('tseManagement.simulator.expiryLabel')}</Typography.Text>
          <DatePicker
            showTime
            value={expiry}
            onChange={(v) => setExpiry(v)}
            style={{ minWidth: 220 }}
          />
          <Button
            loading={busy}
            onClick={() => {
              const id = requireDevice();
              if (!id || !expiry) return;
              expiryMutation.mutate({ id, at: expiry.utc().toISOString() });
            }}
          >
            {t('tseManagement.simulator.applyExpiry')}
          </Button>
        </Space>

        <Button
          type="primary"
          loading={busy}
          onClick={() => {
            const id = requireDevice();
            if (!id) return;
            resetMutation.mutate(id);
          }}
        >
          {t('tseManagement.simulator.reset')}
        </Button>
      </Space>
    </Card>
  );
}
