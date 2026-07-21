'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import Link from 'next/link';
/**
 * Formal Tagesbericht: Liste, Erzeugung (provisorisch), Filter nach Datum und Kasse.
 */
import React, { useMemo, useState } from 'react';

import { type ReportFilterValues, ReportFilters } from '@/components/ReportFilters';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { FormalReportLanguageNotice } from '@/components/reporting/FormalReportLanguageNotice';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDate, formatNumber } from '@/i18n/formatting';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { useFiscalReportText } from '@/shared/reporting/useFiscalReportText';

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

  const fromDate = range[0].format('YYYY-MM-DD');
  const toDate = range[1].format('YYYY-MM-DD');

  const applyFilters = (values: Partial<ReportFilterValues>) => {
    if (values.dateRange?.[0] && values.dateRange[1]) {
      setRange(values.dateRange);
    }
    if (values.registerId) {
      setCashRegisterId(values.registerId);
    }
  };

  const handleFilterGenerate = (values: ReportFilterValues) => {
    applyFilters(values);
    void qc.invalidateQueries({ queryKey: ['tagesbericht', 'list'] });
  };

  const listQ = useQuery({
    queryKey: ['tagesbericht', 'list', fromDate, toDate, cashRegisterId],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<TagesberichtListItem[]>(
        '/api/reports/tagesbericht',
        {
          params: {
            fromDate,
            toDate,
            cashRegisterId,
          },
        }
      );
      return data;
    },
    enabled: Boolean(cashRegisterId),
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

  const handleGenerate = () => {
    if (!cashRegisterId) {
      message.warning(t('reporting.listShared.selectRegister'));
      return;
    }
    generateMut.mutate();
  };

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
                <Typography.Text
                  type="secondary"
                  style={{ fontSize: 12 }}
                  title={fiscalTooltip(hint.contentLang)}
                >
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
        render: (v: number) =>
          formatNumber(v, formatLocale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
      },
      {
        title: '',
        key: 'a',
        render: (_, r) => (
          <Link href={`/reporting/tagesbericht/${r.id}`}>{t('reporting.listShared.details')}</Link>
        ),
      },
    ],
    [t, formatLocale, fiscalTooltip, resolveFiscal]
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
      <Card style={{ marginBottom: 16 }} title={t('adminShell.reporting.filtersTitle')}>
        <ReportFilters
          onGenerate={handleFilterGenerate}
          onValuesChange={applyFilters}
          loading={listQ.isFetching}
          initialValues={{ dateRange: range }}
        />
        <Space wrap style={{ marginBottom: 12 }}>
          {canExport ? (
            <Button type="primary" loading={generateMut.isPending} onClick={handleGenerate}>
              {t('reporting.tagesbericht.list.generateButton')}
            </Button>
          ) : null}
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
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
