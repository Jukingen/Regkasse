'use client';

/**
 * Formal Tagesbericht: Liste, Erzeugung (provisorisch), Filter nach Datum und Kasse.
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

const { RangePicker } = DatePicker;

type TagesberichtListItem = {
  id: string;
  viennaBusinessDate: string;
  cashRegisterId: string;
  registerNumber?: string;
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

export default function TagesberichtListPage() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);

  const [range, setRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
    dayjs().subtract(14, 'day'),
    dayjs(),
  ]);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();

  const registersQ = useGetApiCashRegister();

  const fromDate = range[0].format('YYYY-MM-DD');
  const toDate = range[1].format('YYYY-MM-DD');

  const listQ = useQuery({
    queryKey: ['tagesbericht', 'list', fromDate, toDate, cashRegisterId],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<TagesberichtListItem[]>('/api/reports/tagesbericht', {
        params: {
          fromDate,
          toDate,
          cashRegisterId,
        },
      });
      return data;
    },
  });

  const generateMut = useMutation({
    mutationFn: async () => {
      if (!cashRegisterId) {
        message.warning('Bitte eine Kasse wählen.');
        throw new Error('no register');
      }
      const { data } = await AXIOS_INSTANCE.post('/api/reports/tagesbericht/generate', {
        viennaBusinessDate: range[1].format('YYYY-MM-DD'),
        cashRegisterId,
        operatorUserIdScope: null,
        forceNewProvisional: false,
      });
      return data as { id: string };
    },
    onSuccess: (d) => {
      message.success('Tagesbericht erzeugt oder aktualisiert.');
      qc.invalidateQueries({ queryKey: ['tagesbericht'] });
      if (d?.id) window.location.href = `/reporting/tagesbericht/${d.id}`;
    },
    onError: () => message.error('Erzeugung fehlgeschlagen.'),
  });

  const columns: ColumnsType<TagesberichtListItem> = useMemo(
    () => [
      {
        title: 'Datum',
        dataIndex: 'viennaBusinessDate',
        render: (v: string) => dayjs(v).format('YYYY-MM-DD'),
      },
      { title: 'Kasse', dataIndex: 'registerNumber', render: (v, r) => v ?? r.cashRegisterId.slice(0, 8) },
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
        title: 'Brutto',
        dataIndex: 'grossSalesAmount',
        render: (v: number) => v?.toFixed(2),
      },
      {
        title: '',
        key: 'a',
        render: (_, r) => (
          <Link href={`/reporting/tagesbericht/${r.id}`}>Details</Link>
        ),
      },
    ],
    [],
  );

  return (
    <>
      <AdminPageHeader
        title="Tagesbericht (formal)"
        breadcrumbs={[adminOverviewCrumb(t), { title: 'Tagesbericht', href: '/reporting/tagesbericht' }]}
      />
      <Card style={{ marginBottom: 16 }}>
        <Space wrap>
          <RangePicker value={range} onChange={(v) => v && setRange(v as [dayjs.Dayjs, dayjs.Dayjs])} />
          <Select
            allowClear
            placeholder="Kasse"
            style={{ minWidth: 220 }}
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
          {canExport ? (
            <Button type="primary" loading={generateMut.isPending} onClick={() => generateMut.mutate()}>
              Tagesbericht für Enddatum erzeugen/aktualisieren
            </Button>
          ) : null}
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
          Provisorische Berichte können bis zur Finalisierung neu berechnet werden. Finalisierte Snapshots sind unveränderlich;
          Korrekturen erzeugen eine neue Berichtszeile.
        </Typography.Paragraph>
      </Card>
      <Card>
        <Table<TagesberichtListItem>
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
