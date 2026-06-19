'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Formal Tagesbericht: Liste, Erzeugung (provisorisch), Filter nach Datum und Kasse.
 */
import React, { useMemo, useState } from 'react';
import { Button, Card, DatePicker, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import Link from 'next/link';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { formatDate, formatNumber } from '@/i18n/formatting';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { CashRegister } from '@/api/generated/model';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { FormalReportLanguageNotice } from '@/components/reporting/FormalReportLanguageNotice';
import { useFiscalReportText } from '@/shared/reporting/useFiscalReportText';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

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
    operatorHintEn?: string | null;
    outboxStatus?: string;
  };
};

function reportStatusTagLabel(s: string, t: (k: string) => string): string {
  if (s === 'Finalized' || s === 'Provisional') return t(`reporting.listShared.reportStatus.${s}`);
  return s;
}

export default function TagesberichtListPage() {
  const { message } = useAntdApp();

  const { t, formatLocale } = useI18n();
  const { fiscalTooltip, resolveFiscal } = useFiscalReportText();
  const qc = useQueryClient();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);

  const [range, setRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
    dayjs().subtract(14, 'day'),
    dayjs(),
  ]);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();

  const registersQ = useGetApiCashRegister();
  const registersData = registersQ.data as unknown;
  const registerRows = Array.isArray((registersData as { registers?: CashRegister[] } | undefined)?.registers)
    ? ((registersData as { registers?: CashRegister[] }).registers ?? [])
    : Array.isArray(registersData)
      ? (registersData as CashRegister[])
      : [];

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
        message.warning(t('reporting.listShared.selectRegister'));
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
      message.success(t('reporting.tagesbericht.list.messages.generateSuccess'));
      qc.invalidateQueries({ queryKey: ['tagesbericht'] });
      if (d?.id) window.location.href = `/reporting/tagesbericht/${d.id}`;
    },
    onError: () => message.error(t('reporting.listShared.generateError')),
  });

  const columns: ColumnsType<TagesberichtListItem> = useMemo(
    () => [
      {
        title: t('reporting.tagesbericht.list.columnDate'),
        dataIndex: 'viennaBusinessDate',
        render: (v: string) => formatDate(v, formatLocale),
      },
      {
        title: t('reporting.listShared.columns.register'),
        dataIndex: 'registerNumber',
        render: (v, r) => v ?? r.cashRegisterId.slice(0, 8),
      },
      {
        title: t('reporting.listShared.columns.status'),
        dataIndex: 'reportStatus',
        render: (s: string) => (
          <Tag color={s === 'Finalized' ? 'blue' : s === 'Provisional' ? 'gold' : 'default'}>
            {reportStatusTagLabel(s, t)}
          </Tag>
        ),
      },
      {
        title: t('reporting.listShared.columns.foSubmission'),
        dataIndex: 'submission',
        render: (_: unknown, row) => {
          const hint = resolveFiscal(row.submission.operatorHintDe, row.submission.operatorHintEn);
          return (
            <Space orientation="vertical" size={0}>
              <Tag>{row.submission.lifecycle}</Tag>
              {hint ? (
                <Typography.Text type="secondary" style={{ fontSize: 12 }} title={fiscalTooltip(hint.contentLang)}>
                  {hint.text}
                </Typography.Text>
              ) : null}
            </Space>
          );
        },
      },
      {
        title: t('reporting.listShared.columns.gross'),
        dataIndex: 'grossSalesAmount',
        render: (v: number) => formatNumber(v, formatLocale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
      },
      {
        title: '',
        key: 'a',
        render: (_, r) => (
          <Link href={`/reporting/tagesbericht/${r.id}`}>{t('reporting.listShared.details')}</Link>
        ),
      },
    ],
    [t, formatLocale, fiscalTooltip, resolveFiscal],
  );

  return (
    <>
      <AdminPageHeader
        title={t('reporting.tagesbericht.list.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('reporting.tagesbericht.list.breadcrumb'), href: '/reporting/tagesbericht' },
        ]}
      />
      <FormalReportLanguageNotice />
      <Card style={{ marginBottom: 16 }}>
        <Space wrap>
          <RangePicker format={DAYJS_DATE_FORMAT} value={range} onChange={(v) => v && setRange(v as [dayjs.Dayjs, dayjs.Dayjs])} />
          <Select
            allowClear
            placeholder={t('reporting.listShared.registerPlaceholder')}
            style={{ minWidth: 220 }}
            value={cashRegisterId}
            onChange={setCashRegisterId}
            options={registerRows.map((r) => ({
              value: r.id,
              label: `${r.registerNumber} — ${r.location}`,
            }))}
          />
          <Button onClick={() => listQ.refetch()} loading={listQ.isFetching}>
            {t('reporting.listShared.refresh')}
          </Button>
          {canExport ? (
            <Button type="primary" loading={generateMut.isPending} onClick={() => generateMut.mutate()}>
              {t('reporting.tagesbericht.list.generateButton')}
            </Button>
          ) : null}
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
          {t('reporting.tagesbericht.list.intro')}
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
