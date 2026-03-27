'use client';

/**
 * Tagesbericht-Detail: Snapshot, Profile (Betrieb/Buchhaltung/Compliance), Finalisieren, FinanzOnline, Korrektur.
 */
import React, { useMemo, useState } from 'react';
import {
  Button,
  Card,
  Descriptions,
  Radio,
  Space,
  Table,
  Tag,
  Timeline,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { LegalExportCompletenessBanner } from '@/components/reporting/LegalExportCompletenessBanner';

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
    outboxStatus?: string;
    externalReferenceId?: string;
  };
  submissionEnvelope?: {
    submissionVersusReportNoteDe?: string;
    attempts?: { attemptCount: number; status?: string; nextAttemptAtUtc?: string; failureCategory?: string }[];
    rejectionReasons?: string[];
    remediationHintsDe?: string[];
  };
  exportProfiles: { profileKey: string; labelDe: string; descriptionDe: string; includeTraceIds: boolean; nonLegalOutput?: boolean; isDiagnosticOnly?: boolean }[];
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
  const { t } = useI18n();
  const router = useRouter();
  const params = useParams();
  const id = params?.id as string;
  const qc = useQueryClient();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);
  const canSubmitFo = hasPermission(PERMISSIONS.FINANZONLINE_SUBMIT);

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
      message.success('Finalisiert.');
      qc.invalidateQueries({ queryKey: ['tagesbericht', id] });
    },
    onError: () => message.error('Finalisierung fehlgeschlagen.'),
  });

  const submitMut = useMutation({
    mutationFn: async () => {
      await AXIOS_INSTANCE.post(`/api/reports/tagesbericht/${id}/submit-finanzonline`);
    },
    onSuccess: () => {
      message.success('In FinanzOnline-Outbox eingereiht.');
      qc.invalidateQueries({ queryKey: ['tagesbericht', id] });
    },
    onError: () => message.error('Übermittlung fehlgeschlagen.'),
  });

  const correctionMut = useMutation({
    mutationFn: async () => {
      const { data } = await AXIOS_INSTANCE.post<TagesberichtDto>('/api/reports/tagesbericht/correction', {
        supersedesReportId: id,
        reason: 'Korrektur',
      });
      return data;
    },
    onSuccess: (data) => {
      message.success('Korrekturbericht erzeugt.');
      qc.invalidateQueries({ queryKey: ['tagesbericht'] });
      if (data?.id) router.push(`/reporting/tagesbericht/${data.id}`);
    },
    onError: () => message.error('Korrektur fehlgeschlagen (nur nach finalisiertem Vorgänger).'),
  });

  const d = detailQ.data;
  const showTrace = profile === 'legalComplianceExport' || profile === 'diagnosticPackage';
  const showHashes = profile !== 'operationalPreview';

  const pmCols: ColumnsType<TagesberichtDto['summary']['paymentMethodBreakdown'][0]> = useMemo(
    () => [
      { title: 'Methode', dataIndex: 'methodKey' },
      { title: 'Zeilen', dataIndex: 'rowCount' },
      { title: 'Summe', dataIndex: 'totalAmount', render: (v: number) => v?.toFixed(2) },
    ],
    [],
  );

  if (detailQ.isLoading) {
    return <Typography.Paragraph>Laden…</Typography.Paragraph>;
  }
  if (detailQ.isError || !d) {
    return <Typography.Paragraph type="danger">Bericht nicht gefunden oder Fehler beim Laden.</Typography.Paragraph>;
  }

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader
        title={`Tagesbericht ${(d.viennaBusinessDate ?? d.summary?.viennaBusinessDate ?? '').slice(0, 10)}`}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: 'Tagesbericht', href: '/reporting/tagesbericht' },
          { title: id },
        ]}
        actions={
          <Space wrap>
            {canExport && d.reportStatus === 'Provisional' ? (
              <Button type="primary" loading={finalizeMut.isPending} onClick={() => finalizeMut.mutate()}>
                Finalisieren
              </Button>
            ) : null}
            {canSubmitFo && d.reportStatus === 'Finalized' ? (
              <Button loading={submitMut.isPending} onClick={() => submitMut.mutate()}>
                FinanzOnline (Outbox)
              </Button>
            ) : null}
            {canExport && d.reportStatus === 'Finalized' && !d.supersededByReportId ? (
              <Button onClick={() => correctionMut.mutate()} loading={correctionMut.isPending}>
                Korrektur (neuer Bericht)
              </Button>
            ) : null}
            <Button onClick={() => router.push('/reporting/tagesbericht')}>Zur Liste</Button>
          </Space>
        }
      />

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space direction="vertical">
          <Typography.Text type="secondary">Anzeigeprofil</Typography.Text>
          <Radio.Group value={profile} onChange={(e) => setProfile(e.target.value)}>
            <Radio.Button value="operationalPreview">Operational</Radio.Button>
            <Radio.Button value="accountingReport">Accounting</Radio.Button>
            <Radio.Button value="legalComplianceExport">Legal</Radio.Button>
            <Radio.Button value="diagnosticPackage">Diagnostic</Radio.Button>
          </Radio.Group>
          {profile !== 'legalComplianceExport' ? (
            <Typography.Text type="warning">Nicht-legaler Output: nicht als offizielles Dokument verwenden.</Typography.Text>
          ) : null}
          {profile === 'diagnosticPackage' ? (
            <Typography.Text type="danger">Diagnostic Package ist nur für technische Analyse gedacht.</Typography.Text>
          ) : null}
          <Typography.Text type="secondary">Profil steuert Export/Ansicht, nicht den FinanzOnline-Submission-Status.</Typography.Text>
          <LegalExportCompletenessBanner
            reportKind="tagesbericht"
            reportId={id}
            enabled={profile === 'legalComplianceExport'}
          />
        </Space>
      </Card>

      <Card title="Status" style={{ marginBottom: 16 }}>
        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label="Bericht">
            <Tag color={d.reportStatus === 'Finalized' ? 'blue' : 'gold'}>{d.reportStatus}</Tag>
          </Descriptions.Item>
          {d.reportVersion ? <Descriptions.Item label="Version">v{d.reportVersion}</Descriptions.Item> : null}
          {d.correctionType ? <Descriptions.Item label="Correction Type">{d.correctionType}</Descriptions.Item> : null}
          {d.submissionImpact ? <Descriptions.Item label="Submission Impact">{d.submissionImpact}</Descriptions.Item> : null}
          {d.reportRevisionReason ? <Descriptions.Item label="Revision Reason">{d.reportRevisionReason}</Descriptions.Item> : null}
          {d.rebuildCause ? <Descriptions.Item label="Rebuild Cause">{d.rebuildCause}</Descriptions.Item> : null}
          {d.submissionEnvelope?.submissionVersusReportNoteDe ? (
            <Descriptions.Item label="Bericht vs. Abgabe">
              <Typography.Text type="secondary">{d.submissionEnvelope.submissionVersusReportNoteDe}</Typography.Text>
            </Descriptions.Item>
          ) : null}
          <Descriptions.Item label="Übermittlung">
            <Tag>{d.submission.lifecycle}</Tag> {d.submission.operatorHintDe}
          </Descriptions.Item>
          {d.submissionEnvelope?.attempts?.length ? (
            <Descriptions.Item label="Attempt">
              #{d.submissionEnvelope.attempts[0].attemptCount} ({d.submissionEnvelope.attempts[0].status ?? 'n/a'})
            </Descriptions.Item>
          ) : null}
          {d.submissionEnvelope?.rejectionReasons?.length ? (
            <Descriptions.Item label="Ablehnungsgrund">
              {d.submissionEnvelope.rejectionReasons.join(', ')}
            </Descriptions.Item>
          ) : null}
          {d.submissionEnvelope?.remediationHintsDe?.length ? (
            <Descriptions.Item label="Remediation">
              {d.submissionEnvelope.remediationHintsDe.join(' | ')}
            </Descriptions.Item>
          ) : null}
          {d.submission.externalReferenceId ? (
            <Descriptions.Item label="Referenz">{d.submission.externalReferenceId}</Descriptions.Item>
          ) : null}
          {showHashes ? (
            <Descriptions.Item label="Snapshot-Hash">{d.snapshotHash}</Descriptions.Item>
          ) : null}
        </Descriptions>
      </Card>

      <Card title="Summen" style={{ marginBottom: 16 }}>
        <Descriptions column={2} size="small" bordered>
          <Descriptions.Item label="Brutto">{d.summary.grossSalesAmount.toFixed(2)}</Descriptions.Item>
          <Descriptions.Item label="Steuer (Summe)">{d.summary.taxTotalAmount.toFixed(2)}</Descriptions.Item>
          <Descriptions.Item label="Erstattungen">{d.summary.refundAmountTotal.toFixed(2)}</Descriptions.Item>
          <Descriptions.Item label="Verkaufszeilen">{d.summary.salePaymentRowCount}</Descriptions.Item>
          <Descriptions.Item label="Refund-Zeilen">{d.summary.refundRowCount}</Descriptions.Item>
          <Descriptions.Item label="Storno-Zeilen">{d.summary.stornoRowCount}</Descriptions.Item>
        </Descriptions>
      </Card>

      {(profile === 'accountingReport' || profile === 'legalComplianceExport' || profile === 'diagnosticPackage') && (
        <Card title="Steueraufschlüsselung" style={{ marginBottom: 16 }}>
          <Table
            rowKey="taxBucketKey"
            size="small"
            pagination={false}
            dataSource={d.summary.taxBreakdown}
            columns={[
              { title: 'Bucket', dataIndex: 'taxBucketKey' },
              { title: 'Steuer', dataIndex: 'taxAmount', render: (v: number) => v?.toFixed(4) },
            ]}
          />
        </Card>
      )}

      <Card title="Zahlungsarten" style={{ marginBottom: 16 }}>
        <Table rowKey="methodKey" size="small" pagination={false} dataSource={d.summary.paymentMethodBreakdown} columns={pmCols} />
      </Card>

      <Card title="Abstimmung / Hinweise" style={{ marginBottom: 16 }}>
        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label="Zahlungen ohne Rechnung">
            {d.summary.reconciliation.paymentsWithoutInvoiceCount}
          </Descriptions.Item>
          <Descriptions.Item label="Unbekannte Zahlart (Zeilen)">
            {d.summary.reconciliation.unknownPaymentMethodRowCount}
          </Descriptions.Item>
          <Descriptions.Item label="Offline verknüpft">{d.summary.reconciliation.offlineLinkedPaymentCount}</Descriptions.Item>
          <Descriptions.Item label="Tag in RKSV geschlossen">
            {d.summary.reconciliation.dayClosedInRksv ? 'Ja' : 'Nein'}
          </Descriptions.Item>
        </Descriptions>
        {d.summary.warnings?.length ? (
          <ul>
            {d.summary.warnings.map((w) => (
              <li key={w}>
                <Typography.Text type="warning">{w}</Typography.Text>
              </li>
            ))}
          </ul>
        ) : null}
      </Card>

      {showTrace ? (
        <Card title="Trace (Compliance)">
          <Typography.Paragraph type="secondary">
            Roh-IDs sind gekürzt gespeichert; Hash dient der Nachvollziehbarkeit.
          </Typography.Paragraph>
        </Card>
      ) : null}

      <Card title="History Timeline" style={{ marginTop: 16 }}>
        {historyQ.isLoading ? (
          <Typography.Text type="secondary">Lade Verlauf…</Typography.Text>
        ) : historyQ.data?.items?.length ? (
          <Timeline
            items={historyQ.data.items.map((item) => ({
              color: item.isCurrentActiveVersion ? 'green' : item.reportStatus === 'Superseded' ? 'orange' : 'blue',
              children: (
                <Space direction="vertical" size={2}>
                  <Typography.Text strong>
                    v{item.reportVersion} · {item.reportId.slice(0, 8)} · {item.reportStatus}
                  </Typography.Text>
                  <Space size={[4, 4]} wrap>
                    {item.labelKeys.map((k) => (
                      <Tag key={`${item.reportId}-${k}`}>{k}</Tag>
                    ))}
                  </Space>
                  <Typography.Text type="secondary">
                    Created: {new Date(item.createdAtUtc).toLocaleString()} {item.finalizedAtUtc ? `· Finalized: ${new Date(item.finalizedAtUtc).toLocaleString()}` : ''}
                  </Typography.Text>
                  <Typography.Text type="secondary">
                    Submission: {item.submission.lifecycle}
                    {item.submission.outboxMessageId ? ` · Outbox: ${item.submission.outboxMessageId.slice(0, 8)}` : ''}
                    {item.submission.hasMissingOutboxReference ? ' · Missing outbox reference' : ''}
                  </Typography.Text>
                  {item.submission.lastErrorMessage ? (
                    <Typography.Text type="warning">{item.submission.lastErrorMessage}</Typography.Text>
                  ) : null}
                </Space>
              ),
            }))}
          />
        ) : (
          <Typography.Text type="secondary">Keine Korrekturkette vorhanden.</Typography.Text>
        )}
      </Card>
    </div>
  );
}
