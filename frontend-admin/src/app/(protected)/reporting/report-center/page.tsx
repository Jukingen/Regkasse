'use client';

/**
 * Report Center: formelle Berichte, eingefrorene Perioden, X/Z-Referenz und Meldungs-Sicht.
 * Kurztexte für Operatoren (de-DE); technische Details nur in Diagnose-Bereichen.
 */
import React, { useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Descriptions,
  Modal,
  Row,
  Segmented,
  Select,
  Space,
  Spin,
  Switch,
  Table,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';
import { FormalReportLanguageNotice } from '@/components/reporting/FormalReportLanguageNotice';
import { LegalExportCompletenessBanner } from '@/components/reporting/LegalExportCompletenessBanner';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDate, formatDateTime } from '@/i18n/formatting';
import { DAYJS_DATE_FORMAT, formatUserMonthYear } from '@/lib/dateFormatter';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import { useGetApiAdminFinanzonlineOutbox } from '@/api/generated/admin/admin';
import type { CashRegister } from '@/api/generated/model';
import type { FinanzOnlineOutboxItemDto } from '@/api/generated/model/finanzOnlineOutboxItemDto';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { ReportChainTimelineDrawer, type FormalReportTypeKey } from '@/components/reporting/ReportChainTimelineDrawer';
import { ReportDualBadges } from '@/components/reporting/ReportWorkspaceBadges';
import {
  matchesReportDocFilter,
  matchesSubmissionFilter,
  type ReportDocFilterKey,
  type SubmissionFilterKey,
} from '@/components/reporting/reportWorkspaceLabels';

const { RangePicker } = DatePicker;

type WorkspaceTab =
  | 'tagesbericht'
  | 'monatsbericht'
  | 'jahresbericht'
  | 'periodenbericht'
  | 'xz'
  | 'submissionQueue';

type TagesRow = {
  id: string;
  viennaBusinessDate: string;
  reportStatus: string;
  correctionKind: string;
  submission: { lifecycle: string };
  registerNumber?: string;
};
type MonatsRow = {
  id: string;
  viennaMonthStart: string;
  scopeKind: string;
  reportStatus: string;
  correctionKind: string;
  submission: { lifecycle: string };
  registerNumber?: string;
};
type JahresRow = {
  id: string;
  viennaYearStart: string;
  scopeKind: string;
  reportStatus: string;
  correctionKind: string;
  submission: { lifecycle: string };
  registerNumber?: string;
};

type PeriodenRunRow = {
  id: string;
  createdAtUtc: string;
  periodPreset: string;
  periodStartLocalDate: string;
  periodEndLocalDate: string;
  scopeKind: string;
  paymentRowCount: number;
  grossTotalAmount: number;
  snapshotSchemaVersion: string;
  cashRegisterId?: string;
};

type PeriodenDetailDto = {
  id: string;
  snapshotSchemaVersion: string;
  warnings: string[];
  summary?: { grossTotalAmount?: number; taxTotalAmount?: number; paymentRowCount?: number };
};

type XzClosingRow = {
  id: string;
  cashRegisterId: string;
  closingDateUtc: string;
  status: string;
  totalAmount: number;
  totalTaxAmount: number;
  transactionCount: number;
  hasTseSignature: boolean;
};

type XzBundleDto = {
  schemaVersion: string;
  generatedAtUtc: string;
  viennaBusinessDate: string;
  scopeKind: string;
  cashRegisterId?: string;
  activeOnly: boolean;
  isCurrentBusinessDay: boolean;
  legalDisclaimers: string[];
  informationalWarnings: string[];
  parts: { kind: string; label: string; description?: string }[];
  linkedClosingIds: string[];
  interimVsFullDaySnapshot?: { interimGrossTotal: number; fullDayGrossTotal: number; deltaGross: number };
  operationalVsClosing?: {
    primaryClosingId: string;
    operationalGrossTotal: number;
    closingTotalAmount: number;
    deltaGross: number;
    note: string;
  };
  interimXLike?: { summary?: { grossTotalAmount: number; taxTotalAmount: number; paymentRowCount: number } };
  fullDayOperationalSummary: {
    grossTotalAmount: number;
    taxTotalAmount: number;
    paymentRowCount: number;
    refundRowCount: number;
    byPaymentMethod?: { methodKey?: string; count?: number; totalAmount?: number }[];
  };
  closingReference: { dailyClosings: XzClosingRow[]; operatorNote?: string };
};

function formalReportHref(aggregateType: string | null | undefined, aggregateId: string | null | undefined): string | null {
  if (!aggregateType || !aggregateId) return null;
  switch (aggregateType) {
    case 'TagesberichtReport':
      return `/reporting/tagesbericht/${aggregateId}`;
    case 'MonatsberichtReport':
      return `/reporting/monatsbericht/${aggregateId}`;
    case 'JahresberichtReport':
      return `/reporting/jahresbericht/${aggregateId}`;
    default:
      return null;
  }
}

function workspaceCategoryForTab(tab: WorkspaceTab): 'accounting' | 'legal' | 'operational' | 'diagnostic' {
  if (tab === 'tagesbericht' || tab === 'monatsbericht' || tab === 'jahresbericht') return 'accounting';
  if (tab === 'periodenbericht') return 'legal';
  if (tab === 'xz') return 'operational';
  return 'diagnostic';
}

