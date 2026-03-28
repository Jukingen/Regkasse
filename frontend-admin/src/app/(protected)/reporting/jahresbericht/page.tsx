'use client';

/**
 * Formal Jahresbericht: list, year filter, provisional generation/update.
 */
import React, { useMemo, useState } from 'react';
import { Button, Card, DatePicker, Select, Space, Table, Tag, Typography, message } from 'antd';
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

export default function JahresberichtListPage() {
  const { t, formatLocale } = useI18n();
  const { fiscalTooltip, resolveFiscal } = useFiscalReportText();
  const qc = useQueryClient();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);

  const [fromYear, setFromYear] = useState(dayjs().subtract(2, 'year').startOf('year'));
  const [toYear, setToYear] = useState(dayjs().endOf('year'));
  const [reportYear, setReportYear] = useState(dayjs().startOf('year'));
  const [scopeKind, setScopeKind] = useState<'Register' | 'Company'>('Register');
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();

  const registersQ = useGetApiCashRegister();
  const registersData = registersQ.data as unknown;
  const registerRows = Array.isArray((registersData as { registers?: CashRegister[] } | undefined)?.registers)
    ? ((registersData as { registers?: CashRegister[] }).registers ?? [])
    : Array.isArray(registersData)
      ? (registersData as CashRegister[])
      : [];

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
        message.warning(t('reporting.listShared.selectRegisterScope'));
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
      message.success(t('reporting.jahresbericht.list.messages.generateSuccess'));
      qc.invalidateQueries({ queryKey: ['jahresbericht'] });
      if (d?.id) window.location.href = `/reporting/jahresbericht/${d.id}`;
    },
    onError: () => message.error(t('reporting.listShared.generateError')),
  });

  const columns: ColumnsType<JahresberichtListItem> = useMemo(
    () => [
      {
        title: t('reporting.jahresbericht.list.columnYear'),
        dataIndex: 'viennaYearStart',
        render: (v: string) => dayjs(v).format('YYYY'),
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
            <Space direction="vertical" size={0}>
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
        title: t('reporting.jahresbericht.list.columnGross'),
        dataIndex: 'grossSalesAmount',
        render: (v: number) => formatNumber(v, formatLocale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
      },
      {
        title: '',
        key: 'a',
        render: (_, r) => <Link href={`/reporting/jahresbericht/${r.id}`}>{t('reporting.listShared.details')}</Link>,
      },
    ],
    [t, formatLocale, fiscalTooltip, resolveFiscal],
  );

  return (
    <>
      <AdminPageHeader
        title={t('reporting.jahresbericht.list.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('reporting.jahresbericht.list.breadcrumb'), href: '/reporting/jahresbericht' },
        ]}
      />
      <FormalReportLanguageNotice />
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
          {t('reporting.jahresbericht.list.intro')}
        </Typography.Paragraph>
        <Space wrap align="start">
          <div>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
              {t('reporting.jahresbericht.list.yearPickerLabel')}
            </Typography.Text>
            <DatePicker picker="year" value={reportYear} onChange={(v) => v && setReportYear(v.startOf('year'))} />
          </div>
          {canExport ? (
            <Button type="primary" loading={generateMut.isPending} onClick={() => generateMut.mutate()} style={{ marginTop: 22 }}>
              {t('reporting.jahresbericht.list.generateButton')}
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
