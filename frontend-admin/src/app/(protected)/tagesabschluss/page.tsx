'use client';

import React, { useCallback, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Descriptions,
  Empty,
  Input,
  Modal,
  Row,
  Select,
  Space,
  Spin,
  Table,
  Typography,
  message,
} from 'antd';
import { CalendarOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';

import { getApiCashRegister } from '@/api/generated/cash-register/cash-register';
import {
  getGetApiTagesabschlussCanCloseCashRegisterIdQueryKey,
  getGetApiTagesabschlussHistoryQueryKey,
  getGetApiTagesabschlussStatisticsQueryKey,
  useGetApiTagesabschlussCanCloseCashRegisterId,
  useGetApiTagesabschlussHistory,
  useGetApiTagesabschlussStatistics,
  usePostApiTagesabschlussDaily,
  usePostApiTagesabschlussMonthly,
  usePostApiTagesabschlussYearly,
} from '@/api/generated/tagesabschluss/tagesabschluss';
import {
  normalizeCashRegisterListBody,
} from '@/features/tagesabschluss/normalizers';
import type {
  TagesabschlussCanCloseResponse,
  TagesabschlussResult,
  TagesabschlussStatisticsResponse,
} from '@/api/generated/model';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

const { Title, Paragraph, Text } = Typography;
const { RangePicker } = DatePicker;

function isUuid(v: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    v.trim()
  );
}

function pickError(e: unknown): string {
  const err = e as {
    response?: { data?: { error?: string; details?: string; message?: string } };
    message?: string;
  };
  return (
    err?.response?.data?.error ??
    err?.response?.data?.message ??
    err?.response?.data?.details ??
    err?.message ??
    'Unbekannter Fehler'
  );
}

