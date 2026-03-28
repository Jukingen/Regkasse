'use client';

/**
 * Jahresbericht-Detail: linked months, annual aggregation, correction timeline, submission state.
 */
import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Card, Descriptions, Radio, Space, Table, Tag, Timeline, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, formatNumber, useI18n } from '@/i18n';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { FormalReportLanguageNotice, FormalReportProfileLanguageCue } from '@/components/reporting/FormalReportLanguageNotice';
import { BackendRawTextBlock } from '@/components/admin-layout/BackendRawTextBlock';
import { LegalExportCompletenessBanner } from '@/components/reporting/LegalExportCompletenessBanner';
import { useFiscalReportText } from '@/shared/reporting/useFiscalReportText';

type JahresberichtDto = {
  id: string;
  viennaYearStart: string;
  scopeKind: string;
  cashRegisterId?: string | null;
  registerNumber?: string | null;
  reportStatus: string;
  reportVersion?: number;
  reportRevisionReason?: string;
  rebuildCause?: string;
  correctionType?: string;
  submissionImpact?: string;
  supersededByReportId?: string | null;
  snapshotHash: string;
  summary: {
    viennaYear: number;
    linkedFinalizedMonatsberichte: {
      monatsberichtId: string;
      viennaMonthStart: string;
      cashRegisterId?: string | null;
      registerNumber?: string | null;
      snapshotHash: string;
      grossSalesAmount: number;
      reportStatus: string;
    }[];
    aggregationFromMonthly: {
      linkedMonthlyReportCount: number;
      expectedMonthsInYear: number;
      distinctMonthsCovered: number;
      grossSalesAmount: number;
      taxTotalAmount: number;
      refundAmountTotal: number;
      salePaymentRowCount: number;
      refundRowCount: number;
      stornoRowCount: number;
    };
    rawPaymentRollup: {
      grossSalesAmount: number;
      taxTotalAmount: number;
      refundAmountTotal: number;
      salePaymentRowCount: number;
    };
    adjustment: {
      grossDeltaMonthlyVsRaw: number;
      requiresReview: boolean;
      noteDe?: string | null;
      noteEn?: string | null;
    };
    paymentMethodBreakdown: { methodKey: string; displayLabel?: string; rowCount: number; totalAmount: number }[];
    taxBreakdown: { taxBucketKey: string; taxAmount: number }[];
    warnings: string[];
  };
  submission: {
    lifecycle: string;
    operatorHintDe?: string;
    operatorHintEn?: string | null;
    outboxStatus?: string;
    externalReferenceId?: string;
  };
  submissionEnvelope?: {
    submissionVersusReportNoteDe?: string;
    submissionVersusReportNoteEn?: string | null;
    attempts?: { attemptCount: number; status?: string; nextAttemptAtUtc?: string; failureCategory?: string }[];
    rejectionReasons?: string[];
    remediationHintsDe?: string[];
  };
  exportProfiles: {
    profileKey: string;
    labelDe: string;
    descriptionDe: string;
    labelEn?: string | null;
    descriptionEn?: string | null;
    includeTraceIds: boolean;
    nonLegalOutput?: boolean;
    isDiagnosticOnly?: boolean;
  }[];
  correction: { isCorrection: boolean; supersedesReportId?: string | null; supersededByReportId?: string | null };
  upstreamPropagation?: { requiresReview: boolean; reasonCode?: string; noteDe?: string; noteEn?: string | null };
};

type ReportHistoryTimelineDto = {
  reportType: string;
  requestedReportId: string;
  chainRootReportId: string;
  currentActiveReportId?: string | null;
  items: {
    reportId: string;
    reportVersion: number;
    reportStatus: string;
    createdAtUtc: string;
    finalizedAtUtc?: string | null;
    isCurrentActiveVersion: boolean;
    labelKeys: string[];
    submission: {
      lifecycle: string;
      outboxMessageId?: string | null;
      lastErrorMessage?: string | null;
      hasMissingOutboxReference: boolean;
    };
  }[];
};

/** Display YYYY-MM from an ISO month start (YYYY-MM-…); report identity, not UI locale. */
function formatYearMonthIso(isoMonthStart: string): string {
  const s = isoMonthStart?.trim() ?? '';
  if (s.length >= 7) return s.slice(0, 7);
  return s || FORMAT_EMPTY_DISPLAY;
}

