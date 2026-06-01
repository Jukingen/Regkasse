'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Tagesbericht-Detail: Snapshot, Profile (Betrieb/Buchhaltung/Compliance), Finalisieren, FinanzOnline, Korrektur.
 */
import React, { useCallback, useMemo, useState } from 'react';
import { Button, Card, Descriptions, Radio, Space, Table, Tag, Timeline, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { formatCurrency, formatDateTime, formatNumber, useI18n } from '@/i18n';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { FormalReportLanguageNotice, FormalReportProfileLanguageCue } from '@/components/reporting/FormalReportLanguageNotice';
import { BackendRawTextBlock } from '@/components/admin-layout/BackendRawTextBlock';
import { LegalExportCompletenessBanner } from '@/components/reporting/LegalExportCompletenessBanner';
import { useFiscalReportText } from '@/shared/reporting/useFiscalReportText';

type TagesberichtDto = {
  id: string;
  viennaBusinessDate: string;
  reportStatus: string;
  reportVersion?: number;
  reportRevisionReason?: string;
  rebuildCause?: string;
  correctionType?: string;
  submissionImpact?: string;
  supersededByReportId?: string | null;
  snapshotHash: string;
  summary: {
    viennaBusinessDate: string;
    grossSalesAmount: number;
    taxTotalAmount: number;
    refundAmountTotal: number;
    salePaymentRowCount: number;
    refundRowCount: number;
    stornoRowCount: number;
    paymentMethodBreakdown: { methodKey: string; displayLabel?: string; rowCount: number; totalAmount: number }[];
    taxBreakdown: { taxBucketKey: string; taxAmount: number }[];
    reconciliation: {
      paymentsWithoutInvoiceCount: number;
      unknownPaymentMethodRowCount: number;
      offlineLinkedPaymentCount: number;
      dayClosedInRksv: boolean;
    };
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
  correction: { isCorrection: boolean; supersedesReportId?: string };
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
    originalReportId?: string | null;
    correctionOfReportId?: string | null;
    supersedesReportId?: string | null;
    supersededByReportId?: string | null;
    createdAtUtc: string;
    finalizedAtUtc?: string | null;
    isCurrentActiveVersion: boolean;
    isOriginalVersion: boolean;
    isCorrectionVersion: boolean;
    labelKeys: string[];
    submission: {
      lifecycle: string;
      outboxMessageId?: string | null;
      outboxStatus?: string | null;
      latestStatusCode?: string | null;
      externalReferenceId?: string | null;
      lastErrorMessage?: string | null;
      isSubmitted: boolean;
      isAccepted: boolean;
      isRejected: boolean;
      isRetrying: boolean;
      hasMissingOutboxReference: boolean;
    };
  }[];
};

export default function TagesberichtDetailPage() {
  const { message } = useAntdApp();

  const { t, formatLocale } = useI18n();
  const { fiscalTooltip, resolveFiscal, joinRemediationHints } = useFiscalReportText();
  const td = useCallback((path: string) => t(`reporting.tagesbericht.detail.${path}`), [t]);
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
    queryKey: ['tagesbericht', id],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<TagesberichtDto>(`/api/reports/tagesbericht/${id}`);
      return data;
    },
    enabled: !!id,
  });

  const historyQ = useQuery({
    queryKey: ['report-history', 'tagesbericht', id],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<ReportHistoryTimelineDto>(`/api/reports/history/tagesbericht/${id}`);
      return data;
    },
    enabled: !!id,
  });

  const finalizeMut = useMutation({
    mutationFn: async () => {
      await AXIOS_INSTANCE.post('/api/reports/tagesbericht/finalize', { reportId: id, note: null });
    },
    onSuccess: () => {
      message.success(td('messages.finalizeSuccess'));
      qc.invalidateQueries({ queryKey: ['tagesbericht', id] });
    },
    onError: () => message.error(td('messages.finalizeError')),
  });

  const submitMut = useMutation({
    mutationFn: async () => {
      await AXIOS_INSTANCE.post(`/api/reports/tagesbericht/${id}/submit-finanzonline`);
    },
    onSuccess: () => {
      message.success(td('messages.submitSuccess'));
      qc.invalidateQueries({ queryKey: ['tagesbericht', id] });
    },
    onError: () => message.error(td('messages.submitError')),
  });

  const correctionMut = useMutation({
    mutationFn: async () => {
      const { data } = await AXIOS_INSTANCE.post<TagesberichtDto>('/api/reports/tagesbericht/correction', {
        supersedesReportId: id,
        // API sözleşmesi: backend şu an Almanca sabit bekliyor olabilir — davranış korunur.
        reason: 'Korrektur',
      });
      return data;
    },
    onSuccess: (data) => {
      message.success(td('messages.correctionSuccess'));
      qc.invalidateQueries({ queryKey: ['tagesbericht'] });
      if (data?.id) router.push(`/reporting/tagesbericht/${data.id}`);
    },
    onError: () => message.error(td('messages.correctionError')),
  });

  const pmCols: ColumnsType<TagesberichtDto['summary']['paymentMethodBreakdown'][0]> = useMemo(
    () => [
      { title: td('labels.method'), dataIndex: 'methodKey' },
      { title: td('labels.lines'), dataIndex: 'rowCount' },
      {
        title: td('labels.sum'),
        dataIndex: 'totalAmount',
        render: (v: number) => formatCurrency(v ?? 0, formatLocale),
      },
    ],
    [td, formatLocale],
  );

  const taxColumns: ColumnsType<TagesberichtDto['summary']['taxBreakdown'][0]> = useMemo(
    () => [
      { title: td('labels.taxBucket'), dataIndex: 'taxBucketKey' },
      {
        title: td('labels.taxAmount'),
        dataIndex: 'taxAmount',
        render: (v: number) =>
          formatNumber(v ?? 0, formatLocale, { minimumFractionDigits: 2, maximumFractionDigits: 4 }),
      },
    ],
    [td, formatLocale],
  );

  if (detailQ.isLoading) {
    return <Typography.Paragraph>{td('loading')}</Typography.Paragraph>;
  }
  if (detailQ.isError || !detailQ.data) {
    return <Typography.Paragraph type="danger">{td('loadError')}</Typography.Paragraph>;
  }

  const d = detailQ.data;
  const showTrace = profile === 'legalComplianceExport' || profile === 'diagnosticPackage';
  const showHashes = profile !== 'operationalPreview';
  const businessDate = (d.viennaBusinessDate ?? d.summary?.viennaBusinessDate ?? '').slice(0, 10);

  const reportVsSubmissionNote = resolveFiscal(
    d.submissionEnvelope?.submissionVersusReportNoteDe,
    d.submissionEnvelope?.submissionVersusReportNoteEn,
  );
  const operatorHintResolved = resolveFiscal(d.submission.operatorHintDe, d.submission.operatorHintEn);
  const remediationResolved = joinRemediationHints(d.submissionEnvelope?.remediationHintsDe, ' | ');

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader
        title={t('reporting.tagesbericht.detail.pageTitle', { date: businessDate })}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: td('breadcrumbList'), href: '/reporting/tagesbericht' },
          { title: id },
        ]}
        actions={
          <Space wrap>
            {canExport && d.reportStatus === 'Provisional' ? (
              <Button type="primary" loading={finalizeMut.isPending} onClick={() => finalizeMut.mutate()}>
                {td('actions.finalize')}
              </Button>
            ) : null}
            {canSubmitFo && d.reportStatus === 'Finalized' ? (
              <Button loading={submitMut.isPending} onClick={() => submitMut.mutate()}>
                {td('actions.finanzOnline')}
              </Button>
            ) : null}
            {canExport && d.reportStatus === 'Finalized' && !d.supersededByReportId ? (
              <Button onClick={() => correctionMut.mutate()} loading={correctionMut.isPending}>
                {td('actions.correction')}
              </Button>
            ) : null}
            <Button onClick={() => router.push('/reporting/tagesbericht')}>{td('actions.backToList')}</Button>
          </Space>
        }
      />

      <FormalReportLanguageNotice />

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space orientation="vertical">
          <Typography.Text type="secondary">{td('profile.label')}</Typography.Text>
          <Radio.Group value={profile} onChange={(e) => setProfile(e.target.value)}>
            <Radio.Button value="operationalPreview">{td('profile.operational')}</Radio.Button>
            <Radio.Button value="accountingReport">{td('profile.accounting')}</Radio.Button>
            <Radio.Button value="legalComplianceExport">{td('profile.legal')}</Radio.Button>
            <Radio.Button value="diagnosticPackage">{td('profile.diagnostic')}</Radio.Button>
          </Radio.Group>
          <FormalReportProfileLanguageCue />
          {profile !== 'legalComplianceExport' ? (
            <Typography.Text type="warning">{td('profile.warnNonLegal')}</Typography.Text>
          ) : null}
          {profile === 'diagnosticPackage' ? (
            <Typography.Text type="danger">{td('profile.warnDiagnostic')}</Typography.Text>
          ) : null}
          <Typography.Text type="secondary">{td('profile.hintProfile')}</Typography.Text>
          <LegalExportCompletenessBanner
            reportKind="tagesbericht"
            reportId={id}
            enabled={profile === 'legalComplianceExport'}
          />
        </Space>
      </Card>

      <Card title={td('cards.status')} style={{ marginBottom: 16 }}>
        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label={td('labels.report')}>
            {/* TODO(adapter): reportStatus — sunucu enum’u; locale map backend’de yok */}
            <Tag color={d.reportStatus === 'Finalized' ? 'blue' : 'gold'} title={backendApiTooltip}>
              {d.reportStatus}
            </Tag>
          </Descriptions.Item>
          {d.reportVersion ? (
            <Descriptions.Item label={td('labels.version')}>v{d.reportVersion}</Descriptions.Item>
          ) : null}
          {d.correctionType ? (
            <Descriptions.Item label={td('labels.correctionType')}>
              <Typography.Text title={backendApiTooltip}>{d.correctionType}</Typography.Text>
            </Descriptions.Item>
          ) : null}
          {d.submissionImpact ? (
            <Descriptions.Item label={td('labels.submissionImpact')}>
              <Typography.Text title={backendApiTooltip}>{d.submissionImpact}</Typography.Text>
            </Descriptions.Item>
          ) : null}
          {d.reportRevisionReason ? (
            <Descriptions.Item label={td('labels.revisionReason')}>
              <Typography.Text title={backendApiTooltip}>{d.reportRevisionReason}</Typography.Text>
            </Descriptions.Item>
          ) : null}
          {d.rebuildCause ? (
            <Descriptions.Item label={td('labels.rebuildCause')}>
              <Typography.Text title={backendApiTooltip}>{d.rebuildCause}</Typography.Text>
            </Descriptions.Item>
          ) : null}
          {reportVsSubmissionNote ? (
            <Descriptions.Item label={td('labels.reportVsSubmission')}>
              <Typography.Text type="secondary" title={fiscalTooltip(reportVsSubmissionNote.contentLang)}>
                {reportVsSubmissionNote.text}
              </Typography.Text>
            </Descriptions.Item>
          ) : null}
          <Descriptions.Item label={td('labels.submission')}>
            <Tag title={backendApiTooltip}>{d.submission.lifecycle}</Tag>{' '}
            {operatorHintResolved ? (
              <Typography.Text type="secondary" title={fiscalTooltip(operatorHintResolved.contentLang)}>
                {operatorHintResolved.text}
              </Typography.Text>
            ) : null}
          </Descriptions.Item>
          {d.submissionEnvelope?.attempts?.length ? (
            <Descriptions.Item label={td('labels.attempt')}>
              #{d.submissionEnvelope.attempts[0].attemptCount} (
              {d.submissionEnvelope.attempts[0].status ?? td('labels.notAvailable')})
            </Descriptions.Item>
          ) : null}
          {d.submissionEnvelope?.rejectionReasons?.length ? (
            <Descriptions.Item label={td('labels.rejection')}>
              {/* TODO(adapter): rejectionReasons — ham API dizisi */}
              <Typography.Text title={backendApiTooltip}>
                {d.submissionEnvelope.rejectionReasons.join(', ')}
              </Typography.Text>
            </Descriptions.Item>
          ) : null}
          {remediationResolved ? (
            <Descriptions.Item label={td('labels.remediation')}>
              <Typography.Text title={fiscalTooltip(remediationResolved.contentLang)}>
                {remediationResolved.text}
              </Typography.Text>
            </Descriptions.Item>
          ) : null}
          {d.submission.externalReferenceId ? (
            <Descriptions.Item label={td('labels.reference')}>{d.submission.externalReferenceId}</Descriptions.Item>
          ) : null}
          {showHashes ? (
            <Descriptions.Item label={td('labels.snapshotHash')}>{d.snapshotHash}</Descriptions.Item>
          ) : null}
        </Descriptions>
      </Card>

      <Card title={td('cards.sums')} style={{ marginBottom: 16 }}>
        <Descriptions column={2} size="small" bordered>
          <Descriptions.Item label={td('labels.gross')}>
            {formatCurrency(d.summary.grossSalesAmount, formatLocale)}
          </Descriptions.Item>
          <Descriptions.Item label={td('labels.taxTotal')}>
            {formatCurrency(d.summary.taxTotalAmount, formatLocale)}
          </Descriptions.Item>
          <Descriptions.Item label={td('labels.refunds')}>
            {formatCurrency(d.summary.refundAmountTotal, formatLocale)}
          </Descriptions.Item>
          <Descriptions.Item label={td('labels.saleLines')}>{d.summary.salePaymentRowCount}</Descriptions.Item>
          <Descriptions.Item label={td('labels.refundLines')}>{d.summary.refundRowCount}</Descriptions.Item>
          <Descriptions.Item label={td('labels.stornoLines')}>{d.summary.stornoRowCount}</Descriptions.Item>
        </Descriptions>
      </Card>

      {(profile === 'accountingReport' || profile === 'legalComplianceExport' || profile === 'diagnosticPackage') && (
        <Card title={td('cards.taxBreakdown')} style={{ marginBottom: 16 }}>
          <Table
            rowKey="taxBucketKey"
            size="small"
            pagination={false}
            dataSource={d.summary.taxBreakdown}
            columns={taxColumns}
          />
        </Card>
      )}

      <Card title={td('cards.paymentMethods')} style={{ marginBottom: 16 }}>
        <Table rowKey="methodKey" size="small" pagination={false} dataSource={d.summary.paymentMethodBreakdown} columns={pmCols} />
      </Card>

      <Card title={td('cards.reconciliation')} style={{ marginBottom: 16 }}>
        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label={td('labels.paymentsWithoutInvoice')}>
            {d.summary.reconciliation.paymentsWithoutInvoiceCount}
          </Descriptions.Item>
          <Descriptions.Item label={td('labels.unknownMethodLines')}>
            {d.summary.reconciliation.unknownPaymentMethodRowCount}
          </Descriptions.Item>
          <Descriptions.Item label={td('labels.offlineLinked')}>
            {d.summary.reconciliation.offlineLinkedPaymentCount}
          </Descriptions.Item>
          <Descriptions.Item label={td('labels.dayClosedRksv')}>
            {d.summary.reconciliation.dayClosedInRksv ? td('labels.yes') : td('labels.no')}
          </Descriptions.Item>
        </Descriptions>
        {d.summary.warnings?.length ? (
          <ul>
            {d.summary.warnings.map((w) => (
              <li key={w}>
                {/* TODO(adapter): warnings[] — sunucu metni */}
                <Typography.Text type="warning" title={backendApiTooltip}>
                  {w}
                </Typography.Text>
              </li>
            ))}
          </ul>
        ) : null}
      </Card>

      {showTrace ? (
        <Card title={td('cards.trace')}>
          <Typography.Paragraph type="secondary">{td('cards.traceBody')}</Typography.Paragraph>
        </Card>
      ) : null}

      <Card title={td('cards.history')} style={{ marginTop: 16 }}>
        {historyQ.isLoading ? (
          <Typography.Text type="secondary">{td('history.loading')}</Typography.Text>
        ) : historyQ.data?.items?.length ? (
          <Timeline
            items={historyQ.data.items.map((item) => ({
              color: item.isCurrentActiveVersion ? 'green' : item.reportStatus === 'Superseded' ? 'orange' : 'blue',
              children: (
                <Space orientation="vertical" size={2}>
                  <Typography.Text strong title={backendApiTooltip}>
                    v{item.reportVersion} · {item.reportId.slice(0, 8)} · {item.reportStatus}
                  </Typography.Text>
                  <Space size={[4, 4]} wrap>
                    {item.labelKeys.map((k) => (
                      // TODO(adapter): labelKeys — ham anahtarlar
                      <Tag key={`${item.reportId}-${k}`} title={backendApiTooltip}>
                        {k}
                      </Tag>
                    ))}
                  </Space>
                  <Typography.Text type="secondary">
                    {td('history.created')} {formatDateTime(item.createdAtUtc, formatLocale)}{' '}
                    {item.finalizedAtUtc
                      ? `· ${td('history.finalized')} ${formatDateTime(item.finalizedAtUtc, formatLocale)}`
                      : ''}
                  </Typography.Text>
                  <Typography.Text type="secondary" title={backendApiTooltip}>
                    {td('history.submissionLine')} {item.submission.lifecycle}
                    {item.submission.outboxMessageId
                      ? ` · ${td('history.outbox')} ${item.submission.outboxMessageId.slice(0, 8)}`
                      : ''}
                    {item.submission.hasMissingOutboxReference ? ` · ${td('history.missingOutboxRef')}` : ''}
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
          <Typography.Text type="secondary">{td('history.empty')}</Typography.Text>
        )}
      </Card>
    </div>
  );
}
