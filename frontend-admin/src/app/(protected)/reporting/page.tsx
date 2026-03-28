'use client';

/**
 * POS payment–based operational reports: filters, tabs (summary, period, interim X, closing Z reference), CSV export.
 */
import React, { useCallback, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Row,
  Select,
  Space,
  Spin,
  Switch,
  Table,
  Tabs,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import { useSearchParams } from 'next/navigation';
import { useMutation } from '@tanstack/react-query';

dayjs.extend(utc);
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { formatCurrency } from '@/i18n/formatting';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import {
  getApiReportsOperationalExportSummaryCsv,
  useGetApiReportsOperationalClosings,
  useGetApiReportsOperationalInterim,
  useGetApiReportsOperationalPeriodic,
  useGetApiReportsOperationalSummary,
} from '@/api/generated/reports/reports';
import { useGetApiUserManagement } from '@/api/generated/user-management/user-management';
import type { CashRegister, ClosingReferenceRowDto } from '@/api/generated/model';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

const { RangePicker } = DatePicker;

const PAYMENT_METHOD_OPTIONS: { value: number; labelDe: string }[] = [
  { value: 0, labelDe: 'Bar' },
  { value: 1, labelDe: 'Karte' },
  { value: 2, labelDe: 'Überweisung' },
  { value: 3, labelDe: 'Scheck' },
  { value: 4, labelDe: 'Gutschein' },
  { value: 5, labelDe: 'Mobil' },
];

function formatPaymentMethodKey(key: string | undefined): string {
  if (key === undefined || key === '') return '—';
  const n = Number.parseInt(key, 10);
  const opt = PAYMENT_METHOD_OPTIONS.find((o) => o.value === n);
  if (opt) return opt.labelDe;
  return key;
}

export default function ReportingPage() {
  const { t, formatLocale } = useI18n();
  const searchParams = useSearchParams();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);

  const initialTab = searchParams?.get('tab');
  const [tab, setTab] = useState<string>(
    initialTab && ['summary', 'periodic', 'interim', 'closings'].includes(initialTab) ? initialTab : 'summary'
  );
  const [dateRange, setDateRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
    dayjs().startOf('month'),
    dayjs().endOf('month'),
  ]);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();
  const [cashierId, setCashierId] = useState<string | undefined>();
  const [paymentMethod, setPaymentMethod] = useState<number | undefined>();
  const [activeOnly, setActiveOnly] = useState(true);
  const [periodPreset, setPeriodPreset] = useState<'day' | 'week' | 'month' | 'custom'>('custom');
  const [closingStatusFilter, setClosingStatusFilter] = useState<string | undefined>();
  const [exporting, setExporting] = useState(false);
  const [freezing, setFreezing] = useState(false);

  const startDate = dateRange[0].format('YYYY-MM-DD');
  const endDate = dateRange[1].format('YYYY-MM-DD');

  const filterParams = useMemo(
    () => ({
      startDate,
      endDate,
      cashRegisterId,
      cashierId,
      paymentMethod,
      activeOnly,
    }),
    [startDate, endDate, cashRegisterId, cashierId, paymentMethod, activeOnly],
  );

  const interimParams = useMemo(
    () => ({
      cashRegisterId,
      cashierId,
      paymentMethod,
      activeOnly,
    }),
    [cashRegisterId, cashierId, paymentMethod, activeOnly],
  );

  const periodicParams = useMemo(() => {
    const base = {
      cashRegisterId,
      cashierId,
      paymentMethod,
      activeOnly,
      periodPreset,
    };
    if (periodPreset === 'custom') {
      return { ...base, startDate, endDate };
    }
    return base;
  }, [cashRegisterId, cashierId, paymentMethod, activeOnly, periodPreset, startDate, endDate]);

  const closingsParams = useMemo(
    () => ({
      startDate,
      endDate,
      cashRegisterId,
    }),
    [startDate, endDate, cashRegisterId],
  );

  const { data: registers } = useGetApiCashRegister();
  const registersData = registers as unknown;
  const registerRows = Array.isArray((registersData as { registers?: CashRegister[] } | undefined)?.registers)
    ? ((registersData as { registers?: CashRegister[] }).registers ?? [])
    : Array.isArray(registersData)
      ? (registersData as CashRegister[])
      : [];
  const { data: usersPage } = useGetApiUserManagement({ pageSize: 500, isActive: true });

  const summaryQ = useGetApiReportsOperationalSummary(filterParams, {
    query: { enabled: tab === 'summary' },
  });
  const periodicQ = useGetApiReportsOperationalPeriodic(periodicParams, {
    query: { enabled: tab === 'periodic' },
  });
  const interimQ = useGetApiReportsOperationalInterim(interimParams, {
    query: { enabled: tab === 'interim' },
  });
  const closingsQ = useGetApiReportsOperationalClosings(closingsParams, {
    query: { enabled: tab === 'closings' },
  });

  const loading =
    (tab === 'summary' && summaryQ.isLoading) ||
    (tab === 'periodic' && periodicQ.isLoading) ||
    (tab === 'interim' && interimQ.isLoading) ||
    (tab === 'closings' && closingsQ.isLoading);

  const activeSummary =
    tab === 'summary'
      ? summaryQ.data
      : tab === 'periodic'
        ? periodicQ.data?.summary
        : tab === 'interim'
          ? interimQ.data?.summary
          : undefined;

  const closingsRows = useMemo(
    () => closingsQ.data?.dailyClosings ?? [],
    [closingsQ.data?.dailyClosings],
  );
  const statusOptions = useMemo(() => {
    const s = new Set(closingsRows.map((r) => r.status).filter(Boolean) as string[]);
    return Array.from(s).sort();
  }, [closingsRows]);

  const closingsFiltered = useMemo(() => {
    if (!closingStatusFilter) return closingsRows;
    return closingsRows.filter((r) => r.status === closingStatusFilter);
  }, [closingsRows, closingStatusFilter]);

  const formatMoney = useCallback((v: number) => formatCurrency(v, formatLocale), [formatLocale]);

  const onExportCsv = useCallback(async () => {
    if (!canExport) {
      message.warning(t('adminShell.reporting.exportNoPermission'));
      return;
    }
    setExporting(true);
    try {
      const blob = await getApiReportsOperationalExportSummaryCsv(filterParams);
      // Browser globals (client-only handler; eslint env lacks DOM globals).
      const w = globalThis as unknown as {
        URL: { createObjectURL: (b: unknown) => string; revokeObjectURL: (u: string) => void };
        document: { createElement: (t: string) => { href: string; download: string; click: () => void } };
      };
      const url = w.URL.createObjectURL(blob);
      const a = w.document.createElement('a');
      a.href = url;
      a.download = `operational-summary-${startDate}-${endDate}.csv`;
      a.click();
      w.URL.revokeObjectURL(url);
      message.success(t('adminShell.reporting.exportCsv'));
    } catch {
      message.error(t('adminShell.reporting.exportCsvFailed'));
    } finally {
      setExporting(false);
    }
  }, [canExport, filterParams, startDate, endDate, t]);

  const freezeMutation = useMutation({
    mutationFn: async () => {
      const payload = {
        periodPreset,
        startDate: periodPreset === 'custom' ? startDate : undefined,
        endDate: periodPreset === 'custom' ? endDate : undefined,
        cashRegisterId,
        cashierId,
        paymentMethod,
        activeOnly,
      };
      await AXIOS_INSTANCE.post('/api/reports/operational/periodic/freeze', payload);
    },
    onSuccess: () => message.success(t('adminShell.reporting.freezeSuccess')),
    onError: () => message.error(t('adminShell.reporting.freezeError')),
  });

  const payCols: ColumnsType<{ methodKey?: string; count?: number; totalAmount?: number }> = [
    {
      title: t('adminShell.reporting.methodKey'),
      dataIndex: 'methodKey',
      render: (v: string) => formatPaymentMethodKey(v),
    },
    { title: t('adminShell.reporting.count'), dataIndex: 'count' },
    {
      title: t('adminShell.reporting.amount'),
      dataIndex: 'totalAmount',
      render: (v: number) => formatMoney(Number(v ?? 0)),
    },
  ];

  const staffCols: ColumnsType<{ cashierId?: string; count?: number; totalAmount?: number }> = [
    { title: t('adminShell.reporting.cashierId'), dataIndex: 'cashierId' },
    { title: t('adminShell.reporting.count'), dataIndex: 'count' },
    {
      title: t('adminShell.reporting.amount'),
      dataIndex: 'totalAmount',
      render: (v: number) => formatMoney(Number(v ?? 0)),
    },
  ];

  const closingCols: ColumnsType<ClosingReferenceRowDto> = [
    {
      title: t('adminShell.reporting.closingTime'),
      dataIndex: 'closingDateUtc',
      render: (v: string) => (v ? dayjs(v).utc().format('DD.MM.YYYY HH:mm:ss') : '—'),
    },
    { title: t('adminShell.reporting.closingStatus'), dataIndex: 'status' },
    {
      title: t('adminShell.reporting.closingAmount'),
      dataIndex: 'totalAmount',
      render: (v: number) => formatMoney(Number(v ?? 0)),
    },
    { title: t('adminShell.reporting.closingTx'), dataIndex: 'transactionCount' },
    {
      title: t('adminShell.reporting.tseSigned'),
      dataIndex: 'hasTseSignature',
      render: (v: boolean) => (v ? t('adminShell.reporting.yes') : t('adminShell.reporting.no')),
    },
  ];

  const registerOptions = useMemo(
    () =>
      registerRows
        .filter((r: CashRegister) => r.id)
        .map((r: CashRegister) => ({
          value: r.id as string,
          label: `${r.registerNumber} — ${r.location}`,
        })),
    [registerRows],
  );

  const cashierOptions = useMemo(() => {
    const users = usersPage?.items ?? [];
    return users
      .filter((u) => u.id)
      .map((u) => ({
        value: u.id as string,
        label: [u.firstName, u.lastName].filter(Boolean).join(' ') || u.userName || u.id,
      }));
  }, [usersPage]);

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader
        title={t('adminShell.reporting.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('adminShell.reporting.pageTitle'), href: '/reporting' },
        ]}
        actions={
          <Space wrap>
            <Button href="/reporting/report-center">{t('nav.reportCenter')}</Button>
            {canExport ? (
              <Button type="primary" loading={exporting} onClick={() => void onExportCsv()}>
                {t('adminShell.reporting.exportCsv')}
              </Button>
            ) : (
              <Typography.Text type="secondary">{t('adminShell.reporting.exportNoPermission')}</Typography.Text>
            )}
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('adminShell.reporting.pageIntro')}
        </Typography.Paragraph>
      </AdminPageHeader>

      <Card size="small" style={{ marginBottom: 16 }} title={t('adminShell.reporting.filtersTitle')}>
        <Row gutter={[16, 12]}>
          <Col xs={24} md={10}>
            <Typography.Text type="secondary">{t('adminShell.reporting.dateRange')}</Typography.Text>
            <div style={{ marginTop: 4 }}>
              <RangePicker
                style={{ width: '100%' }}
                value={dateRange}
                onChange={(d) => {
                  if (d?.[0] && d[1]) setDateRange([d[0], d[1]]);
                }}
              />
            </div>
          </Col>
          <Col xs={24} md={7}>
            <Typography.Text type="secondary">{t('adminShell.reporting.register')}</Typography.Text>
            <Select
              allowClear
              placeholder={t('adminShell.reporting.registerAll')}
              style={{ width: '100%', marginTop: 4 }}
              options={registerOptions}
              value={cashRegisterId}
              onChange={(v) => setCashRegisterId(v)}
            />
          </Col>
          <Col xs={24} md={7}>
            <Typography.Text type="secondary">{t('adminShell.reporting.cashier')}</Typography.Text>
            <Select
              showSearch
              allowClear
              placeholder={t('adminShell.reporting.cashierAll')}
              style={{ width: '100%', marginTop: 4 }}
              options={cashierOptions}
              value={cashierId}
              onChange={(v) => setCashierId(v)}
              filterOption={(input, option) =>
                (option?.label ?? '').toString().toLowerCase().includes(input.toLowerCase())
              }
            />
          </Col>
          <Col xs={24} md={8}>
            <Typography.Text type="secondary">{t('adminShell.reporting.paymentMethod')}</Typography.Text>
            <Select
              allowClear
              placeholder={t('adminShell.reporting.paymentAll')}
              style={{ width: '100%', marginTop: 4 }}
              options={PAYMENT_METHOD_OPTIONS.map((o) => ({ value: o.value, label: o.labelDe }))}
              value={paymentMethod}
              onChange={(v) => setPaymentMethod(v ?? undefined)}
            />
          </Col>
          <Col xs={24} md={8}>
            <Typography.Text type="secondary">{t('adminShell.reporting.activeOnly')}</Typography.Text>
            <div style={{ marginTop: 8 }}>
              <Switch checked={activeOnly} onChange={setActiveOnly} />
            </div>
          </Col>
        </Row>
      </Card>

      <Tabs
        activeKey={tab}
        onChange={setTab}
        items={[
          {
            key: 'summary',
            label: t('adminShell.reporting.tabSummary'),
            children: (
              <Spin spinning={!!loading}>
                {activeSummary?.interimDisclaimer && (
                  <Alert type="info" showIcon style={{ marginBottom: 12 }} message={activeSummary.interimDisclaimer} />
                )}
                <Row gutter={16} style={{ marginBottom: 16 }}>
                  <Col xs={12} md={6}>
                    <Card size="small">
                      <Typography.Text type="secondary">{t('adminShell.reporting.gross')}</Typography.Text>
                      <div style={{ fontSize: 20, fontWeight: 600 }}>
                        {formatMoney(Number(activeSummary?.grossTotalAmount ?? 0))}
                      </div>
                    </Card>
                  </Col>
                  <Col xs={12} md={6}>
                    <Card size="small">
                      <Typography.Text type="secondary">{t('adminShell.reporting.tax')}</Typography.Text>
                      <div style={{ fontSize: 20, fontWeight: 600 }}>
                        {formatMoney(Number(activeSummary?.taxTotalAmount ?? 0))}
                      </div>
                    </Card>
                  </Col>
                  <Col xs={12} md={6}>
                    <Card size="small">
                      <Typography.Text type="secondary">{t('adminShell.reporting.refunds')}</Typography.Text>
                      <div style={{ fontSize: 20, fontWeight: 600 }}>
                        {formatMoney(Number(activeSummary?.refundAmountTotal ?? 0))}
                      </div>
                    </Card>
                  </Col>
                  <Col xs={12} md={6}>
                    <Card size="small">
                      <Typography.Text type="secondary">{t('adminShell.reporting.rows')}</Typography.Text>
                      <div style={{ fontSize: 20, fontWeight: 600 }}>{activeSummary?.paymentRowCount ?? 0}</div>
                    </Card>
                  </Col>
                </Row>
                <Typography.Title level={5}>{t('adminShell.reporting.byPaymentTitle')}</Typography.Title>
                <Table
                  size="small"
                  rowKey={(r) => `${r.methodKey}-${r.count}`}
                  columns={payCols}
                  dataSource={[...(activeSummary?.byPaymentMethod ?? [])]}
                  pagination={false}
                />
                <Typography.Title level={5} style={{ marginTop: 24 }}>
                  {t('adminShell.reporting.byStaffTitle')}
                </Typography.Title>
                <Table
                  size="small"
                  rowKey={(r) => `${r.cashierId}-${r.count}`}
                  columns={staffCols}
                  dataSource={[...(activeSummary?.byCashier ?? [])]}
                  pagination={false}
                />
              </Spin>
            ),
          },
          {
            key: 'periodic',
            label: t('adminShell.reporting.tabPeriodic'),
            children: (
              <Spin spinning={!!loading}>
                <Space direction="vertical" style={{ width: '100%' }} size="middle">
                  <div>
                    <Typography.Text type="secondary">{t('adminShell.reporting.periodPreset')}</Typography.Text>
                    <Select
                      style={{ width: '100%', maxWidth: 360, marginTop: 4, display: 'block' }}
                      value={periodPreset}
                      onChange={(v) => setPeriodPreset(v)}
                      options={[
                        { value: 'custom', label: t('adminShell.reporting.presetCustom') },
                        { value: 'day', label: t('adminShell.reporting.presetDay') },
                        { value: 'week', label: t('adminShell.reporting.presetWeek') },
                        { value: 'month', label: t('adminShell.reporting.presetMonth') },
                      ]}
                    />
                  </div>
                  <Space>
                    <Button
                      type="primary"
                      loading={freezeMutation.isPending || freezing}
                      onClick={async () => {
                        setFreezing(true);
                        try {
                          await freezeMutation.mutateAsync();
                        } finally {
                          setFreezing(false);
                        }
                      }}
                    >
                      Periodenbericht einfrieren
                    </Button>
                  </Space>
                  {periodicQ.data?.summary && (
                    <>
                      <Typography.Title level={5}>{t('adminShell.reporting.totalsTitle')}</Typography.Title>
                      <Row gutter={16}>
                        <Col span={8}>
                          {t('adminShell.reporting.gross')}:{' '}
                          <strong>{formatMoney(Number(periodicQ.data.summary.grossTotalAmount ?? 0))}</strong>
                        </Col>
                        <Col span={8}>
                          {t('adminShell.reporting.refunds')}:{' '}
                          <strong>{formatMoney(Number(periodicQ.data.summary.refundAmountTotal ?? 0))}</strong>
                        </Col>
                        <Col span={8}>
                          {t('adminShell.reporting.rows')}: <strong>{periodicQ.data.summary.paymentRowCount}</strong>
                        </Col>
                      </Row>
                      <Table
                        size="small"
                        rowKey={(r) => `${r.methodKey}-${r.count}`}
                        columns={payCols}
                        dataSource={[...(periodicQ.data.summary.byPaymentMethod ?? [])]}
                        pagination={false}
                      />
                    </>
                  )}
                </Space>
              </Spin>
            ),
          },
          {
            key: 'interim',
            label: t('adminShell.reporting.tabInterim'),
            children: (
              <Spin spinning={!!loading}>
                {interimQ.data?.summary?.interimDisclaimer && (
                  <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 12 }}
                    message={interimQ.data.summary.interimDisclaimer}
                  />
                )}
                <Row gutter={16} style={{ marginBottom: 16 }}>
                  <Col xs={12} md={8}>
                    <Card size="small">
                      <Typography.Text type="secondary">{t('adminShell.reporting.gross')}</Typography.Text>
                      <div style={{ fontSize: 20, fontWeight: 600 }}>
                        {formatMoney(Number(interimQ.data?.summary?.grossTotalAmount ?? 0))}
                      </div>
                    </Card>
                  </Col>
                  <Col xs={12} md={8}>
                    <Card size="small">
                      <Typography.Text type="secondary">{t('adminShell.reporting.rows')}</Typography.Text>
                      <div style={{ fontSize: 20, fontWeight: 600 }}>{interimQ.data?.summary?.paymentRowCount ?? 0}</div>
                    </Card>
                  </Col>
                </Row>
                <Table
                  size="small"
                  rowKey={(r) => `${r.methodKey}-${r.count}`}
                  columns={payCols}
                  dataSource={[...(interimQ.data?.summary?.byPaymentMethod ?? [])]}
                  pagination={false}
                />
              </Spin>
            ),
          },
          {
            key: 'closings',
            label: t('adminShell.reporting.tabClosings'),
            children: (
              <Spin spinning={!!loading}>
                {closingsQ.data?.operatorNote && (
                  <Alert type="info" showIcon style={{ marginBottom: 12 }} message={closingsQ.data.operatorNote} />
                )}
                <Space style={{ marginBottom: 12 }} wrap>
                  <Typography.Text type="secondary">{t('adminShell.reporting.closingStatus')}</Typography.Text>
                  <Select
                    allowClear
                    placeholder={t('adminShell.reporting.cashierAll')}
                    style={{ minWidth: 200 }}
                    value={closingStatusFilter}
                    onChange={setClosingStatusFilter}
                    options={statusOptions.map((s) => ({ value: s, label: s }))}
                  />
                </Space>
                <Table
                  size="small"
                  rowKey={(r) => String(r.id ?? `${r.cashRegisterId}-${r.closingDateUtc}`)}
                  columns={closingCols}
                  dataSource={closingsFiltered}
                  pagination={{ pageSize: 20 }}
                />
              </Spin>
            ),
          },
        ]}
      />
    </div>
  );
}
