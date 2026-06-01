'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Minimal admin trigger for RKSV Jahresbeleg (manual POST). German operator copy only.
 */
import React, { useCallback, useMemo, useState } from 'react';
import { Button, Card, Input, InputNumber, Select, Space, Typography } from 'antd';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { CashRegister } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

function getViennaCalendarYear(now: Date = new Date()): number {
  const fmt = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Vienna', year: 'numeric' });
  const y = fmt.formatToParts(now).find((p) => p.type === 'year')?.value;
  return y ? Number(y) : now.getUTCFullYear();
}

type CreateJahresbelegResponse = {
  paymentId: string;
  invoiceId: string;
  receiptId: string;
  receiptNumber: string;
  qrData: string;
};

function normalizeRegisterRows(data: unknown): CashRegister[] {
  if (Array.isArray(data)) return data as CashRegister[];
  if (data && typeof data === 'object' && 'registers' in data) {
    const r = (data as { registers?: CashRegister[] }).registers;
    if (Array.isArray(r)) return r;
  }
  return [];
}

export function RksvJahresbelegManualCard() {
  const { message, modal } = useAntdApp();

  const { hasPermission } = usePermissions();
  const can = hasPermission(PERMISSIONS.RKSV_JAHRESBELEG_CREATE);
  const { data: registersRaw, isLoading } = useGetApiCashRegister();
  const registers = useMemo(() => normalizeRegisterRows(registersRaw), [registersRaw]);

  const defaultYear = useMemo(() => getViennaCalendarYear(), []);
  const [registerId, setRegisterId] = useState<string | undefined>(undefined);
  const [year, setYear] = useState<number>(defaultYear);
  const [earlyReason, setEarlyReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const doPost = useCallback(async () => {
    if (!registerId) {
      message.warning('Bitte eine Kasse wählen.');
      return;
    }
    setSubmitting(true);
    try {
      const res = await customInstance<CreateJahresbelegResponse>({
        url: '/api/rksv/special-receipts/jahresbeleg',
        method: 'POST',
        data: {
          cashRegisterId: registerId,
          year,
          reason: 'Admin Jahresbeleg',
          earlyReason: earlyReason.trim() || null,
        },
      });
      message.success(`Jahresbeleg erstellt: ${res.receiptNumber}`);
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } }; message?: string };
      const msg = err?.response?.data?.message ?? err?.message ?? 'Anfrage fehlgeschlagen';
      message.error(String(msg));
    } finally {
      setSubmitting(false);
    }
  }, [registerId, year, earlyReason]);

  const onCreateClick = useCallback(() => {
    modal.confirm({
      title: 'Jahresbeleg erstellen',
      content: 'Dieser Vorgang kann nicht rückgängig gemacht werden.',
      okText: 'Erstellen',
      cancelText: 'Abbrechen',
      okButtonProps: { loading: submitting },
      onOk: () => doPost(),
    });
  }, [doPost, submitting]);

  if (!can) {
    return null;
  }

  return (
    <Card size="small" title="Jahresbeleg (manuell)" style={{ marginBottom: 16 }}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0, fontSize: 12 }}>
        Erstellt einen fiskalischen Jahres-Nullbeleg für die gewählte Kasse und das Kalenderjahr (Wien).
      </Typography.Paragraph>
      <Space orientation="vertical" style={{ width: '100%' }} size="middle">
        <div>
          <Typography.Text type="secondary">Kasse</Typography.Text>
          <Select
            showSearch
            allowClear
            placeholder="Kasse wählen"
            style={{ width: '100%', marginTop: 4 }}
            loading={isLoading}
            optionFilterProp="label"
            value={registerId}
            onChange={(v) => setRegisterId(v)}
            options={registers
              .filter((r) => r.id)
              .map((r) => ({
                value: r.id as string,
                label: `${r.registerNumber ?? r.id} (${r.id})`,
              }))}
          />
        </div>
        <div>
          <Typography.Text type="secondary">Kalenderjahr (Wien)</Typography.Text>
          <InputNumber min={2000} max={2100} value={year} onChange={(v) => setYear(Number(v) || defaultYear)} style={{ width: '100%', marginTop: 4 }} />
        </div>
        <div>
          <Typography.Text type="secondary">Optional: Hinweis vorzeitige Erstellung</Typography.Text>
          <Input value={earlyReason} onChange={(e) => setEarlyReason(e.target.value)} maxLength={450} style={{ marginTop: 4 }} />
        </div>
        <Button type="primary" onClick={onCreateClick} loading={submitting} disabled={!registerId}>
          Jahresbeleg erstellen…
        </Button>
      </Space>
    </Card>
  );
}