export default function JahresberichtDetailPage() {
  const { t, formatLocale } = useI18n();
  const { fiscalTooltip, resolveFiscal, joinRemediationHints, resolveExportProfileRow } = useFiscalReportText();
  const tj = useCallback((path: string) => t(`reporting.jahresbericht.detail.${path}`), [t]);
  const ts = useCallback((path: string) => t(`reporting.tagesbericht.detail.${path}`), [t]);
  const tm = useCallback((path: string) => t(`reporting.monatsbericht.detail.${path}`), [t]);
  const router = useRouter();
  const params = useParams();
  const id = params?.id as string;
  const qc = useQueryClient();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);
  const canSubmitFo = hasPermission(PERMISSIONS.FINANZONLINE_SUBMIT);

  const backendApiTooltip = t('reporting.backend.apiStringsTooltip');

  const [profile, setProfile] = useState<'operationalPreview' | 'accountingReport' | 'legalComplianceExport' | 'diagnosticPackage'>('operationalPreview');

  const detailQ = useQuery({
    queryKey: ['jahresbericht', id],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<JahresberichtDto>(`/api/reports/jahresbericht/${id}`);
      return data;
    },
    enabled: !!id,
  });

  const historyQ = useQuery({
    queryKey: ['report-history', 'jahresbericht', id],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<ReportHistoryTimelineDto>(`/api/reports/history/jahresbericht/${id}`);
      return data;
    },
    enabled: !!id,
  });

  const finalizeMut = useMutation({
    mutationFn: async () => {
      await AXIOS_INSTANCE.post('/api/reports/jahresbericht/finalize', { reportId: id, note: null });
    },
    onSuccess: () => {
      message.success(ts('messages.finalizeSuccess'));
      qc.invalidateQueries({ queryKey: ['jahresbericht', id] });
    },
    onError: () => message.error(ts('messages.finalizeError')),
  });

  const submitMut = useMutation({
    mutationFn: async () => {
      await AXIOS_INSTANCE.post(`/api/reports/jahresbericht/${id}/submit-finanzonline`);
    },
    onSuccess: () => {
      message.success(tj('messages.submitFinanzOnlineIdempotent'));
      qc.invalidateQueries({ queryKey: ['jahresbericht', id] });
    },
    onError: () => message.error(ts('messages.submitError')),
  });

  const correctionMut = useMutation({
    mutationFn: async () => {
      const { data } = await AXIOS_INSTANCE.post<JahresberichtDto>('/api/reports/jahresbericht/correction', {
        supersedesReportId: id,
        // API sözleşmesi: backend şu an bu sabit metni bekliyor olabilir
        reason: 'Korrektur / Neuberechnung',
      });
      return data;
    },
    onSuccess: (data) => {
      message.success(ts('messages.correctionSuccess'));
      qc.invalidateQueries({ queryKey: ['jahresbericht'] });
      if (data?.id) router.push(`/reporting/jahresbericht/${data.id}`);
    },
    onError: () => message.error(ts('messages.correctionError')),
  });

  const linkedCols: ColumnsType<JahresberichtDto['summary']['linkedFinalizedMonatsberichte'][0]> = useMemo(
    () => [
      {
        title: tj('labels.tableMonth'),
        dataIndex: 'viennaMonthStart',
        render: (v: string) => formatYearMonthIso(v),
      },
      {
        title: tm('labels.register'),
        dataIndex: 'registerNumber',
        render: (v, r) => v ?? (r.cashRegisterId ? r.cashRegisterId.slice(0, 8) : FORMAT_EMPTY_DISPLAY),
      },
      {
        title: ts('labels.gross'),
        dataIndex: 'grossSalesAmount',
        render: (v: number) => formatCurrency(v ?? 0, formatLocale),
      },
      {
        title: tj('labels.monthlyReportColumn'),
        key: 'link',
        render: (_, r) => (
          <Link href={`/reporting/monatsbericht/${r.monatsberichtId}`}>{tj('labels.openMonthlyReport')}</Link>
        ),
      },
    ],
    [tj, tm, ts, formatLocale],
  );

  const pmCols: ColumnsType<JahresberichtDto['summary']['paymentMethodBreakdown'][0]> = useMemo(
    () => [
      { title: ts('labels.method'), dataIndex: 'methodKey' },
      { title: ts('labels.lines'), dataIndex: 'rowCount' },
      {
        title: ts('labels.sum'),
        dataIndex: 'totalAmount',
        render: (v: number) => formatCurrency(v ?? 0, formatLocale),
      },
    ],
    [ts, formatLocale],
  );

  const taxColumns: ColumnsType<JahresberichtDto['summary']['taxBreakdown'][0]> = useMemo(
    () => [
      { title: ts('labels.taxBucket'), dataIndex: 'taxBucketKey' },
      {
        title: ts('labels.taxAmount'),
        dataIndex: 'taxAmount',
        render: (v: number) =>
          formatNumber(v ?? 0, formatLocale, { minimumFractionDigits: 2, maximumFractionDigits: 4 }),
      },
    ],
    [ts, formatLocale],
  );

  if (detailQ.isLoading) {
    return <Typography.Paragraph>{tj('loading')}</Typography.Paragraph>;
  }
  if (detailQ.isError || !detailQ.data) {
    return <Typography.Paragraph type="danger">{tj('loadError')}</Typography.Paragraph>;
  }

  const d = detailQ.data;
  const showHashes = profile !== 'operationalPreview';
  const showTrace = profile === 'legalComplianceExport' || profile === 'diagnosticPackage';

  const agg = d.summary.aggregationFromMonthly;
  const raw = d.summary.rawPaymentRollup;
  const adj = d.summary.adjustment;

  const reportVsSubmissionNote = resolveFiscal(
    d.submissionEnvelope?.submissionVersusReportNoteDe,
    d.submissionEnvelope?.submissionVersusReportNoteEn,
  );
  const operatorHintResolved = resolveFiscal(d.submission.operatorHintDe, d.submission.operatorHintEn);
  const remediationResolved = joinRemediationHints(d.submissionEnvelope?.remediationHintsDe, ' | ');
  const upstreamNote = d.upstreamPropagation
    ? resolveFiscal(d.upstreamPropagation.noteDe, d.upstreamPropagation.noteEn)
    : undefined;
  const adjustmentNote = resolveFiscal(adj.noteDe, adj.noteEn);

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader
        title={t('reporting.jahresbericht.detail.pageTitle', { year: String(d.summary.viennaYear) })}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: tj('breadcrumbList'), href: '/reporting/jahresbericht' },
          { title: id },
        ]}
        actions={
          <Space wrap>
            {canExport && d.reportStatus === 'Provisional' ? (
              <Button type="primary" loading={finalizeMut.isPending} onClick={() => finalizeMut.mutate()}>
                {ts('actions.finalize')}
              </Button>
            ) : null}
            {canSubmitFo && d.reportStatus === 'Finalized' ? (
              <Button loading={submitMut.isPending} onClick={() => submitMut.mutate()}>
                {ts('actions.finanzOnline')}
              </Button>
            ) : null}
            {canExport && d.reportStatus === 'Finalized' && !d.supersededByReportId ? (
              <Button onClick={() => correctionMut.mutate()} loading={correctionMut.isPending}>
                {ts('actions.correction')}
              </Button>
            ) : null}
            <Button onClick={() => router.push('/reporting/jahresbericht')}>{ts('actions.backToList')}</Button>
          </Space>
        }
      />

      <FormalReportLanguageNotice />

      {d.upstreamPropagation?.requiresReview && upstreamNote ? (
        <Alert
          type="warning"
          showIcon
          message={tm('upstreamAlertTitle')}
          description={
            <Typography.Text title={fiscalTooltip(upstreamNote.contentLang)}>{upstreamNote.text}</Typography.Text>
          }
          style={{ marginBottom: 16 }}
        />
      ) : null}

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space direction="vertical">
          <Typography.Text type="secondary">{ts('profile.label')}</Typography.Text>
          <Radio.Group value={profile} onChange={(e) => setProfile(e.target.value)}>
            <Radio.Button value="operationalPreview">{ts('profile.operational')}</Radio.Button>
            <Radio.Button value="accountingReport">{ts('profile.accounting')}</Radio.Button>
            <Radio.Button value="legalComplianceExport">{ts('profile.legal')}</Radio.Button>
            <Radio.Button value="diagnosticPackage">{ts('profile.diagnostic')}</Radio.Button>
          </Radio.Group>
          <FormalReportProfileLanguageCue />
          {profile !== 'legalComplianceExport' ? (
            <Typography.Text type="warning">{ts('profile.warnNonLegal')}</Typography.Text>
          ) : null}
          {profile === 'diagnosticPackage' ? (
            <Typography.Text type="danger">{ts('profile.warnDiagnostic')}</Typography.Text>
          ) : null}
          <Typography.Text type="secondary">{ts('profile.hintProfile')}</Typography.Text>
          {d.exportProfiles?.length ? (
            <Typography.Paragraph
              type="secondary"
              style={{ marginBottom: 0, fontSize: 12 }}
              title={t('reporting.backend.fiscalReportExportProfilesHint')}
            >
              {d.exportProfiles.map((p) => {
                const row = resolveExportProfileRow(p);
                if (!row) return null;
                return (
                  <span key={p.profileKey} style={{ marginRight: 12 }}>
                    <strong title={fiscalTooltip(row.label.contentLang)}>{row.label.text}</strong>
                    {': '}
                    <span title={fiscalTooltip(row.description.contentLang)}>{row.description.text}</span>
                  </span>
                );
              })}
            </Typography.Paragraph>
          ) : null}
          <LegalExportCompletenessBanner
            reportKind="jahresbericht"
            reportId={id}
            enabled={profile === 'legalComplianceExport'}
          />
        </Space>
      </Card>

      <Card title={ts('cards.status')} style={{ marginBottom: 16 }}>
        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label={tm('labels.scope')}>
            <Tag title={backendApiTooltip}>{d.scopeKind}</Tag>{' '}
            {d.scopeKind === 'Company' ? (
              tm('labels.scopeAllRegisters')
            ) : (
              <Typography.Text title={backendApiTooltip}>{d.registerNumber ?? d.cashRegisterId}</Typography.Text>
            )}
          </Descriptions.Item>
          <Descriptions.Item label={ts('labels.report')}>
            <Tag color={d.reportStatus === 'Finalized' ? 'blue' : 'gold'} title={backendApiTooltip}>
              {d.reportStatus}
            </Tag>
            {d.correction.isCorrection ? (
              <Tag color="orange" title={backendApiTooltip}>
                {tm('labels.correctionTag')}
              </Tag>
            ) : null}
          </Descriptions.Item>
          {d.reportVersion ? (
            <Descriptions.Item label={ts('labels.version')}>v{d.reportVersion}</Descriptions.Item>
          ) : null}
          {d.correctionType ? (
            <Descriptions.Item label={ts('labels.correctionType')}>
              <Typography.Text title={backendApiTooltip}>{d.correctionType}</Typography.Text>
            </Descriptions.Item>
          ) : null}
          {d.submissionImpact ? (
            <Descriptions.Item label={ts('labels.submissionImpact')}>
              <Typography.Text title={backendApiTooltip}>{d.submissionImpact}</Typography.Text>
            </Descriptions.Item>
          ) : null}
          {d.reportRevisionReason ? (
            <Descriptions.Item label={ts('labels.revisionReason')}>
              <Typography.Text title={backendApiTooltip}>{d.reportRevisionReason}</Typography.Text>
            </Descriptions.Item>
          ) : null}
          {d.rebuildCause ? (
            <Descriptions.Item label={ts('labels.rebuildCause')}>
              <Typography.Text title={backendApiTooltip}>{d.rebuildCause}</Typography.Text>
            </Descriptions.Item>
          ) : null}
          {d.correction.supersedesReportId ? (
            <Descriptions.Item label={tm('labels.predecessor')}>
              <Link href={`/reporting/jahresbericht/${d.correction.supersedesReportId}`}>
                {d.correction.supersedesReportId}
              </Link>
            </Descriptions.Item>
          ) : null}
          {d.supersededByReportId ? (
            <Descriptions.Item label={tm('labels.supersededBy')}>
              <Link href={`/reporting/jahresbericht/${d.supersededByReportId}`}>{d.supersededByReportId}</Link>
            </Descriptions.Item>
          ) : null}
          {reportVsSubmissionNote ? (
            <Descriptions.Item label={ts('labels.reportVsSubmission')}>
              <Typography.Text type="secondary" title={fiscalTooltip(reportVsSubmissionNote.contentLang)}>
                {reportVsSubmissionNote.text}
              </Typography.Text>
            </Descriptions.Item>
          ) : null}
          <Descriptions.Item label={ts('labels.submission')}>
            <Tag title={backendApiTooltip}>{d.submission.lifecycle}</Tag>{' '}
            {operatorHintResolved ? (
              <Typography.Text type="secondary" title={fiscalTooltip(operatorHintResolved.contentLang)}>
                {operatorHintResolved.text}
              </Typography.Text>
            ) : null}
          </Descriptions.Item>
          {d.submissionEnvelope?.attempts?.length ? (
            <Descriptions.Item label={ts('labels.attempt')}>
              #{d.submissionEnvelope.attempts[0].attemptCount} (
              {d.submissionEnvelope.attempts[0].status ?? ts('labels.notAvailable')})
            </Descriptions.Item>
          ) : null}
          {d.submissionEnvelope?.rejectionReasons?.length ? (
            <Descriptions.Item label={ts('labels.rejection')}>
              <Typography.Text title={backendApiTooltip}>
                {d.submissionEnvelope.rejectionReasons.join(', ')}
              </Typography.Text>
            </Descriptions.Item>
          ) : null}
          {remediationResolved ? (
            <Descriptions.Item label={ts('labels.remediation')}>
              <Typography.Text title={fiscalTooltip(remediationResolved.contentLang)}>
                {remediationResolved.text}
              </Typography.Text>
            </Descriptions.Item>
          ) : null}
          {d.submission.externalReferenceId ? (
            <Descriptions.Item label={ts('labels.reference')}>{d.submission.externalReferenceId}</Descriptions.Item>
          ) : null}
          {showHashes ? (
            <Descriptions.Item label={ts('labels.snapshotHash')}>{d.snapshotHash}</Descriptions.Item>
          ) : null}
        </Descriptions>
      </Card>

      <Card title={tj('cards.linkedFinalizedMonthly')} style={{ marginBottom: 16 }}>
        <Table
          rowKey="monatsberichtId"
          size="small"
          pagination={false}
          dataSource={d.summary.linkedFinalizedMonatsberichte}
          columns={linkedCols}
        />
      </Card>

      <Card title={tj('cards.annualSums')} style={{ marginBottom: 16 }}>
        <Descriptions column={2} size="small" bordered>
          <Descriptions.Item label={tj('labels.monthlyReportsCount')}>{agg.linkedMonthlyReportCount}</Descriptions.Item>
          <Descriptions.Item label={tj('labels.monthsCovered')}>
            {agg.distinctMonthsCovered} / {agg.expectedMonthsInYear}
          </Descriptions.Item>
          <Descriptions.Item label={tj('labels.grossFromMonthlyReports')}>
            {formatCurrency(agg.grossSalesAmount, formatLocale)}
          </Descriptions.Item>
          <Descriptions.Item label={ts('labels.taxTotal')}>
            {formatCurrency(agg.taxTotalAmount, formatLocale)}
          </Descriptions.Item>
          <Descriptions.Item label={ts('labels.refunds')}>
            {formatCurrency(agg.refundAmountTotal, formatLocale)}
          </Descriptions.Item>
          <Descriptions.Item label={ts('labels.saleLines')}>{agg.salePaymentRowCount}</Descriptions.Item>
        </Descriptions>
      </Card>

      {(profile === 'accountingReport' || profile === 'legalComplianceExport' || profile === 'diagnosticPackage') ? (
        <Card title={tj('cards.rawDataPaymentDetails')} style={{ marginBottom: 16 }}>
          <Descriptions column={2} size="small" bordered>
            <Descriptions.Item label={ts('labels.gross')}>
              {formatCurrency(raw.grossSalesAmount, formatLocale)}
            </Descriptions.Item>
            <Descriptions.Item label={ts('labels.taxAmount')}>
              {formatCurrency(raw.taxTotalAmount, formatLocale)}
            </Descriptions.Item>
            <Descriptions.Item label={ts('labels.refunds')}>
              {formatCurrency(raw.refundAmountTotal, formatLocale)}
            </Descriptions.Item>
            <Descriptions.Item label={ts('labels.saleLines')}>{raw.salePaymentRowCount}</Descriptions.Item>
          </Descriptions>
          <Descriptions column={1} size="small" bordered style={{ marginTop: 8 }}>
            <Descriptions.Item label={tj('labels.deltaGrossMonthlyVsRaw')}>
              {formatCurrency(adj.grossDeltaMonthlyVsRaw, formatLocale)}
            </Descriptions.Item>
            <Descriptions.Item label={tm('labels.reviewCheck')}>
              {adj.requiresReview ? (
                <Tag color="warning" title={backendApiTooltip}>
                  {tm('labels.reviewDeviation')}
                </Tag>
              ) : (
                <Tag title={backendApiTooltip}>{tm('labels.reviewOk')}</Tag>
              )}
              {adjustmentNote ? (
                <>
                  {' — '}
                  <Typography.Text title={fiscalTooltip(adjustmentNote.contentLang)}>{adjustmentNote.text}</Typography.Text>
                </>
              ) : null}
            </Descriptions.Item>
          </Descriptions>
        </Card>
      ) : null}

      {(profile === 'accountingReport' || profile === 'legalComplianceExport' || profile === 'diagnosticPackage') ? (
        <Card title={tj('cards.taxBreakdownFromMonthly')} style={{ marginBottom: 16 }}>
          <Table
            rowKey="taxBucketKey"
            size="small"
            pagination={false}
            dataSource={d.summary.taxBreakdown}
            columns={taxColumns}
          />
        </Card>
      ) : null}

      <Card title={tj('cards.paymentMethodsFromMonthly')} style={{ marginBottom: 16 }}>
        <Table rowKey="methodKey" size="small" pagination={false} dataSource={d.summary.paymentMethodBreakdown} columns={pmCols} />
      </Card>

      <Card title={tm('cards.hints')} style={{ marginBottom: 16 }}>
        {d.summary.warnings?.length ? (
          <ul>
            {d.summary.warnings.map((w) => (
              <li key={w}>
                <Typography.Text type="warning" title={backendApiTooltip}>
                  {w}
                </Typography.Text>
              </li>
            ))}
          </ul>
        ) : (
          <Typography.Text type="secondary">{tm('labels.noWarnings')}</Typography.Text>
        )}
      </Card>

      {showTrace ? (
        <Card title={ts('cards.trace')}>
          <Typography.Paragraph type="secondary">{tj('traceBody')}</Typography.Paragraph>
        </Card>
      ) : null}

      <Card title={ts('cards.history')} style={{ marginTop: 16 }}>
        {historyQ.isLoading ? (
          <Typography.Text type="secondary">{ts('history.loading')}</Typography.Text>
        ) : historyQ.data?.items?.length ? (
          <Timeline
            items={historyQ.data.items.map((item) => ({
              color: item.isCurrentActiveVersion ? 'green' : item.reportStatus === 'Superseded' ? 'orange' : 'blue',
              children: (
                <Space direction="vertical" size={2}>
                  <Typography.Text strong title={backendApiTooltip}>
                    v{item.reportVersion} · {item.reportId.slice(0, 8)} · {item.reportStatus}
                  </Typography.Text>
                  <Space size={[4, 4]} wrap>
                    {item.labelKeys.map((k) => (
                      <Tag key={`${item.reportId}-${k}`} title={backendApiTooltip}>
                        {k}
                      </Tag>
                    ))}
                  </Space>
                  <Typography.Text type="secondary">
                    {ts('history.created')} {formatDateTime(item.createdAtUtc, formatLocale)}{' '}
                    {item.finalizedAtUtc
                      ? `· ${ts('history.finalized')} ${formatDateTime(item.finalizedAtUtc, formatLocale)}`
                      : ''}
                  </Typography.Text>
                  <Typography.Text type="secondary" title={backendApiTooltip}>
                    {ts('history.submissionLine')} {item.submission.lifecycle}
                    {item.submission.outboxMessageId
                      ? ` · ${ts('history.outbox')} ${item.submission.outboxMessageId.slice(0, 8)}`
                      : ''}
                    {item.submission.hasMissingOutboxReference ? ` · ${ts('history.missingOutboxRef')}` : ''}
                  </Typography.Text>
                  {item.submission.lastErrorMessage ? (
                    <BackendRawTextBlock
                      introKey="reporting.tagesbericht.detail.history.submissionLastErrorIntro"
                      body={item.submission.lastErrorMessage}
                      textType="warning"
                    />
                  ) : null}
                </Space>
              ),
            }))}
          />
        ) : (
          <Typography.Text type="secondary">{ts('history.empty')}</Typography.Text>
        )}
      </Card>
    </div>
  );
}
