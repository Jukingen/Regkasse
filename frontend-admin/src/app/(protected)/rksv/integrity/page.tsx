'use client';

/**
 * Operational integrity triage: backend consistency checks (sequences, refunds, payment↔invoice).
 */

import React, { useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Descriptions,
  Row,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import Link from 'next/link';
import dayjs, { type Dayjs } from 'dayjs';
import { OPERATOR_LINK_LABELS } from '@/shared/operatorTruthCopy';
import { useQuery } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { getApiAdminIntegrity } from '@/api/generated/admin/admin';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { IntegrityReportDto } from '@/api/generated/model';

const { RangePicker } = DatePicker;

function severityTag(count: number) {
  if (count <= 0) return <Tag color="success">OK</Tag>;
  if (count <= 5) return <Tag color="warning">Prüfen</Tag>;
  return <Tag color="error">Handlungsbedarf</Tag>;
}

export default function IntegrityReportPage() {
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [
    dayjs().subtract(7, 'day').startOf('day'),
    dayjs().endOf('day'),
  ]);
  const [includeDetails, setIncludeDetails] = useState(false);

  const fromDate = range[0]?.format('YYYY-MM-DD');
  /** Backend uses CreatedAt &lt; toDate (exclusive); send day after selected end for inclusive UI range. */
  const toDate = range[1]?.add(1, 'day').format('YYYY-MM-DD');

  const { data, isLoading, isFetching, error, refetch } = useQuery({
    queryKey: rksvAdminQueryKeys.integrity({ fromDate, toDate, includeDetails }),
    queryFn: () =>
      getApiAdminIntegrity({
        fromDate,
        toDate,
        includeDetails,
      }),
  });

  const report = data as IntegrityReportDto | undefined;
  const seq = report?.sequenceIssues;
  const orphans = report?.orphanRefunds;
  const pwi = report?.paymentWithoutInvoice;

  const hasAnyIssue = useMemo(() => {
    if (!report) return false;
    return (
      (seq?.duplicateReceiptNumberCount ?? 0) > 0 ||
      (seq?.nonMonotonicSequenceCount ?? 0) > 0 ||
      (orphans?.orphanRefundCount ?? 0) > 0 ||
      (pwi?.count ?? 0) > 0
    );
  }, [report, seq, orphans, pwi]);

  return (
    <div>
      <AdminPageHeader
        title="Datenintegrität (Support)"
        breadcrumbs={[
          { title: 'Dashboard', href: '/dashboard' },
          { title: 'RKSV', href: '/rksv' },
          { title: 'Integrität' },
        ]}
      />

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Diagnose, kein Rechtsnachweis"
        description={
          <span>
            Nur Konsistenzprüfungen im gewählten Zeitraum (Belegnummern/Sequenzen, Erstattungen, Zahlungen ohne
            Rechnungsbezug). Für Kettendiagnose siehe{' '}
            <Link href="/rksv/fiscal-export-diagnostics">Fiscal-Export Diagnose</Link>, für Offline-Replay{' '}
            <Link href="/rksv/incident">{OPERATOR_LINK_LABELS.incidentAggregate}</Link> /{' '}
            <Link href="/rksv/replay-batch">{OPERATOR_LINK_LABELS.replayBatch}</Link>, FO-Queue{' '}
            <Link href="/rksv/finanz-online-queue">FinanzOnline Abgleich</Link>, Hash-Konflikte{' '}
            <Link href="/rksv/payload-hash-conflicts">Payload-Hash</Link>.
          </span>
        }
      />

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space wrap align="center">
          <Typography.Text strong>Zeitraum (CreatedAt / IssuedAt je Check):</Typography.Text>
          <RangePicker value={range} onChange={(v) => v && v[0] && v[1] && setRange([v[0], v[1]])} allowClear={false} />
          <Space align="center">
            <Typography.Text>Details (IDs / Belegnr.):</Typography.Text>
            <Switch checked={includeDetails} onChange={setIncludeDetails} />
          </Space>
          <Button icon={<ReloadOutlined />} loading={isLoading || isFetching} onClick={() => refetch()}>
            Aktualisieren
          </Button>
        </Space>
        {report?.generatedAtUtc && (
          <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
            Generiert (UTC): {dayjs(report.generatedAtUtc).format('DD.MM.YYYY HH:mm:ss')}
          </Typography.Paragraph>
        )}
      </Card>

      {error && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          message="Integritätsbericht fehlgeschlagen"
          description={error instanceof Error ? error.message : 'Unbekannter Fehler (Berechtigung audit.view erforderlich).'}
        />
      )}

      {!error && report && (
        <>
          {hasAnyIssue ? (
            <Alert type="warning" showIcon style={{ marginBottom: 16 }} message="Auffälligkeiten im gewählten Umfang" />
          ) : (
            <Alert
              type="success"
              showIcon
              style={{ marginBottom: 16 }}
              message="Keine Treffer in den Hauptkategorien (gewählter Zeitraum)"
            />
          )}

          <Row gutter={[16, 16]}>
            <Col xs={24} lg={8}>
              <Card size="small" title="Belegsequenz / Duplikate" extra={severityTag(seq?.duplicateReceiptNumberCount ?? 0)}>
                <Descriptions column={1} size="small">
                  <Descriptions.Item label="Doppelte Belegnr. (Zahlung+Receipt)">
                    {seq?.duplicateReceiptNumberCount ?? 0}
                  </Descriptions.Item>
                  <Descriptions.Item label="Nicht-monotone Sequenz (pro Kasse/Tag)">
                    {seq?.nonMonotonicSequenceCount ?? 0}
                  </Descriptions.Item>
                </Descriptions>
                {includeDetails && seq?.duplicateReceiptNumbers && seq.duplicateReceiptNumbers.length > 0 && (
                  <Table
                    size="small"
                    pagination={{ pageSize: 8 }}
                    rowKey={(r) => r}
                    dataSource={seq.duplicateReceiptNumbers}
                    columns={[
                      {
                        title: 'Belegnummer',
                        key: 'n',
                        render: (_: unknown, r: string) => <Typography.Text code>{r}</Typography.Text>,
                      },
                    ]}
                  />
                )}
                {includeDetails && seq?.nonMonotonicKeys && seq.nonMonotonicKeys.length > 0 && (
                  <Table
                    size="small"
                    style={{ marginTop: 8 }}
                    pagination={{ pageSize: 8 }}
                    rowKey={(r) => r}
                    dataSource={seq.nonMonotonicKeys}
                    columns={[
                      {
                        title: 'KasseId|Datum',
                        key: 'k',
                        render: (_: unknown, r: string) => <Typography.Text code>{r}</Typography.Text>,
                      },
                    ]}
                  />
                )}
              </Card>
            </Col>

            <Col xs={24} lg={8}>
              <Card size="small" title="Erstattungen (Refund)" extra={severityTag(orphans?.orphanRefundCount ?? 0)}>
                <Descriptions column={1} size="small">
                  <Descriptions.Item label="Orphan-Gesamt (vereinigt)">{orphans?.orphanRefundCount ?? 0}</Descriptions.Item>
                  <Descriptions.Item label="Ohne gültige Original-Zahlung">
                    {orphans?.missingOriginalPaymentCount ?? 0}
                  </Descriptions.Item>
                  <Descriptions.Item label="Refund ohne Rechnung">{orphans?.refundWithoutInvoiceCount ?? 0}</Descriptions.Item>
                </Descriptions>
                {includeDetails && orphans?.orphanPaymentIds && orphans.orphanPaymentIds.length > 0 && (
                  <Table
                    size="small"
                    style={{ marginTop: 8 }}
                    pagination={{ pageSize: 8 }}
                    rowKey="id"
                    dataSource={orphans.orphanPaymentIds.map((id) => ({ id }))}
                    columns={[
                      {
                        title: 'Payment-ID',
                        dataIndex: 'id',
                        key: 'id',
                        render: (id: string) => (
                          <Link href={`/payments?paymentId=${encodeURIComponent(id)}`} target="_blank" rel="noreferrer">
                            <Typography.Text code>{id.slice(0, 8)}…</Typography.Text>
                          </Link>
                        ),
                      },
                    ]}
                  />
                )}
                {includeDetails &&
                  orphans?.refundReceiptNumbersMissingInvoice &&
                  orphans.refundReceiptNumbersMissingInvoice.length > 0 && (
                    <Table
                      size="small"
                      style={{ marginTop: 8 }}
                      pagination={{ pageSize: 8 }}
                      rowKey={(r) => r}
                    dataSource={orphans.refundReceiptNumbersMissingInvoice}
                    columns={[
                      {
                        title: 'Beleg (Refund)',
                        key: 'n',
                        render: (_: unknown, r: string) => <Typography.Text code>{r || '—'}</Typography.Text>,
                      },
                    ]}
                    />
                  )}
              </Card>
            </Col>

            <Col xs={24} lg={8}>
              <Card size="small" title="Zahlung ohne Rechnung" extra={severityTag(pwi?.count ?? 0)}>
                <Descriptions column={1} size="small">
                  <Descriptions.Item label="Anzahl (aktive Verkäufe, kein Invoice.SourcePaymentId)">
                    {pwi?.count ?? 0}
                  </Descriptions.Item>
                </Descriptions>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
                  Blockiert u. a. Tagesabschluss für die betroffene Kasse — Details in{' '}
                  <Link href="/payments">Payments</Link> öffnen.
                </Typography.Paragraph>
                {includeDetails && pwi?.paymentIds && pwi.paymentIds.length > 0 && (
                  <Table
                    size="small"
                    style={{ marginTop: 8 }}
                    pagination={{ pageSize: 10 }}
                    rowKey="id"
                    dataSource={pwi.paymentIds.map((id) => ({ id }))}
                    columns={[
                      {
                        title: 'Payment-ID',
                        dataIndex: 'id',
                        key: 'id',
                        render: (id: string) => (
                          <Link href={`/payments?paymentId=${encodeURIComponent(id)}`} target="_blank" rel="noreferrer">
                            <Typography.Text code>{id}</Typography.Text>
                          </Link>
                        ),
                      },
                    ]}
                  />
                )}
              </Card>
            </Col>
          </Row>

          <Card size="small" title="Verwandte Werkzeuge" style={{ marginTop: 16 }}>
            <Space wrap>
              <Link href="/rksv/fiscal-export-diagnostics">Fiscal-Export Diagnose</Link>
              <span>·</span>
              <Link href="/rksv/finanz-online-queue">FinanzOnline Abgleich</Link>
              <span>·</span>
              <Link href="/rksv/incident">Incident (Correlation)</Link>
              <span>·</span>
              <Link href="/rksv/payload-hash-conflicts">Payload-Hash</Link>
              <span>·</span>
              <Link href="/rksv/offline-intent-coverage">Offline Coverage</Link>
              <span>·</span>
              <Link href="/payments">Payments</Link>
            </Space>
          </Card>
        </>
      )}
    </div>
  );
}
