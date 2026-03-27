'use client';

/**
 * Formal Monatsbericht: Liste, provisorische Erzeugung/Aktualisierung, Filter Monat und Kassen-/Firmenkontext.
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

type MonatsberichtListItem = {
  id: string;
  viennaMonthStart: string;
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

export default function MonatsberichtListPage() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);

  const [listRange, setListRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
    dayjs().subtract(5, 'month').startOf('month'),
    dayjs().endOf('month'),
  ]);
  const [reportMonth, setReportMonth] = useState(dayjs().startOf('month'));
  const [scopeKind, setScopeKind] = useState<'Register' | 'Company'>('Register');
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();

  const registersQ = useGetApiCashRegister();

  const fromMonth = listRange[0].startOf('month').format('YYYY-MM-DD');
  const toMonth = listRange[1].startOf('month').format('YYYY-MM-DD');

  const listQ = useQuery({
    queryKey: ['monatsbericht', 'list', fromMonth, toMonth, scopeKind, cashRegisterId],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<MonatsberichtListItem[]>('/api/reports/monatsbericht', {
        params: {
          fromMonth,
          toMonth,
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
      const { data } = await AXIOS_INSTANCE.post('/api/reports/monatsbericht/generate', {
        viennaMonthAnyDay: reportMonth.format('YYYY-MM-DD'),
        scopeKind,
        cashRegisterId: scopeKind === 'Company' ? null : cashRegisterId,
        forceNewProvisional: false,
      });
      return data as { id: string };
    },
    onSuccess: (d) => {
      message.success('Monatsbericht erzeugt oder aktualisiert.');
      qc.invalidateQueries({ queryKey: ['monatsbericht'] });
      if (d?.id) window.location.href = `/reporting/monatsbericht/${d.id}`;
    },
    onError: () => message.error('Erzeugung fehlgeschlagen.'),
  });

  const columns: ColumnsType<MonatsberichtListItem> = useMemo(
    () => [
      {
        title: 'Monat',
        dataIndex: 'viennaMonthStart',
        render: (v: string) => dayjs(v).format('YYYY-MM'),
      },
      {
        title: 'Umfang',
        dataIndex: 'scopeKind',
        render: (s: string) => <Tag>{s}</Tag>,
      },
      {
        title: 'Kasse',
        dataIndex: 'registerNumber',
        render: (v: string | null | undefined, r) =>
          r.scopeKind === 'Company' ? '—' : v ?? r.cashRegisterId?.slice(0, 8) ?? '—',
      },
      {
        title: 'Status',
        dataIndex: 'reportStatus',
        render: (s: string) => (
          <Tag color={s === 'Finalized' ? 'blue' : s === 'Provisional' ? 'gold' : 'default'}>{s}</Tag>
        ),
      },
      {
        title: 'FO / Übermittlung',
        dataIndex: 'submission',
        render: (_: unknown, row) => (
          <Space direction="vertical" size={0}>
            <Tag>{row.submission.lifecycle}</Tag>
            {row.submission.operatorHintDe ? (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {row.submission.operatorHintDe}
              </Typography.Text>
            ) : null}
          </Space>
        ),
      },
      {
        title: 'Brutto (Tagesberichte)',
        dataIndex: 'grossSalesAmount',
        render: (v: number) => v?.toFixed(2),
      },
      {
        title: '',
        key: 'a',
        render: (_, r) => <Link href={`/reporting/monatsbericht/${r.id}`}>Details</Link>,
      },
    ],
    [],
  );

  return (
    <>
      <AdminPageHeader
        title="Monatsbericht (formal)"
        breadcrumbs={[adminOverviewCrumb(t), { title: 'Monatsbericht', href: '/reporting/monatsbericht' }]}
      />
      <Card style={{ marginBottom: 16 }}>
        <Space wrap>
          <DatePicker.RangePicker
            picker="month"
            value={listRange}
            onChange={(v) => v && setListRange(v as [dayjs.Dayjs, dayjs.Dayjs])}
          />
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
          <Button onClick={() => listQ.refetch()} loading={listQ.isFetching}>
            Aktualisieren
          </Button>
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 8 }}>
          Monatsberichte aggregieren finalisierte Tagesberichte im Kalendermonat; Rohzahlungen dienen der Abstimmung.
        </Typography.Paragraph>
        <Space wrap align="start">
          <div>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
              Berichtsmonat (neu / Aktualisierung)
            </Typography.Text>
            <DatePicker picker="month" value={reportMonth} onChange={(v) => v && setReportMonth(v.startOf('month'))} />
          </div>
          {canExport ? (
            <Button type="primary" loading={generateMut.isPending} onClick={() => generateMut.mutate()} style={{ marginTop: 22 }}>
              Monatsbericht erzeugen/aktualisieren
            </Button>
          ) : null}
        </Space>
      </Card>
      <Card>
        <Table<MonatsberichtListItem>
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
