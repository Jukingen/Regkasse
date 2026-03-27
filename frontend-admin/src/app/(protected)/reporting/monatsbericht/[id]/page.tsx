'use client';

/**
 * Monatsbericht-Detail: verknüpfte Tagesberichte, Aggregation vs. Rohdaten, Profile, Finalisierung, FinanzOnline, Korrekturkette.
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
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

type MonatsberichtDto = {
  id: string;
  viennaMonthStart: string;
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
    viennaYearMonth: string;
    linkedFinalizedTagesberichte: {
      tagesberichtId: string;
      viennaBusinessDate: string;
      cashRegisterId: string;
      registerNumber?: string | null;
      snapshotHash: string;
      grossSalesAmount: number;
    }[];
    aggregationFromDaily: {
      linkedDailyReportCount: number;
      expectedCalendarDaysInMonth: number;
      distinctDaysCovered: number;
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
      grossDeltaDailyVsRaw: number;
      requiresReview: boolean;
      noteDe?: string | null;
    };
    paymentMethodBreakdown: { methodKey: string; displayLabel?: string; rowCount: number; totalAmount: number }[];
    taxBreakdown: { taxBucketKey: string; taxAmount: number }[];
    warnings: string[];
  };
  submission: {
    lifecycle: string;
    operatorHintDe?: string;
    outboxStatus?: string;
    externalReferenceId?: string;
  };
  submissionEnvelope?: {
    attempts?: { attemptCount: number; status?: string; nextAttemptAtUtc?: string; failureCategory?: string }[];
    rejectionReasons?: string[];
    remediationHintsDe?: string[];
  };
  exportProfiles: { profileKey: string; labelDe: string; descriptionDe: string; includeTraceIds: boolean; nonLegalOutput?: boolean; isDiagnosticOnly?: boolean }[];
  correction: { isCorrection: boolean; supersedesReportId?: string | null; supersededByReportId?: string | null };
};

export default function MonatsberichtDetailPage() {
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
    queryKey: ['monatsbericht', id],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<MonatsberichtDto>(`/api/reports/monatsbericht/${id}`);
      return data;
    },
    enabled: !!id,
  });

  const finalizeMut = useMutation({
    mutationFn: async () => {
      await AXIOS_INSTANCE.post('/api/reports/monatsbericht/finalize', { reportId: id, note: null });
    },
    onSuccess: () => {
      message.success('Finalisiert.');
      qc.invalidateQueries({ queryKey: ['monatsbericht', id] });
    },
    onError: () => message.error('Finalisierung fehlgeschlagen.'),
  });

  const submitMut = useMutation({
    mutationFn: async () => {
      await AXIOS_INSTANCE.post(`/api/reports/monatsbericht/${id}/submit-finanzonline`);
    },
    onSuccess: () => {
      message.success('In FinanzOnline-Outbox eingereiht (idempotent möglich).');
      qc.invalidateQueries({ queryKey: ['monatsbericht', id] });
    },
    onError: () => message.error('Übermittlung fehlgeschlagen.'),
  });

  const correctionMut = useMutation({
    mutationFn: async () => {
      const { data } = await AXIOS_INSTANCE.post<MonatsberichtDto>('/api/reports/monatsbericht/correction', {
        supersedesReportId: id,
        reason: 'Korrektur / Neuberechnung',
      });
      return data;
    },
    onSuccess: (data) => {
      message.success('Korrekturbericht erzeugt.');
      qc.invalidateQueries({ queryKey: ['monatsbericht'] });
      if (data?.id) router.push(`/reporting/monatsbericht/${data.id}`);
    },
    onError: () => message.error('Korrektur fehlgeschlagen (nur nach finalisiertem Vorgänger).'),
  });

  const d = detailQ.data;
  const showHashes = profile !== 'operationalPreview';
  const showTrace = profile === 'legalComplianceExport' || profile === 'diagnosticPackage';

  const linkedCols: ColumnsType<MonatsberichtDto['summary']['linkedFinalizedTagesberichte'][0]> = useMemo(
    () => [
      {
        title: 'Datum',
        dataIndex: 'viennaBusinessDate',
        render: (v: string) => dayjs(v).format('YYYY-MM-DD'),
      },
      { title: 'Kasse', dataIndex: 'registerNumber', render: (v, r) => v ?? r.cashRegisterId.slice(0, 8) },
      {
        title: 'Brutto',
        dataIndex: 'grossSalesAmount',
        render: (v: number) => v?.toFixed(2),
      },
      {
        title: 'Tagesbericht',
        key: 'link',
        render: (_, r) => <Link href={`/reporting/tagesbericht/${r.tagesberichtId}`}>Öffnen</Link>,
      },
    ],
    [],
  );

  const pmCols: ColumnsType<MonatsberichtDto['summary']['paymentMethodBreakdown'][0]> = useMemo(
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

  const agg = d.summary.aggregationFromDaily;
  const raw = d.summary.rawPaymentRollup;
  const adj = d.summary.adjustment;

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader
        title={`Monatsbericht ${d.summary.viennaYearMonth}`}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: 'Monatsbericht', href: '/reporting/monatsbericht' },
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
            <Button onClick={() => router.push('/reporting/monatsbericht')}>Zur Liste</Button>
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
          {d.exportProfiles?.length ? (
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
              {d.exportProfiles.map((p) => (
                <span key={p.profileKey} style={{ marginRight: 12 }}>
                  <strong>{p.labelDe}:</strong> {p.descriptionDe}
                </span>
              ))}
            </Typography.Paragraph>
          ) : null}
        </Space>
      </Card>

      <Card title="Status" style={{ marginBottom: 16 }}>
        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label="Umfang">
            <Tag>{d.scopeKind}</Tag>{' '}
            {d.scopeKind === 'Company' ? 'alle Kassen' : d.registerNumber ?? d.cashRegisterId}
          </Descriptions.Item>
          <Descriptions.Item label="Bericht">
            <Tag color={d.reportStatus === 'Finalized' ? 'blue' : 'gold'}>{d.reportStatus}</Tag>
            {d.correction.isCorrection ? <Tag color="orange">Korrektur</Tag> : null}
          </Descriptions.Item>
          {d.reportVersion ? <Descriptions.Item label="Version">v{d.reportVersion}</Descriptions.Item> : null}
          {d.correctionType ? <Descriptions.Item label="Correction Type">{d.correctionType}</Descriptions.Item> : null}
          {d.submissionImpact ? <Descriptions.Item label="Submission Impact">{d.submissionImpact}</Descriptions.Item> : null}
          {d.reportRevisionReason ? <Descriptions.Item label="Revision Reason">{d.reportRevisionReason}</Descriptions.Item> : null}
          {d.rebuildCause ? <Descriptions.Item label="Rebuild Cause">{d.rebuildCause}</Descriptions.Item> : null}
          {d.correction.supersedesReportId ? (
            <Descriptions.Item label="Vorgänger">
              <Link href={`/reporting/monatsbericht/${d.correction.supersedesReportId}`}>
                {d.correction.supersedesReportId}
              </Link>
            </Descriptions.Item>
          ) : null}
          {d.supersededByReportId ? (
            <Descriptions.Item label="Ersetzt durch">
              <Link href={`/reporting/monatsbericht/${d.supersededByReportId}`}>{d.supersededByReportId}</Link>
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
          {showHashes ? <Descriptions.Item label="Snapshot-Hash">{d.snapshotHash}</Descriptions.Item> : null}
        </Descriptions>
      </Card>

      <Card title="Verknüpfte Tagesberichte (final)" style={{ marginBottom: 16 }}>
        <Table
          rowKey="tagesberichtId"
          size="small"
          pagination={false}
          dataSource={d.summary.linkedFinalizedTagesberichte}
          columns={linkedCols}
        />
      </Card>

      <Card title="Summen" style={{ marginBottom: 16 }}>
        <Descriptions column={2} size="small" bordered>
          <Descriptions.Item label="Tagesberichte (Anzahl)">{agg.linkedDailyReportCount}</Descriptions.Item>
          <Descriptions.Item label="Kalendertage / abgedeckt">
            {agg.distinctDaysCovered} / {agg.expectedCalendarDaysInMonth}
          </Descriptions.Item>
          <Descriptions.Item label="Brutto (aus Tagesberichten)">{agg.grossSalesAmount.toFixed(2)}</Descriptions.Item>
          <Descriptions.Item label="Steuer (Summe)">{agg.taxTotalAmount.toFixed(2)}</Descriptions.Item>
          <Descriptions.Item label="Erstattungen">{agg.refundAmountTotal.toFixed(2)}</Descriptions.Item>
          <Descriptions.Item label="Verkaufszeilen">{agg.salePaymentRowCount}</Descriptions.Item>
        </Descriptions>
      </Card>

      {(profile === 'accountingReport' || profile === 'legalComplianceExport' || profile === 'diagnosticPackage') && (
        <Card title="Rohdaten (PaymentDetails, Monatsfenster)" style={{ marginBottom: 16 }}>
          <Descriptions column={2} size="small" bordered>
            <Descriptions.Item label="Brutto">{raw.grossSalesAmount.toFixed(2)}</Descriptions.Item>
            <Descriptions.Item label="Steuer">{raw.taxTotalAmount.toFixed(2)}</Descriptions.Item>
            <Descriptions.Item label="Erstattungen">{raw.refundAmountTotal.toFixed(2)}</Descriptions.Item>
            <Descriptions.Item label="Verkaufszeilen">{raw.salePaymentRowCount}</Descriptions.Item>
          </Descriptions>
          <Descriptions column={1} size="small" bordered style={{ marginTop: 8 }}>
            <Descriptions.Item label="Δ Brutto (Tagesberichte − Roh)">{adj.grossDeltaDailyVsRaw.toFixed(2)}</Descriptions.Item>
            <Descriptions.Item label="Prüfung">
              {adj.requiresReview ? <Tag color="warning">Abweichung</Tag> : <Tag>ok</Tag>}
              {adj.noteDe ? ` — ${adj.noteDe}` : ''}
            </Descriptions.Item>
          </Descriptions>
        </Card>
      )}

      {(profile === 'accountingReport' || profile === 'legalComplianceExport' || profile === 'diagnosticPackage') && (
        <Card title="Steueraufschlüsselung (aus Tagesberichten)" style={{ marginBottom: 16 }}>
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

      <Card title="Zahlungsarten (aus Tagesberichten)" style={{ marginBottom: 16 }}>
        <Table rowKey="methodKey" size="small" pagination={false} dataSource={d.summary.paymentMethodBreakdown} columns={pmCols} />
      </Card>

      <Card title="Hinweise" style={{ marginBottom: 16 }}>
        {d.summary.warnings?.length ? (
          <ul>
            {d.summary.warnings.map((w) => (
              <li key={w}>
                <Typography.Text type="warning">{w}</Typography.Text>
              </li>
            ))}
          </ul>
        ) : (
          <Typography.Text type="secondary">Keine Warnungen.</Typography.Text>
        )}
      </Card>

      {showTrace ? (
        <Card title="Trace (Compliance)">
          <Typography.Paragraph type="secondary">
            Monats-Snapshot ist unveränderlich nach Finalisierung; Korrekturen erzeugen neue Berichtszeilen mit Verweis.
          </Typography.Paragraph>
        </Card>
      ) : null}
    </div>
  );
}
