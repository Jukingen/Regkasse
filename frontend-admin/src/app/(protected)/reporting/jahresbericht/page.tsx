'use client';

/**
 * Formal Jahresbericht: liste, yıl bazlı filtre, provisional üretim/güncelleme.
 */
import React, { useMemo, useState } from 'react';
import { Button, Card, DatePicker, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import Link from 'next/link';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { CashRegister } from '@/api/generated/model';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

type JahresberichtListItem = {
  id: string;
  viennaYearStart: string;
  scopeKind: string;
  cashRegisterId?: string | null;
  registerNumber?: string | null;
  reportStatus: string;
  correctionKind: string;
  grossSalesAmount: number;
  createdAtUtc: string;
  submission: {
    lifecycle: string;
    operatorHintDe?: string;
    outboxStatus?: string;
  };
};

export default function JahresberichtListPage() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);

  const [fromYear, setFromYear] = useState(dayjs().subtract(2, 'year').startOf('year'));
  const [toYear, setToYear] = useState(dayjs().endOf('year'));
  const [reportYear, setReportYear] = useState(dayjs().startOf('year'));
  const [scopeKind, setScopeKind] = useState<'Register' | 'Company'>('Register');
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();

  const registersQ = useGetApiCashRegister();

  const listQ = useQuery({
    queryKey: ['jahresbericht', 'list', fromYear.format('YYYY'), toYear.format('YYYY'), scopeKind, cashRegisterId],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<JahresberichtListItem[]>('/api/reports/jahresbericht', {
        params: {
          fromYear: fromYear.format('YYYY-01-01'),
          toYear: toYear.format('YYYY-01-01'),
          scopeKind,
          cashRegisterId: scopeKind === 'Register' ? cashRegisterId : undefined,
        },
      });
      return data;
    },
  });

  const generateMut = useMutation({
    mutationFn: async () => {
      if (scopeKind === 'Register' && !cashRegisterId) {
        message.warning('Bitte eine Kasse wählen (Register-Umfang).');
        throw new Error('no register');
      }
      const { data } = await AXIOS_INSTANCE.post('/api/reports/jahresbericht/generate', {
        viennaYearAnyDay: reportYear.format('YYYY-01-01'),
        scopeKind,
        cashRegisterId: scopeKind === 'Company' ? null : cashRegisterId,
        forceNewProvisional: false,
      });
      return data as { id: string };
    },
    onSuccess: (d) => {
      message.success('Jahresbericht erzeugt oder aktualisiert.');
      qc.invalidateQueries({ queryKey: ['jahresbericht'] });
      if (d?.id) window.location.href = `/reporting/jahresbericht/${d.id}`;
    },
    onError: () => message.error('Erzeugung fehlgeschlagen.'),
  });

  const columns: ColumnsType<JahresberichtListItem> = useMemo(
    () => [
      { title: 'Jahr', dataIndex: 'viennaYearStart', render: (v: string) => dayjs(v).format('YYYY') },
      { title: 'Umfang', dataIndex: 'scopeKind', render: (s: string) => <Tag>{s}</Tag> },
      {
        title: 'Kasse',
        dataIndex: 'registerNumber',
        render: (v: string | null | undefined, r) => (r.scopeKind === 'Company' ? '—' : v ?? r.cashRegisterId?.slice(0, 8) ?? '—'),
      },
      {
        title: 'Status',
        dataIndex: 'reportStatus',
        render: (s: string) => <Tag color={s === 'Finalized' ? 'blue' : s === 'Provisional' ? 'gold' : 'default'}>{s}</Tag>,
      },
      {
        title: 'FO / Übermittlung',
        dataIndex: 'submission',
        render: (_: unknown, row) => (
          <Space direction="vertical" size={0}>
            <Tag>{row.submission.lifecycle}</Tag>
            {row.submission.operatorHintDe ? <Typography.Text type="secondary" style={{ fontSize: 12 }}>{row.submission.operatorHintDe}</Typography.Text> : null}
          </Space>
        ),
      },
      { title: 'Brutto (Monate)', dataIndex: 'grossSalesAmount', render: (v: number) => v?.toFixed(2) },
      { title: '', key: 'a', render: (_, r) => <Link href={`/reporting/jahresbericht/${r.id}`}>Details</Link> },
    ],
    [],
  );

  return (
    <>
      <AdminPageHeader
        title="Jahresbericht (formal)"
        breadcrumbs={[adminOverviewCrumb(t), { title: 'Jahresbericht', href: '/reporting/jahresbericht' }]}
      />
      <Card style={{ marginBottom: 16 }}>
        <Space wrap>
          <DatePicker picker="year" value={fromYear} onChange={(v) => v && setFromYear(v.startOf('year'))} />
          <DatePicker picker="year" value={toYear} onChange={(v) => v && setToYear(v.startOf('year'))} />
          <Select
            style={{ minWidth: 140 }}
            value={scopeKind}
            onChange={(v) => {
              setScopeKind(v);
              if (v === 'Company') setCashRegisterId(undefined);
            }}
            options={[
              { value: 'Register', label: 'Register (eine Kasse)' },
              { value: 'Company', label: 'Company (alle Kassen)' },
            ]}
          />
          <Select
            allowClear={scopeKind === 'Company'}
            placeholder="Kasse (bei Register)"
            style={{ minWidth: 220 }}
            disabled={scopeKind === 'Company'}
            value={cashRegisterId}
            onChange={setCashRegisterId}
            options={((registersQ.data as CashRegister[] | undefined) ?? []).map((r) => ({
              value: r.id,
              label: `${r.registerNumber} — ${r.location}`,
            }))}
          />
          <Button onClick={() => listQ.refetch()} loading={listQ.isFetching}>Aktualisieren</Button>
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 8 }}>
          Jahresberichte aggregieren finalisierte Monatsberichte im Kalenderjahr; Rohzahlungen dienen der Abstimmung.
        </Typography.Paragraph>
        <Space wrap align="start">
          <div>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
              Berichtsjahr (neu / Aktualisierung)
            </Typography.Text>
            <DatePicker picker="year" value={reportYear} onChange={(v) => v && setReportYear(v.startOf('year'))} />
          </div>
          {canExport ? (
            <Button type="primary" loading={generateMut.isPending} onClick={() => generateMut.mutate()} style={{ marginTop: 22 }}>
              Jahresbericht erzeugen/aktualisieren
            </Button>
          ) : null}
        </Space>
      </Card>
      <Card>
        <Table<JahresberichtListItem>
          rowKey="id"
          loading={listQ.isLoading}
          dataSource={listQ.data ?? []}
          columns={columns}
          pagination={{ pageSize: 20 }}
        />
      </Card>
    </>
  );
}
