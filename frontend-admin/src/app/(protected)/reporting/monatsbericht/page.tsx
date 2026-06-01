'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Formal Monatsbericht: Liste, provisorische Erzeugung/Aktualisierung, Filter Monat und Kassen-/Firmenkontext.
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
import { formatNumber } from '@/i18n/formatting';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { CashRegister } from '@/api/generated/model';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { FormalReportLanguageNotice } from '@/components/reporting/FormalReportLanguageNotice';
import { useFiscalReportText } from '@/shared/reporting/useFiscalReportText';

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
    operatorHintEn?: string | null;
    outboxStatus?: string;
  };
};

function reportStatusTagLabel(s: string, t: (k: string) => string): string {
  if (s === 'Finalized' || s === 'Provisional') return t(`reporting.listShared.reportStatus.${s}`);
  return s;
}

function scopeKindLabel(s: string, t: (k: string) => string): string {
  if (s === 'Register' || s === 'Company') return t(`reporting.listShared.scopeKind.${s}`);
  return s;
}

export default function MonatsberichtListPage() {
  const { message } = useAntdApp();

  const { t, formatLocale } = useI18n();
  const { fiscalTooltip, resolveFiscal } = useFiscalReportText();
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
  const registersData = registersQ.data as unknown;
  const registerRows = Array.isArray((registersData as { registers?: CashRegister[] } | undefined)?.registers)
    ? ((registersData as { registers?: CashRegister[] }).registers ?? [])
    : Array.isArray(registersData)
      ? (registersData as CashRegister[])
      : [];

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
        message.warning(t('reporting.listShared.selectRegisterScope'));
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
      message.success(t('reporting.monatsbericht.list.messages.generateSuccess'));
      qc.invalidateQueries({ queryKey: ['monatsbericht'] });
      if (d?.id) window.location.href = `/reporting/monatsbericht/${d.id}`;
    },
    onError: () => message.error(t('reporting.listShared.generateError')),
  });

  const columns: ColumnsType<MonatsberichtListItem> = useMemo(
    () => [
      {
        title: t('reporting.monatsbericht.list.columnMonth'),
        dataIndex: 'viennaMonthStart',
        render: (v: string) => dayjs(v).format('YYYY-MM'),
      },
      {
        title: t('reporting.listShared.columns.scope'),
        dataIndex: 'scopeKind',
        render: (s: string) => <Tag>{scopeKindLabel(s, t)}</Tag>,
      },
      {
        title: t('reporting.listShared.columns.register'),
        dataIndex: 'registerNumber',
        render: (v: string | null | undefined, r) =>
          r.scopeKind === 'Company'
            ? t('reporting.listShared.emptyDash')
            : v ?? r.cashRegisterId?.slice(0, 8) ?? t('reporting.listShared.emptyDash'),
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
        title: t('reporting.monatsbericht.list.columnGross'),
        dataIndex: 'grossSalesAmount',
        render: (v: number) => formatNumber(v, formatLocale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
      },
      {
        title: '',
        key: 'a',
        render: (_, r) => <Link href={`/reporting/monatsbericht/${r.id}`}>{t('reporting.listShared.details')}</Link>,
      },
    ],
    [t, formatLocale, fiscalTooltip, resolveFiscal],
  );

  return (
    <>
      <AdminPageHeader
        title={t('reporting.monatsbericht.list.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('reporting.monatsbericht.list.breadcrumb'), href: '/reporting/monatsbericht' },
        ]}
      />
      <FormalReportLanguageNotice />
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
              { value: 'Register', label: t('reporting.listShared.scopeOptionRegister') },
              { value: 'Company', label: t('reporting.listShared.scopeOptionCompany') },
            ]}
          />
          <Select
            allowClear={scopeKind === 'Company'}
            placeholder={t('reporting.listShared.registerPlaceholderScoped')}
            style={{ minWidth: 220 }}
            disabled={scopeKind === 'Company'}
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
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 8 }}>
          {t('reporting.monatsbericht.list.intro')}
        </Typography.Paragraph>
        <Space wrap align="start">
          <div>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
              {t('reporting.monatsbericht.list.monthPickerLabel')}
            </Typography.Text>
            <DatePicker picker="month" value={reportMonth} onChange={(v) => v && setReportMonth(v.startOf('month'))} />
          </div>
          {canExport ? (
            <Button type="primary" loading={generateMut.isPending} onClick={() => generateMut.mutate()} style={{ marginTop: 22 }}>
              {t('reporting.monatsbericht.list.generateButton')}
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