export default function TagesabschlussPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const canListRegisters = hasPermission(PERMISSIONS.CASHREGISTER_VIEW);

  const [range, setRange] = useState<[Dayjs, Dayjs]>([
    dayjs().subtract(30, 'day'),
    dayjs(),
  ]);
  const [selectedRegisterId, setSelectedRegisterId] = useState<string>('');
  const [manualRegisterId, setManualRegisterId] = useState<string>('');

  const historyParams = useMemo(
    () => ({
      fromDate: range[0].format('YYYY-MM-DD'),
      toDate: range[1].format('YYYY-MM-DD'),
    }),
    [range]
  );

  const statsParams = historyParams;

  const { data: registersRaw, isLoading: registersLoading } = useQuery({
    queryKey: ['admin', 'cashRegisters', 'list'],
    queryFn: async () => getApiCashRegister(),
    enabled: canListRegisters,
  });

  const registerOptions = useMemo(() => {
    const list = normalizeCashRegisterListBody(registersRaw);
    return list
      .filter((r) => r.id)
      .map((r) => ({
        value: r.id as string,
        label: `${r.registerNumber ?? r.id} — ${r.location ?? ''}`,
      }));
  }, [registersRaw]);

  const effectiveRegisterId = selectedRegisterId || manualRegisterId.trim();
  const registerIdValid = effectiveRegisterId.length > 0 && isUuid(effectiveRegisterId);

  const historyQuery = useGetApiTagesabschlussHistory(historyParams);
  const historyRows: TagesabschlussResult[] = historyQuery.data ?? [];

  const statsQuery = useGetApiTagesabschlussStatistics(statsParams);
  const stats: TagesabschlussStatisticsResponse | undefined = statsQuery.data;

  const canCloseQuery = useGetApiTagesabschlussCanCloseCashRegisterId(effectiveRegisterId, {
    query: { enabled: registerIdValid },
  });
  const canClose: TagesabschlussCanCloseResponse | undefined = canCloseQuery.data;

  const invalidateTagesabschluss = useCallback(async () => {
    await queryClient.invalidateQueries({
      queryKey: getGetApiTagesabschlussHistoryQueryKey(historyParams),
    });
    await queryClient.invalidateQueries({
      queryKey: getGetApiTagesabschlussStatisticsQueryKey(statsParams),
    });
    if (registerIdValid) {
      await queryClient.invalidateQueries({
        queryKey: getGetApiTagesabschlussCanCloseCashRegisterIdQueryKey(effectiveRegisterId),
      });
    }
  }, [queryClient, historyParams, statsParams, registerIdValid, effectiveRegisterId]);

  const dailyMu = usePostApiTagesabschlussDaily({
    mutation: {
      onSuccess: async () => {
        message.success('Tagesabschluss (täglich) ausgeführt.');
        await invalidateTagesabschluss();
      },
      onError: (e) => message.error(pickError(e)),
    },
  });
  const monthlyMu = usePostApiTagesabschlussMonthly({
    mutation: {
      onSuccess: async () => {
        message.success('Monatsabschluss ausgeführt.');
        await invalidateTagesabschluss();
      },
      onError: (e) => message.error(pickError(e)),
    },
  });
  const yearlyMu = usePostApiTagesabschlussYearly({
    mutation: {
      onSuccess: async () => {
        message.success('Jahresabschluss ausgeführt.');
        await invalidateTagesabschluss();
      },
      onError: (e) => message.error(pickError(e)),
    },
  });

  const runClosing = (kind: 'daily' | 'monthly' | 'yearly') => {
    if (!registerIdValid) {
      message.warning('Bitte gültige Kassen-ID wählen oder eingeben (UUID).');
      return;
    }
    const labels = {
      daily: 'Tagesabschluss',
      monthly: 'Monatsabschluss',
      yearly: 'Jahresabschluss',
    } as const;
    Modal.confirm({
      title: `${labels[kind]} ausführen?`,
      content: 'RKSV-relevante Operation. Nur ausführen, wenn die Kasse fachlich bereit ist.',
      okText: 'Ausführen',
      cancelText: 'Abbrechen',
      okButtonProps: { danger: kind !== 'daily' },
      onOk: async () => {
        const body = { data: { cashRegisterId: effectiveRegisterId } };
        if (kind === 'daily') await dailyMu.mutateAsync(body);
        else if (kind === 'monthly') await monthlyMu.mutateAsync(body);
        else await yearlyMu.mutateAsync(body);
      },
    });
  };

  const historyColumns = [
    { title: 'Datum', dataIndex: 'closingDate', key: 'closingDate', width: 200 },
    { title: 'Typ', dataIndex: 'closingType', key: 'closingType', width: 100 },
    {
      title: 'Brutto',
      dataIndex: 'totalAmount',
      key: 'totalAmount',
      render: (v: number) => `${v.toFixed(2)} €`,
    },
    {
      title: 'Steuer',
      dataIndex: 'totalTaxAmount',
      key: 'totalTaxAmount',
      render: (v: number) => `${v.toFixed(2)} €`,
    },
    { title: 'Vorgänge', dataIndex: 'transactionCount', key: 'transactionCount', width: 100 },
    { title: 'Status', dataIndex: 'status', key: 'status', width: 120 },
    { title: 'FinanzOnline', dataIndex: 'finanzOnlineStatus', key: 'fo', width: 140 },
  ];

  const closingBusy = dailyMu.isPending || monthlyMu.isPending || yearlyMu.isPending;

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Title level={3} style={{ marginBottom: 4 }}>
          <CalendarOutlined /> Tagesabschluss
        </Title>
        <Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Abschlüsse für die gewählte Kasse auslösen, Schlussprüfung anzeigen, Historie und Kennzahlen einsehen.
          Historie und Statistik beziehen sich auf den angemeldeten Benutzer (API-Filter).{' '}
          Backend: <Text code>/api/Tagesabschluss/*</Text> (Berechtigung: <Text code>tse.sign</Text>).
        </Paragraph>
      </div>

      <Card title="Kasse & Prüfung">
        <Space direction="vertical" style={{ width: '100%' }} size="middle">
          {canListRegisters ? (
            <div>
              <Text type="secondary">Kasse aus Liste</Text>
              <Select
                showSearch
                allowClear
                placeholder="Kasse wählen"
                style={{ width: '100%', marginTop: 6 }}
                options={registerOptions}
                loading={registersLoading}
                value={selectedRegisterId || undefined}
                onChange={(v) => setSelectedRegisterId(v ?? '')}
                optionFilterProp="label"
              />
            </div>
          ) : (
            <Alert
              type="info"
              showIcon
              message="Keine Kassenliste"
              description="Berechtigung cashregister.view fehlt — Kassen-ID manuell als UUID eingeben."
            />
          )}
          <div>
            <Text type="secondary">Kassen-ID (UUID)</Text>
            <Input
              style={{ marginTop: 6 }}
              placeholder="00000000-0000-0000-0000-000000000000"
              value={manualRegisterId}
              onChange={(e) => setManualRegisterId(e.target.value)}
            />
          </div>

          {!registerIdValid ? (
            <Text type="warning">Bitte gültige UUID setzen (Liste oder manuell).</Text>
          ) : canCloseQuery.isLoading ? (
            <Spin />
          ) : canCloseQuery.isError ? (
            <Alert type="error" message="Prüfung fehlgeschlagen" description={pickError(canCloseQuery.error)} />
          ) : (
            <Descriptions bordered size="small" column={1}>
              <Descriptions.Item label="Schließung möglich">
                {canClose?.canClose ? (
                  <Text type="success">Ja</Text>
                ) : (
                  <Text type="danger">Nein</Text>
                )}
              </Descriptions.Item>
              <Descriptions.Item label="Letzter Abschluss">
                {canClose?.lastClosingDate
                  ? dayjs(canClose.lastClosingDate).format('DD.MM.YYYY HH:mm')
                  : '—'}
              </Descriptions.Item>
              <Descriptions.Item label="Zahlungen ohne Rechnung (heute)">
                {canClose?.paymentsWithoutInvoiceCount}
              </Descriptions.Item>
              <Descriptions.Item label="Hinweis">{canClose?.message ?? '—'}</Descriptions.Item>
            </Descriptions>
          )}

          <Space wrap>
            <Button
              type="primary"
              loading={closingBusy}
              disabled={!registerIdValid}
              onClick={() => runClosing('daily')}
            >
              Tagesabschluss
            </Button>
            <Button loading={closingBusy} disabled={!registerIdValid} onClick={() => runClosing('monthly')}>
              Monatsabschluss
            </Button>
            <Button
              danger
              loading={closingBusy}
              disabled={!registerIdValid}
              onClick={() => runClosing('yearly')}
            >
              Jahresabschluss
            </Button>
          </Space>
        </Space>
      </Card>

      <Card
        title="Statistik & Historie (Zeitraum)"
        extra={
          <Space>
            <RangePicker value={range} onChange={(v) => v && v[0] && v[1] && setRange([v[0], v[1]])} />
            <Button
              icon={<ReloadOutlined />}
              onClick={() => {
                void historyQuery.refetch();
                void statsQuery.refetch();
                if (registerIdValid) void canCloseQuery.refetch();
              }}
            >
              Aktualisieren
            </Button>
          </Space>
        }
      >
        <Row gutter={[16, 16]}>
          <Col xs={24} lg={12}>
            <Title level={5}>Kennzahlen</Title>
            {statsQuery.isLoading ? (
              <Spin />
            ) : statsQuery.isError ? (
              <Alert type="error" description={pickError(statsQuery.error)} />
            ) : /* No aggregate closings in range */ stats == null ||
              (stats.totalClosings === 0 && stats.totalAmount === 0) ? (
              <Empty description="Keine Daten für diesen Zeitraum." />
            ) : (
              <Descriptions bordered size="small" column={1}>
                <Descriptions.Item label="Abschlüsse (Anzahl)">{stats.totalClosings}</Descriptions.Item>
                <Descriptions.Item label="Summe Brutto">
                  {stats.totalAmount.toFixed(2)} €
                </Descriptions.Item>
                <Descriptions.Item label="Summe Steuer">
                  {stats.totalTaxAmount.toFixed(2)} €
                </Descriptions.Item>
                <Descriptions.Item label="Transaktionen">{stats.totalTransactions}</Descriptions.Item>
                <Descriptions.Item label="Ø Tagesbrutto (nur Daily)">
                  {stats.averageDailyAmount.toFixed(2)} €
                </Descriptions.Item>
                <Descriptions.Item label="Letzter Abschluss im Zeitraum">
                  {stats.lastClosingDate
                    ? dayjs(stats.lastClosingDate).format('DD.MM.YYYY HH:mm')
                    : '—'}
                </Descriptions.Item>
              </Descriptions>
            )}
          </Col>
          <Col xs={24} lg={24}>
            <Title level={5}>Historie</Title>
            {historyQuery.isLoading ? (
              <Spin />
            ) : historyQuery.isError ? (
              <Alert type="error" description={pickError(historyQuery.error)} />
            ) : historyRows.length === 0 ? (
              <Empty description="Keine Einträge in diesem Zeitraum." />
            ) : (
              <Table
                rowKey={(r) => r.closingId ?? `${r.closingDate}-${r.closingType}`}
                size="small"
                pagination={{ pageSize: 10 }}
                columns={historyColumns}
                dataSource={historyRows}
              />
            )}
          </Col>
        </Row>
      </Card>
    </Space>
  );
}