export default function ReportCenterPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canFinanzOnlineView = hasPermission(PERMISSIONS.FINANZONLINE_VIEW);

  const [tab, setTab] = useState<WorkspaceTab>('tagesbericht');
  const [dateRange, setDateRange] = useState<[Dayjs, Dayjs]>([dayjs().subtract(30, 'day'), dayjs()]);
  const [periodenRange, setPeriodenRange] = useState<[Dayjs, Dayjs]>([dayjs().subtract(60, 'day'), dayjs()]);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();
  const [scopeKind, setScopeKind] = useState<'all' | 'Register' | 'Company'>('all');
  const [reportDocFilter, setReportDocFilter] = useState<ReportDocFilterKey>('all');
  const [submissionFilter, setSubmissionFilter] = useState<SubmissionFilterKey>('all');

  const [xzBusinessDate, setXzBusinessDate] = useState(dayjs());
  const [xzCashRegisterId, setXzCashRegisterId] = useState<string | undefined>();
  const [xzActiveOnly, setXzActiveOnly] = useState(true);

  const [chainOpen, setChainOpen] = useState(false);
  const [chainType, setChainType] = useState<FormalReportTypeKey>('tagesbericht');
  const [chainReportId, setChainReportId] = useState<string | null>(null);

  const [periodenModalOpen, setPeriodenModalOpen] = useState(false);
  const [periodenDetailId, setPeriodenDetailId] = useState<string | null>(null);

  const [outboxAggregateFilter, setOutboxAggregateFilter] = useState<string | undefined>(undefined);

  const { data: registers } = useGetApiCashRegister();
  const registersData = registers as unknown;
  const registerRows = Array.isArray((registersData as { registers?: CashRegister[] } | undefined)?.registers)
    ? ((registersData as { registers?: CashRegister[] }).registers ?? [])
    : Array.isArray(registersData)
      ? (registersData as CashRegister[])
      : [];

  const pickerMode = tab === 'monatsbericht' ? 'month' : tab === 'jahresbericht' ? 'year' : 'date';

  const tagesParams = useMemo(() => {
    const fromDate = dateRange[0].format('YYYY-MM-DD');
    const toDate = dateRange[1].format('YYYY-MM-DD');
    return { fromDate, toDate, cashRegisterId };
  }, [dateRange, cashRegisterId]);

  const monatsParams = useMemo(() => {
    const fromMonth = dateRange[0].startOf('month').format('YYYY-MM-DD');
    const toMonth = dateRange[1].startOf('month').format('YYYY-MM-DD');
    return {
      fromMonth,
      toMonth,
      cashRegisterId,
      scopeKind: scopeKind === 'all' ? undefined : scopeKind,
    };
  }, [dateRange, cashRegisterId, scopeKind]);

  const jahresParams = useMemo(() => {
    const fromYear = dateRange[0].startOf('year').format('YYYY-MM-DD');
    const toYear = dateRange[1].startOf('year').format('YYYY-MM-DD');
    return {
      fromYear,
      toYear,
      cashRegisterId,
      scopeKind: scopeKind === 'all' ? undefined : scopeKind,
    };
  }, [dateRange, cashRegisterId, scopeKind]);

  const tagesQ = useQuery({
    queryKey: ['report-center', 'tages', tagesParams],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<TagesRow[]>('/api/reports/tagesbericht', { params: tagesParams });
      return data;
    },
    enabled: tab === 'tagesbericht',
  });

  const monatsQ = useQuery({
    queryKey: ['report-center', 'monats', monatsParams],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<MonatsRow[]>('/api/reports/monatsbericht', { params: monatsParams });
      return data;
    },
    enabled: tab === 'monatsbericht',
  });

  const jahresQ = useQuery({
    queryKey: ['report-center', 'jahres', jahresParams],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<JahresRow[]>('/api/reports/jahresbericht', { params: jahresParams });
      return data;
    },
    enabled: tab === 'jahresbericht',
  });

  const periodenParams = useMemo(() => {
    const fromDate = periodenRange[0].format('YYYY-MM-DD');
    const toDate = periodenRange[1].format('YYYY-MM-DD');
    return { fromDate, toDate, cashRegisterId, limit: 100 };
  }, [periodenRange, cashRegisterId]);

  const periodenQ = useQuery({
    queryKey: ['report-center', 'perioden-frozen', periodenParams],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<PeriodenRunRow[]>('/api/reports/operational/periodic/frozen', {
        params: periodenParams,
      });
      return data;
    },
    enabled: tab === 'periodenbericht',
  });

  const periodenDetailQ = useQuery({
    queryKey: ['report-center', 'perioden-detail', periodenDetailId],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<PeriodenDetailDto>(
        `/api/reports/operational/periodic/frozen/${periodenDetailId}`
      );
      return data;
    },
    enabled: periodenModalOpen && !!periodenDetailId,
  });

  const xzQ = useQuery({
    queryKey: ['report-center', 'xz-bundle', xzBusinessDate.format('YYYY-MM-DD'), xzCashRegisterId ?? '', xzActiveOnly],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<XzBundleDto>('/api/reports/operational/xz-reference-bundle', {
        params: {
          businessDate: xzBusinessDate.format('YYYY-MM-DD'),
          cashRegisterId: xzCashRegisterId,
          activeOnly: xzActiveOnly,
        },
      });
      return data;
    },
    enabled: tab === 'xz',
  });

  const outboxQueryParams = useMemo(
    () => ({
      bucket: 'in_flight' as const,
      limit: 80,
      aggregateType: outboxAggregateFilter,
    }),
    [outboxAggregateFilter]
  );

  const outboxQ = useGetApiAdminFinanzonlineOutbox(outboxQueryParams, {
    query: { enabled: tab === 'submissionQueue' && canFinanzOnlineView },
  });

  const filteredTages = useMemo(() => {
    const rows = tagesQ.data ?? [];
    return rows.filter(
      (r) =>
        matchesReportDocFilter(r.reportStatus, reportDocFilter) &&
        matchesSubmissionFilter(r.submission?.lifecycle, submissionFilter)
    );
  }, [tagesQ.data, reportDocFilter, submissionFilter]);

  const filteredMonats = useMemo(() => {
    const rows = monatsQ.data ?? [];
    return rows.filter(
      (r) =>
        matchesReportDocFilter(r.reportStatus, reportDocFilter) &&
        matchesSubmissionFilter(r.submission?.lifecycle, submissionFilter)
    );
  }, [monatsQ.data, reportDocFilter, submissionFilter]);

  const filteredJahres = useMemo(() => {
    const rows = jahresQ.data ?? [];
    return rows.filter(
      (r) =>
        matchesReportDocFilter(r.reportStatus, reportDocFilter) &&
        matchesSubmissionFilter(r.submission?.lifecycle, submissionFilter)
    );
  }, [jahresQ.data, reportDocFilter, submissionFilter]);

  const registerOptions = useMemo(
    () =>
      registerRows
        .filter((r: CashRegister) => r.id)
        .map((r: CashRegister) => ({
          value: r.id as string,
          label: `${r.registerNumber} — ${r.location}`,
        })),
    [registerRows]
  );

  const openChain = (kind: FormalReportTypeKey, id: string) => {
    setChainType(kind);
    setChainReportId(id);
    setChainOpen(true);
  };

  const docFilterOptions: { value: ReportDocFilterKey; label: string }[] = [
    { value: 'all', label: t('adminShell.reporting.reportCenter.filterAll') },
    { value: 'Provisional', label: t('adminShell.reporting.reportCenter.docProvisional') },
    { value: 'Finalized', label: t('adminShell.reporting.reportCenter.docFinalized') },
    { value: 'Superseded', label: t('adminShell.reporting.reportCenter.docSuperseded') },
  ];

  const subFilterOptions: { value: SubmissionFilterKey; label: string }[] = [
    { value: 'all', label: t('adminShell.reporting.reportCenter.filterAll') },
    { value: 'notSubmitted', label: t('adminShell.reporting.reportCenter.filterSubNotSubmitted') },
    { value: 'inFlight', label: t('adminShell.reporting.reportCenter.filterSubInFlight') },
    { value: 'accepted', label: t('adminShell.reporting.reportCenter.filterSubAccepted') },
    { value: 'rejectedOrReview', label: t('adminShell.reporting.reportCenter.filterSubRejected') },
  ];

  const tagesCols: ColumnsType<TagesRow> = useMemo(
    () => [
      {
        title: t('adminShell.reporting.reportCenter.colPeriod'),
        dataIndex: 'viennaBusinessDate',
        render: (v: string) => formatDate(v, ''),
      },
      {
        title: t('adminShell.reporting.reportCenter.colRegister'),
        dataIndex: 'registerNumber',
        render: (v: string | undefined) => v ?? '—',
      },
      {
        title: t('adminShell.reporting.reportCenter.filterReport'),
        key: 'doc',
        render: (_, r) => <ReportDualBadges reportStatus={r.reportStatus} lifecycle={r.submission?.lifecycle} t={t} />,
      },
      { title: t('adminShell.reporting.reportCenter.colCorrection'), dataIndex: 'correctionKind' },
      {
        title: t('adminShell.reporting.reportCenter.colActions'),
        key: 'a',
        render: (_, r) => (
          <Space size="small" wrap>
            <Link href={`/reporting/tagesbericht/${r.id}`}>{t('adminShell.reporting.reportCenter.actionDetail')}</Link>
            <Button type="link" size="small" style={{ padding: 0 }} onClick={() => openChain('tagesbericht', r.id)}>
              {t('adminShell.reporting.reportCenter.actionChain')}
            </Button>
          </Space>
        ),
      },
    ],
    [t]
  );

  const monatsCols: ColumnsType<MonatsRow> = useMemo(
    () => [
      {
        title: t('adminShell.reporting.reportCenter.colPeriod'),
        dataIndex: 'viennaMonthStart',
        render: (v: string) => formatUserMonthYear(v) || '—',
      },
      {
        title: t('adminShell.reporting.reportCenter.colScope'),
        dataIndex: 'scopeKind',
        render: (s: string) =>
          s === 'Company' ? t('adminShell.reporting.reportCenter.scopeCompany') : t('adminShell.reporting.reportCenter.scopeRegister'),
      },
      {
        title: t('adminShell.reporting.reportCenter.filterReport'),
        key: 'doc',
        render: (_, r) => <ReportDualBadges reportStatus={r.reportStatus} lifecycle={r.submission?.lifecycle} t={t} />,
      },
      { title: t('adminShell.reporting.reportCenter.colCorrection'), dataIndex: 'correctionKind' },
      {
        title: t('adminShell.reporting.reportCenter.colActions'),
        key: 'a',
        render: (_, r) => (
          <Space size="small" wrap>
            <Link href={`/reporting/monatsbericht/${r.id}`}>{t('adminShell.reporting.reportCenter.actionDetail')}</Link>
            <Button type="link" size="small" style={{ padding: 0 }} onClick={() => openChain('monatsbericht', r.id)}>
              {t('adminShell.reporting.reportCenter.actionChain')}
            </Button>
          </Space>
        ),
      },
    ],
    [t]
  );

  const jahresCols: ColumnsType<JahresRow> = useMemo(
    () => [
      {
        title: t('adminShell.reporting.reportCenter.colPeriod'),
        dataIndex: 'viennaYearStart',
        render: (v: string) => dayjs(v).format('YYYY'),
      },
      {
        title: t('adminShell.reporting.reportCenter.colScope'),
        dataIndex: 'scopeKind',
        render: (s: string) =>
          s === 'Company' ? t('adminShell.reporting.reportCenter.scopeCompany') : t('adminShell.reporting.reportCenter.scopeRegister'),
      },
      {
        title: t('adminShell.reporting.reportCenter.filterReport'),
        key: 'doc',
        render: (_, r) => <ReportDualBadges reportStatus={r.reportStatus} lifecycle={r.submission?.lifecycle} t={t} />,
      },
      { title: t('adminShell.reporting.reportCenter.colCorrection'), dataIndex: 'correctionKind' },
      {
        title: t('adminShell.reporting.reportCenter.colActions'),
        key: 'a',
        render: (_, r) => (
          <Space size="small" wrap>
            <Link href={`/reporting/jahresbericht/${r.id}`}>{t('adminShell.reporting.reportCenter.actionDetail')}</Link>
            <Button type="link" size="small" style={{ padding: 0 }} onClick={() => openChain('jahresbericht', r.id)}>
              {t('adminShell.reporting.reportCenter.actionChain')}
            </Button>
          </Space>
        ),
      },
    ],
    [t]
  );

  const periodenCols: ColumnsType<PeriodenRunRow> = useMemo(
    () => [
      {
        title: t('adminShell.reporting.dateRange'),
        key: 'p',
        render: (_, r) =>
          `${formatDate(r.periodStartLocalDate, '')} – ${formatDate(r.periodEndLocalDate, '')}`,
      },
      { title: t('adminShell.reporting.periodPreset'), dataIndex: 'periodPreset' },
      {
        title: t('adminShell.reporting.reportCenter.colScope'),
        dataIndex: 'scopeKind',
      },
      { title: t('adminShell.reporting.rows'), dataIndex: 'paymentRowCount' },
      {
        title: t('adminShell.reporting.amount'),
        dataIndex: 'grossTotalAmount',
        render: (v: number) => Number(v ?? 0).toFixed(2),
      },
      {
        title: t('adminShell.reporting.reportCenter.colActions'),
        key: 'd',
        render: (_, r) => (
          <Button
            type="link"
            size="small"
            style={{ padding: 0 }}
            onClick={() => {
              setPeriodenDetailId(r.id);
              setPeriodenModalOpen(true);
            }}
          >
            {t('adminShell.reporting.reportCenter.periodenOpenRow')}
          </Button>
        ),
      },
    ],
    [t]
  );

  const xzClosingCols: ColumnsType<XzClosingRow> = useMemo(
    () => [
      { title: 'ID', dataIndex: 'id', render: (v: string) => <Typography.Text style={{ fontSize: 12 }}>{v}</Typography.Text> },
      { title: t('adminShell.reporting.reportCenter.register'), dataIndex: 'cashRegisterId', render: (v: string) => v.slice(0, 8) },
      { title: t('adminShell.reporting.closingTime'), dataIndex: 'closingDateUtc', render: (v: string) => formatDateTime(v, '') },
      { title: t('adminShell.reporting.closingStatus'), dataIndex: 'status' },
      { title: t('adminShell.reporting.closingAmount'), dataIndex: 'totalAmount' },
      { title: t('adminShell.reporting.closingTx'), dataIndex: 'transactionCount' },
      {
        title: t('adminShell.reporting.tseSigned'),
        dataIndex: 'hasTseSignature',
        render: (v: boolean) => (v ? t('adminShell.reporting.yes') : t('adminShell.reporting.no')),
      },
    ],
    [t]
  );

  const outboxItems: FinanzOnlineOutboxItemDto[] = outboxQ.data?.items ?? [];

  const outboxCols = useMemo(
    () => [
      {
        title: t('adminShell.reporting.reportCenter.colActions'),
        key: 'lnk',
        render: (_: unknown, r: FinanzOnlineOutboxItemDto) => {
          const href = formalReportHref(r.aggregateType, r.aggregateId);
          return href ? (
            <Link href={href}>{t('adminShell.reporting.reportCenter.outboxGoReport')}</Link>
          ) : (
            <Typography.Text type="secondary">—</Typography.Text>
          );
        },
      },
      { title: t('adminShell.reporting.reportCenter.queueColBericht'), dataIndex: 'aggregateType' },
      {
        title: t('adminShell.reporting.closingStatus'),
        dataIndex: 'status',
        render: (s: string | undefined, r: FinanzOnlineOutboxItemDto) => (
          <Typography.Text>{r.operatorStatusLabel ?? s}</Typography.Text>
        ),
      },
      {
        title: t('adminShell.reporting.reportCenter.queueColHint'),
        dataIndex: 'operatorFailureHint',
        ellipsis: true,
        render: (h: string | undefined) => h ?? '—',
      },
      {
        title: t('adminShell.reporting.closingTime'),
        dataIndex: 'createdAtUtc',
        render: (v: string | undefined) => (v ? formatDateTime(v, '') : '—'),
      },
    ],
    [t]
  );

  const cat = workspaceCategoryForTab(tab);
  const stripLabel =
    cat === 'accounting'
      ? t('adminShell.reporting.reportCenter.workspaceStripAccounting')
      : cat === 'legal'
        ? t('adminShell.reporting.reportCenter.workspaceStripLegal')
        : cat === 'operational'
          ? t('adminShell.reporting.reportCenter.workspaceStripOperational')
          : t('adminShell.reporting.reportCenter.workspaceStripDiagnostic');

  const formalFilters = (
    <Card size="small" style={{ marginBottom: 12 }} title={t('adminShell.reporting.reportCenter.filtersTitle')}>
      <Row gutter={[12, 12]}>
        <Col xs={24} lg={8}>
          <Typography.Text type="secondary">{t('adminShell.reporting.reportCenter.dateRange')}</Typography.Text>
          <div style={{ marginTop: 4 }}>
            <RangePicker
              style={{ width: '100%' }}
              picker={pickerMode as 'date' | 'month' | 'year'}
              format={pickerMode === 'date' ? DAYJS_DATE_FORMAT : undefined}
              value={dateRange}
              onChange={(v) => {
                if (v?.[0] && v[1]) setDateRange([v[0], v[1]]);
              }}
            />
          </div>
        </Col>
        <Col xs={24} lg={6}>
          <Typography.Text type="secondary">{t('adminShell.reporting.reportCenter.register')}</Typography.Text>
          <div style={{ marginTop: 4 }}>
            <Select
              allowClear
              placeholder={t('adminShell.reporting.reportCenter.registerAll')}
              style={{ width: '100%' }}
              options={registerOptions}
              value={cashRegisterId}
              onChange={(v) => setCashRegisterId(v)}
            />
          </div>
        </Col>
        {(tab === 'monatsbericht' || tab === 'jahresbericht') && (
          <Col xs={24} lg={5}>
            <Typography.Text type="secondary">{t('adminShell.reporting.reportCenter.scope')}</Typography.Text>
            <div style={{ marginTop: 4 }}>
              <Select
                style={{ width: '100%' }}
                value={scopeKind}
                onChange={(v) => setScopeKind(v)}
                options={[
                  { value: 'all', label: t('adminShell.reporting.reportCenter.scopeAll') },
                  { value: 'Register', label: t('adminShell.reporting.reportCenter.scopeRegister') },
                  { value: 'Company', label: t('adminShell.reporting.reportCenter.scopeCompany') },
                ]}
              />
            </div>
          </Col>
        )}
        <Col xs={24} lg={5}>
          <Typography.Text type="secondary">{t('adminShell.reporting.reportCenter.filterReport')}</Typography.Text>
          <div style={{ marginTop: 4 }}>
            <Select
              style={{ width: '100%' }}
              value={reportDocFilter}
              onChange={(v) => setReportDocFilter(v)}
              options={docFilterOptions}
            />
          </div>
        </Col>
        <Col xs={24} lg={5}>
          <Typography.Text type="secondary">{t('adminShell.reporting.reportCenter.filterSubmission')}</Typography.Text>
          <div style={{ marginTop: 4 }}>
            <Select
              style={{ width: '100%' }}
              value={submissionFilter}
              onChange={(v) => setSubmissionFilter(v)}
              options={subFilterOptions}
            />
          </div>
        </Col>
      </Row>
    </Card>
  );

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader
        title={t('adminShell.reporting.reportCenter.pageTitle')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('nav.reportCenter'), href: '/reporting/report-center' }]}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
          {t('adminShell.reporting.reportCenter.pageIntro')}
        </Typography.Paragraph>
        <Typography.Text type="secondary">
          {stripLabel}
        </Typography.Text>
      </AdminPageHeader>

      <FormalReportLanguageNotice />

      <Card size="small" style={{ marginBottom: 16 }}>
        <Segmented
          block
          value={tab}
          onChange={(v) => setTab(v as WorkspaceTab)}
          options={[
            { label: t('adminShell.reporting.reportCenter.tabTages'), value: 'tagesbericht' },
            { label: t('adminShell.reporting.reportCenter.tabMonats'), value: 'monatsbericht' },
            { label: t('adminShell.reporting.reportCenter.tabJahres'), value: 'jahresbericht' },
            { label: t('adminShell.reporting.reportCenter.tabPerioden'), value: 'periodenbericht' },
            { label: t('adminShell.reporting.reportCenter.tabXz'), value: 'xz' },
            { label: t('adminShell.reporting.reportCenter.tabQueue'), value: 'submissionQueue' },
          ]}
        />
      </Card>

      {tab === 'tagesbericht' ? (
        <>
          {formalFilters}
          <Card
            title={
              <Space wrap>
                <span>{t('adminShell.reporting.reportCenter.listTitleTages')}</span>
                <Link href="/reporting/tagesbericht">{t('adminShell.reporting.reportCenter.linkFullList')}</Link>
              </Space>
            }
          >
            <Table
              rowKey="id"
              loading={tagesQ.isLoading}
              dataSource={filteredTages}
              columns={tagesCols}
              pagination={{ pageSize: 12 }}
            />
          </Card>
        </>
      ) : null}

      {tab === 'monatsbericht' ? (
        <>
          {formalFilters}
          <Card
            title={
              <Space wrap>
                <span>{t('adminShell.reporting.reportCenter.listTitleMonats')}</span>
                <Link href="/reporting/monatsbericht">{t('adminShell.reporting.reportCenter.linkFullList')}</Link>
              </Space>
            }
          >
            <Table
              rowKey="id"
              loading={monatsQ.isLoading}
              dataSource={filteredMonats}
              columns={monatsCols}
              pagination={{ pageSize: 12 }}
            />
          </Card>
        </>
      ) : null}

      {tab === 'jahresbericht' ? (
        <>
          {formalFilters}
          <Card
            title={
              <Space wrap>
                <span>{t('adminShell.reporting.reportCenter.listTitleJahres')}</span>
                <Link href="/reporting/jahresbericht">{t('adminShell.reporting.reportCenter.linkFullList')}</Link>
              </Space>
            }
          >
            <Table
              rowKey="id"
              loading={jahresQ.isLoading}
              dataSource={filteredJahres}
              columns={jahresCols}
              pagination={{ pageSize: 12 }}
            />
          </Card>
        </>
      ) : null}

      {tab === 'periodenbericht' ? (
        <Card title={t('adminShell.reporting.reportCenter.tabPerioden')}>
          <Alert type="info" showIcon title={t('adminShell.reporting.reportCenter.periodenSectionIntro')} style={{ marginBottom: 12 }} />
          <Card size="small" style={{ marginBottom: 12 }} title={t('adminShell.reporting.reportCenter.filtersTitle')}>
            <Row gutter={[12, 12]}>
              <Col xs={24} md={12}>
                <Typography.Text type="secondary">{t('adminShell.reporting.reportCenter.dateRange')}</Typography.Text>
                <div style={{ marginTop: 4 }}>
                  <RangePicker format={DAYJS_DATE_FORMAT}
                    style={{ width: '100%' }}
                    value={periodenRange}
                    onChange={(v) => {
                      if (v?.[0] && v[1]) setPeriodenRange([v[0], v[1]]);
                    }}
                  />
                </div>
              </Col>
              <Col xs={24} md={12}>
                <Typography.Text type="secondary">{t('adminShell.reporting.reportCenter.register')}</Typography.Text>
                <div style={{ marginTop: 4 }}>
                  <Select
                    allowClear
                    placeholder={t('adminShell.reporting.reportCenter.registerAll')}
                    style={{ width: '100%' }}
                    options={registerOptions}
                    value={cashRegisterId}
                    onChange={(v) => setCashRegisterId(v)}
                  />
                </div>
              </Col>
            </Row>
          </Card>
          <Space style={{ marginBottom: 12 }} wrap>
            <Button type="default" href="/reporting?tab=periodic">
              {t('adminShell.reporting.reportCenter.periodenLinkLive')}
            </Button>
          </Space>
          <Table
            rowKey="id"
            loading={periodenQ.isLoading}
            dataSource={periodenQ.data ?? []}
            columns={periodenCols}
            pagination={{ pageSize: 12 }}
          />
        </Card>
      ) : null}

      {tab === 'xz' ? (
        <Card title={t('adminShell.reporting.reportCenter.tabXz')}>
          <Alert type="warning" showIcon title={t('adminShell.reporting.reportCenter.xzLegalNote')} style={{ marginBottom: 8 }} />
          <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
            {t('adminShell.reporting.reportCenter.xzOperationalNote')}
          </Typography.Paragraph>
          <Row gutter={[16, 12]} style={{ marginBottom: 16 }}>
            <Col xs={24} md={8}>
              <Typography.Text type="secondary">{t('adminShell.reporting.dateRange')}</Typography.Text>
              <div style={{ marginTop: 4 }}>
                <DatePicker format={DAYJS_DATE_FORMAT}
                  style={{ width: '100%' }}
                  value={xzBusinessDate}
                  onChange={(d) => {
                    if (d) setXzBusinessDate(d);
                  }}
                />
              </div>
            </Col>
            <Col xs={24} md={10}>
              <Typography.Text type="secondary">{t('adminShell.reporting.reportCenter.register')}</Typography.Text>
              <div style={{ marginTop: 4 }}>
                <Select
                  allowClear
                  placeholder={t('adminShell.reporting.reportCenter.registerAll')}
                  style={{ width: '100%' }}
                  options={registerOptions}
                  value={xzCashRegisterId}
                  onChange={(v) => setXzCashRegisterId(v)}
                />
              </div>
            </Col>
            <Col xs={24} md={6}>
              <Typography.Text type="secondary">{t('adminShell.reporting.activeOnly')}</Typography.Text>
              <div style={{ marginTop: 4 }}>
                <Switch checked={xzActiveOnly} onChange={setXzActiveOnly} />
              </div>
            </Col>
          </Row>

          <Spin spinning={xzQ.isLoading}>
            {xzQ.data?.legalDisclaimers?.length ? (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 12 }}
                title={
                  <Space orientation="vertical" size={4}>
                    {xzQ.data.legalDisclaimers.map((line, i) => (
                      <Typography.Text key={`legal-${i}`}>{line}</Typography.Text>
                    ))}
                  </Space>
                }
              />
            ) : null}

            {xzQ.data?.informationalWarnings?.length ? (
              <Alert
                type="info"
                showIcon
                style={{ marginBottom: 12 }}
                title={
                  <ul style={{ margin: 0, paddingLeft: 18 }}>
                    {xzQ.data.informationalWarnings.map((w) => (
                      <li key={w}>{w}</li>
                    ))}
                  </ul>
                }
              />
            ) : null}

            <Descriptions bordered size="small" column={2} style={{ marginBottom: 16 }}>
              <Descriptions.Item label={t('adminShell.reporting.reportCenter.detailLabelSchema')}>
                {xzQ.data?.schemaVersion ?? '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('adminShell.reporting.reportCenter.detailLabelUtc')}>
                {xzQ.data?.generatedAtUtc ? formatDateTime(xzQ.data.generatedAtUtc, '', { second: '2-digit' }) : '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('adminShell.reporting.dateRange')}>
                {xzQ.data?.viennaBusinessDate ? formatDate(xzQ.data.viennaBusinessDate, '') : '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('adminShell.reporting.tabInterim')}>
                {xzQ.data?.isCurrentBusinessDay ? t('adminShell.reporting.yes') : t('adminShell.reporting.no')}
              </Descriptions.Item>
              <Descriptions.Item label={t('adminShell.reporting.reportCenter.colScope')}>{xzQ.data?.scopeKind ?? '—'}</Descriptions.Item>
              <Descriptions.Item label={t('adminShell.reporting.reportCenter.detailLabelClosings')}>
                {xzQ.data?.linkedClosingIds?.length ?? 0}
              </Descriptions.Item>
            </Descriptions>

            {xzQ.data?.parts?.length ? (
              <Card size="small" title={t('adminShell.reporting.totalsTitle')} style={{ marginBottom: 16 }}>
                <Space orientation="vertical" size={8} style={{ width: '100%' }}>
                  {xzQ.data.parts.map((p) => (
                    <div key={p.kind}>
                      <Typography.Text strong>{p.label}</Typography.Text>
                      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                        {p.description}
                      </Typography.Paragraph>
                    </div>
                  ))}
                </Space>
              </Card>
            ) : null}

            <Row gutter={16} style={{ marginBottom: 16 }}>
              <Col xs={24} md={xzQ.data?.interimXLike ? 12 : 24}>
                <Card size="small" title={t('adminShell.reporting.tabSummary')}>
                  <Typography.Paragraph>
                    {t('adminShell.reporting.gross')}:{' '}
                    <strong>{Number(xzQ.data?.fullDayOperationalSummary?.grossTotalAmount ?? 0).toFixed(2)}</strong> ·{' '}
                    {t('adminShell.reporting.tax')}:{' '}
                    <strong>{Number(xzQ.data?.fullDayOperationalSummary?.taxTotalAmount ?? 0).toFixed(2)}</strong> ·{' '}
                    {t('adminShell.reporting.rows')}: <strong>{xzQ.data?.fullDayOperationalSummary?.paymentRowCount ?? 0}</strong>
                  </Typography.Paragraph>
                  <Table
                    size="small"
                    pagination={false}
                    rowKey={(r) => `${r.methodKey}-${r.count}`}
                    dataSource={xzQ.data?.fullDayOperationalSummary?.byPaymentMethod ?? []}
                    columns={[
                      { title: t('adminShell.reporting.methodKey'), dataIndex: 'methodKey' },
                      { title: t('adminShell.reporting.count'), dataIndex: 'count' },
                      { title: t('adminShell.reporting.amount'), dataIndex: 'totalAmount' },
                    ]}
                  />
                </Card>
              </Col>
              {xzQ.data?.interimXLike ? (
                <Col xs={24} md={12}>
                  <Card size="small" title={t('adminShell.reporting.tabInterim')}>
                    <Typography.Paragraph>
                      {t('adminShell.reporting.gross')}:{' '}
                      <strong>{Number(xzQ.data.interimXLike.summary?.grossTotalAmount ?? 0).toFixed(2)}</strong> ·{' '}
                      {t('adminShell.reporting.rows')}: <strong>{xzQ.data.interimXLike.summary?.paymentRowCount ?? 0}</strong>
                    </Typography.Paragraph>
                    {xzQ.data.interimVsFullDaySnapshot ? (
                      <Typography.Paragraph type="secondary">
                        Δ {t('adminShell.reporting.gross')}: {Number(xzQ.data.interimVsFullDaySnapshot.deltaGross).toFixed(2)}
                      </Typography.Paragraph>
                    ) : null}
                  </Card>
                </Col>
              ) : null}
            </Row>

            {xzQ.data?.operationalVsClosing ? (
              <Alert
                type="info"
                showIcon
                style={{ marginBottom: 12 }}
                title={
                  <div>
                    <div>{xzQ.data.operationalVsClosing.note}</div>
                    <div>
                      Δ: {Number(xzQ.data.operationalVsClosing.deltaGross).toFixed(2)}
                    </div>
                  </div>
                }
              />
            ) : null}

            <Card size="small" title={t('adminShell.reporting.closingsTableTitle')} style={{ marginBottom: 12 }}>
              <Table
                size="small"
                rowKey="id"
                loading={xzQ.isLoading}
                dataSource={xzQ.data?.closingReference?.dailyClosings ?? []}
                columns={xzClosingCols}
                pagination={{ pageSize: 10 }}
              />
              {xzQ.data?.closingReference?.operatorNote ? (
                <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
                  {xzQ.data.closingReference.operatorNote}
                </Typography.Paragraph>
              ) : null}
            </Card>

            <Space wrap>
              <Button type="default" href="/reporting?tab=closings">
                {t('adminShell.reporting.tabClosings')}
              </Button>
              <Button type="default" href="/reporting?tab=interim">
                {t('adminShell.reporting.tabInterim')}
              </Button>
              <Button href="/tagesabschluss">Tagesabschluss</Button>
            </Space>
          </Spin>
        </Card>
      ) : null}

      {tab === 'submissionQueue' ? (
        <Card title={t('adminShell.reporting.reportCenter.tabQueue')}>
          <Typography.Paragraph type="secondary">{t('adminShell.reporting.reportCenter.queueSectionIntro')}</Typography.Paragraph>
          {!canFinanzOnlineView ? (
            <Alert type="warning" showIcon title={t('adminShell.reporting.reportCenter.queueNoPermission')} style={{ marginBottom: 12 }} />
          ) : null}
          {canFinanzOnlineView ? (
            <>
              <Space style={{ marginBottom: 12 }} wrap>
                <Select
                  allowClear
                  placeholder="Aggregate-Typ"
                  style={{ minWidth: 200 }}
                  value={outboxAggregateFilter}
                  onChange={(v) => setOutboxAggregateFilter(v)}
                  options={[
                    { value: 'TagesberichtReport', label: 'Tagesbericht' },
                    { value: 'MonatsberichtReport', label: 'Monatsbericht' },
                    { value: 'JahresberichtReport', label: 'Jahresbericht' },
                  ]}
                />
                <Button type="primary" href="/rksv/finanz-online-outbox">
                  {t('adminShell.reporting.reportCenter.queueOpenAdmin')}
                </Button>
              </Space>
              <Table
                rowKey={(r) => r.outboxId ?? r.aggregateId ?? r.correlationId ?? 'row'}
                loading={outboxQ.isLoading}
                dataSource={outboxItems}
                columns={outboxCols}
                pagination={{ pageSize: 12 }}
              />
            </>
          ) : null}
        </Card>
      ) : null}

      <ReportChainTimelineDrawer open={chainOpen} onClose={() => setChainOpen(false)} reportType={chainType} reportId={chainReportId} />

      <Modal
        title={t('adminShell.reporting.reportCenter.periodenModalTitle')}
        open={periodenModalOpen}
        onCancel={() => setPeriodenModalOpen(false)}
        footer={null}
        width={640}
        destroyOnHidden
      >
        <Spin spinning={periodenDetailQ.isLoading}>
          {periodenDetailQ.data ? (
            <>
              <Descriptions bordered size="small" column={1} style={{ marginBottom: 12 }}>
                <Descriptions.Item label={t('adminShell.reporting.reportCenter.detailLabelSchema')}>
                  {periodenDetailQ.data.snapshotSchemaVersion}
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.reporting.gross')}>
                  {Number(periodenDetailQ.data.summary?.grossTotalAmount ?? 0).toFixed(2)}
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.reporting.tax')}>
                  {Number(periodenDetailQ.data.summary?.taxTotalAmount ?? 0).toFixed(2)}
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.reporting.rows')}>{periodenDetailQ.data.summary?.paymentRowCount ?? 0}</Descriptions.Item>
              </Descriptions>
              {periodenDetailQ.data.warnings?.length ? (
                <Alert
                  type="info"
                  showIcon
                  title={periodenDetailQ.data.warnings.map((w) => (
                    <div key={w}>{w}</div>
                  ))}
                />
              ) : null}
              <LegalExportCompletenessBanner
                reportKind="periodenbericht"
                reportId={periodenDetailId ?? undefined}
                enabled={!!periodenDetailId}
              />
            </>
          ) : null}
        </Spin>
      </Modal>
    </div>
  );
}
