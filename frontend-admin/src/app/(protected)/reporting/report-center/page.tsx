'use client';

/**
 * Report Center: formal raporları ve FinanzOnline gönderim durumlarını tek operatör/muhasebe merkezinde toplar.
 */
import React, { useMemo, useState } from 'react';
import { Alert, Button, Card, Segmented, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { AXIOS_INSTANCE } from '@/lib/axios';

type ReportKind = 'tagesbericht' | 'monatsbericht' | 'jahresbericht' | 'periodenbericht' | 'xz' | 'submissionQueue';

type TagesRow = { id: string; viennaBusinessDate: string; reportStatus: string; correctionKind: string; submission: { lifecycle: string } };
type MonatsRow = { id: string; viennaMonthStart: string; reportStatus: string; correctionKind: string; submission: { lifecycle: string } };
type JahresRow = { id: string; viennaYearStart: string; reportStatus: string; correctionKind: string; submission: { lifecycle: string } };
type OutboxRow = { outboxId: string; status?: string; operatorStatusLabel?: string; aggregateType?: string; aggregateId?: string; createdAt?: string };

function statusColor(status: string) {
  if (status === 'Finalized' || status === 'accepted' || status === 'ProtocolSuccess') return 'success';
  if (status === 'Provisional' || status === 'queued' || status === 'Pending') return 'gold';
  if (status === 'Superseded' || status === 'correction_required' || status === 'ManualReviewRequired') return 'orange';
  if (status === 'rejected' || status === 'DeadLetter' || status === 'ProtocolFailure') return 'error';
  return 'default';
}

export default function ReportCenterPage() {
  const { t } = useI18n();
  const [tab, setTab] = useState<ReportKind>('tagesbericht');

  const tagesQ = useQuery({
    queryKey: ['report-center', 'tages'],
    queryFn: async () => {
      const toDate = dayjs().format('YYYY-MM-DD');
      const fromDate = dayjs().subtract(14, 'day').format('YYYY-MM-DD');
      const { data } = await AXIOS_INSTANCE.get<TagesRow[]>('/api/reports/tagesbericht', { params: { fromDate, toDate } });
      return data;
    },
    enabled: tab === 'tagesbericht',
  });

  const monatsQ = useQuery({
    queryKey: ['report-center', 'monats'],
    queryFn: async () => {
      const fromMonth = dayjs().subtract(5, 'month').startOf('month').format('YYYY-MM-DD');
      const toMonth = dayjs().startOf('month').format('YYYY-MM-DD');
      const { data } = await AXIOS_INSTANCE.get<MonatsRow[]>('/api/reports/monatsbericht', { params: { fromMonth, toMonth } });
      return data;
    },
    enabled: tab === 'monatsbericht',
  });

  const jahresQ = useQuery({
    queryKey: ['report-center', 'jahres'],
    queryFn: async () => {
      const fromYear = dayjs().subtract(2, 'year').startOf('year').format('YYYY-MM-DD');
      const toYear = dayjs().startOf('year').format('YYYY-MM-DD');
      const { data } = await AXIOS_INSTANCE.get<JahresRow[]>('/api/reports/jahresbericht', { params: { fromYear, toYear } });
      return data;
    },
    enabled: tab === 'jahresbericht',
  });

  const outboxQ = useQuery({
    queryKey: ['report-center', 'outbox'],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<OutboxRow[]>('/api/admin/finanzonline-outbox', {
        params: { bucket: 'in_flight', limit: 50 },
      });
      return data;
    },
    enabled: tab === 'submissionQueue',
  });

  const tagesCols: ColumnsType<TagesRow> = useMemo(
    () => [
      { title: 'Datum', dataIndex: 'viennaBusinessDate', render: (v: string) => dayjs(v).format('YYYY-MM-DD') },
      { title: 'Report', dataIndex: 'reportStatus', render: (s: string) => <Tag color={statusColor(s)}>{s}</Tag> },
      { title: 'Submission', dataIndex: ['submission', 'lifecycle'], render: (s: string) => <Tag color={statusColor(s)}>{s}</Tag> },
      { title: 'Correction', dataIndex: 'correctionKind' },
      { title: '', key: 'd', render: (_, r) => <Link href={`/reporting/tagesbericht/${r.id}`}>Detail</Link> },
    ],
    [],
  );

  const monatsCols: ColumnsType<MonatsRow> = useMemo(
    () => [
      { title: 'Monat', dataIndex: 'viennaMonthStart', render: (v: string) => dayjs(v).format('YYYY-MM') },
      { title: 'Report', dataIndex: 'reportStatus', render: (s: string) => <Tag color={statusColor(s)}>{s}</Tag> },
      { title: 'Submission', dataIndex: ['submission', 'lifecycle'], render: (s: string) => <Tag color={statusColor(s)}>{s}</Tag> },
      { title: 'Correction', dataIndex: 'correctionKind' },
      { title: '', key: 'd', render: (_, r) => <Link href={`/reporting/monatsbericht/${r.id}`}>Detail</Link> },
    ],
    [],
  );

  const jahresCols: ColumnsType<JahresRow> = useMemo(
    () => [
      { title: 'Jahr', dataIndex: 'viennaYearStart', render: (v: string) => dayjs(v).format('YYYY') },
      { title: 'Report', dataIndex: 'reportStatus', render: (s: string) => <Tag color={statusColor(s)}>{s}</Tag> },
      { title: 'Submission', dataIndex: ['submission', 'lifecycle'], render: (s: string) => <Tag color={statusColor(s)}>{s}</Tag> },
      { title: 'Correction', dataIndex: 'correctionKind' },
      { title: '', key: 'd', render: (_, r) => <Link href={`/reporting/jahresbericht/${r.id}`}>Detail</Link> },
    ],
    [],
  );

  const outboxCols: ColumnsType<OutboxRow> = useMemo(
    () => [
      { title: 'Outbox', dataIndex: 'outboxId', render: (v: string) => <Typography.Text style={{ fontSize: 12 }}>{v}</Typography.Text> },
      { title: 'Aggregate', dataIndex: 'aggregateType' },
      { title: 'Status', dataIndex: 'status', render: (s: string, r) => <Tag color={statusColor(s || '')}>{r.operatorStatusLabel ?? s}</Tag> },
      { title: 'Created', dataIndex: 'createdAt', render: (v?: string) => (v ? dayjs(v).format('YYYY-MM-DD HH:mm') : '—') },
      { title: '', key: 'o', render: () => <Link href="/rksv/finanz-online-outbox">Queue öffnen</Link> },
    ],
    [],
  );

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader
        title="Report Center"
        breadcrumbs={[adminOverviewCrumb(t), { title: t('nav.reportCenter'), href: '/reporting/report-center' }]}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Formal Reports, Submission Visibility, Correction Timeline ve Export Profil seçimini tek merkezde sunar.
        </Typography.Paragraph>
      </AdminPageHeader>

      <Card size="small" style={{ marginBottom: 16 }}>
        <Segmented
          block
          value={tab}
          onChange={(v) => setTab(v as ReportKind)}
          options={[
            { label: 'Tagesbericht', value: 'tagesbericht' },
            { label: 'Monatsbericht', value: 'monatsbericht' },
            { label: 'Jahresbericht', value: 'jahresbericht' },
            { label: 'Periodenbericht', value: 'periodenbericht' },
            { label: 'X/Z', value: 'xz' },
            { label: 'Submission Queue', value: 'submissionQueue' },
          ]}
        />
      </Card>

      {tab === 'tagesbericht' ? (
        <Card title="Tagesbericht (formal)">
          <Space style={{ marginBottom: 12 }}>
            <Button type="primary" href="/reporting/tagesbericht">Zur Tagesbericht-Übersicht</Button>
          </Space>
          <Table rowKey="id" loading={tagesQ.isLoading} dataSource={tagesQ.data ?? []} columns={tagesCols} pagination={{ pageSize: 10 }} />
        </Card>
      ) : null}

      {tab === 'monatsbericht' ? (
        <Card title="Monatsbericht (formal)">
          <Space style={{ marginBottom: 12 }}>
            <Button type="primary" href="/reporting/monatsbericht">Zur Monatsbericht-Übersicht</Button>
          </Space>
          <Table rowKey="id" loading={monatsQ.isLoading} dataSource={monatsQ.data ?? []} columns={monatsCols} pagination={{ pageSize: 10 }} />
        </Card>
      ) : null}

      {tab === 'jahresbericht' ? (
        <Card title="Jahresbericht (formal)">
          <Space style={{ marginBottom: 12 }}>
            <Button type="primary" href="/reporting/jahresbericht">Zur Jahresbericht-Übersicht</Button>
          </Space>
          <Table rowKey="id" loading={jahresQ.isLoading} dataSource={jahresQ.data ?? []} columns={jahresCols} pagination={{ pageSize: 10 }} />
        </Card>
      ) : null}

      {tab === 'periodenbericht' ? (
        <Card title="Periodenbericht">
          <Alert
            type="info"
            showIcon
            message="Periodenbericht nutzt die bestehende operative Reporting-Engine (Periodic/Custom)."
            style={{ marginBottom: 12 }}
          />
          <Space>
            <Button type="primary" href="/reporting?tab=periodic">Periodenbericht öffnen</Button>
            <Button href="/reporting">Operative Reporting Seite</Button>
          </Space>
        </Card>
      ) : null}

      {tab === 'xz' ? (
        <Card title="X / Z Referenz">
          <Alert
            type="info"
            showIcon
            message="X/Z görünümü mevcut reporting closings + tagesabschluss akışına bağlıdır."
            style={{ marginBottom: 12 }}
          />
          <Space>
            <Button type="primary" href="/reporting?tab=closings">X/Z Referenz öffnen</Button>
            <Button href="/tagesabschluss">Tagesabschluss</Button>
          </Space>
        </Card>
      ) : null}

      {tab === 'submissionQueue' ? (
        <Card title="Submission Queue / FinanzOnline">
          <Space style={{ marginBottom: 12 }}>
            <Button type="primary" href="/rksv/finanz-online-outbox">Volle Queue-Ansicht</Button>
          </Space>
          <Table
            rowKey={(r) => r.outboxId}
            loading={outboxQ.isLoading}
            dataSource={outboxQ.data ?? []}
            columns={outboxCols}
            pagination={{ pageSize: 10 }}
          />
        </Card>
      ) : null}
    </div>
  );
}
