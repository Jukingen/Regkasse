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
import { useQuery } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_GROUP_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import { getApiAdminIntegrity } from '@/api/generated/admin/admin';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { IntegrityReportDto } from '@/api/generated/model';

const { RangePicker } = DatePicker;

function severityTag(count: number, t: (key: string) => string) {
  if (count <= 0) return <Tag color="success">{t('rksvHub.integrityPage.severityOk')}</Tag>;
  if (count <= 5) return <Tag color="warning">{t('rksvHub.integrityPage.severityReview')}</Tag>;
  return <Tag color="error">{t('rksvHub.integrityPage.severityAction')}</Tag>;
}

export default function IntegrityReportPage() {
  const { t } = useI18n();
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
        title={t('rksvHub.integrityPage.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_GROUP_LABEL_KEYS.rksv), href: '/rksv' },
          { title: t('rksvHub.integrityPage.breadcrumb') },
        ]}
      />

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('rksvHub.integrityPage.alertTitle')}
        description={
          <span>
            {t('rksvHub.integrityPage.alertIntro')}{' '}
            <Link href="/rksv/fiscal-export-diagnostics">{t('rksvHub.integrityPage.alertLinkFiscalExportDiag')}</Link>
            {t('rksvHub.integrityPage.alertMidOffline')}{' '}
            <Link href="/rksv/incident">{t('rksvHub.integrityPage.alertLinkIncident')}</Link>
            {t('rksvHub.integrityPage.alertSlashReplay')}
            <Link href="/rksv/replay-batch">{t('rksvHub.integrityPage.alertLinkReplayBatch')}</Link>
            {t('rksvHub.integrityPage.alertMidFo')}{' '}
            <Link href="/rksv/finanz-online-queue">{t('rksvHub.integrityPage.alertLinkFoQueue')}</Link>
            {t('rksvHub.integrityPage.alertMidHash')}{' '}
            <Link href="/rksv/payload-hash-conflicts">{t('rksvHub.integrityPage.alertLinkPayloadHash')}</Link>
            {t('rksvHub.integrityPage.alertEnd')}
          </span>
        }
      />

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space wrap align="center">
          <Typography.Text strong>{t('rksvHub.integrityPage.filterRangeLabel')}</Typography.Text>
          <RangePicker value={range} onChange={(v) => v && v[0] && v[1] && setRange([v[0], v[1]])} allowClear={false} />
          <Space align="center">
            <Typography.Text>{t('rksvHub.integrityPage.detailsSwitchLabel')}</Typography.Text>
            <Switch checked={includeDetails} onChange={setIncludeDetails} />
          </Space>
          <Button icon={<ReloadOutlined />} loading={isLoading || isFetching} onClick={() => refetch()}>
            {t('common.buttons.refresh')}
          </Button>
        </Space>
        {report?.generatedAtUtc && (
          <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
            {t('rksvHub.integrityPage.generatedUtc', {
              ts: dayjs(report.generatedAtUtc).format('DD.MM.YYYY HH:mm:ss'),
            })}
          </Typography.Paragraph>
        )}
      </Card>

      {error && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('rksvHub.integrityPage.loadFailedTitle')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={error}
              logContext="IntegrityReport.load"
              fallbackKey="rksvHub.integrityPage.loadFailedUnknown"
            />
          }
        />
      )}

      {!error && report && (
        <>
          {hasAnyIssue ? (
            <Alert type="warning" showIcon style={{ marginBottom: 16 }} title={t('rksvHub.integrityPage.hasIssuesTitle')} />
          ) : (
            <Alert
              type="success"
              showIcon
              style={{ marginBottom: 16 }}
              title={t('rksvHub.integrityPage.noIssuesTitle')}
            />
          )}

          <Row gutter={[16, 16]}>
            <Col xs={24} lg={8}>
              <Card
                size="small"
                title={t('rksvHub.integrityPage.cardSequenceTitle')}
                extra={severityTag(seq?.duplicateReceiptNumberCount ?? 0, t)}
              >
                <Descriptions column={1} size="small">
                  <Descriptions.Item label={t('rksvHub.integrityPage.dupReceiptLabel')}>
                    {seq?.duplicateReceiptNumberCount ?? 0}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('rksvHub.integrityPage.nonMonoSeqLabel')}>
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
                        title: t('rksvHub.integrityPage.colReceiptNumber'),
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
                        title: t('rksvHub.integrityPage.colRegisterDate'),
                        key: 'k',
                        render: (_: unknown, r: string) => <Typography.Text code>{r}</Typography.Text>,
                      },
                    ]}
                  />
                )}
              </Card>
            </Col>

            <Col xs={24} lg={8}>
              <Card
                size="small"
                title={t('rksvHub.integrityPage.cardRefundTitle')}
                extra={severityTag(orphans?.orphanRefundCount ?? 0, t)}
              >
                <Descriptions column={1} size="small">
                  <Descriptions.Item label={t('rksvHub.integrityPage.orphanTotalLabel')}>
                    {orphans?.orphanRefundCount ?? 0}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('rksvHub.integrityPage.missingOriginalLabel')}>
                    {orphans?.missingOriginalPaymentCount ?? 0}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('rksvHub.integrityPage.refundWithoutInvoiceLabel')}>
                    {orphans?.refundWithoutInvoiceCount ?? 0}
                  </Descriptions.Item>
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
                        title: t('rksvHub.integrityPage.colPaymentId'),
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
                          title: t('rksvHub.integrityPage.colRefundReceipt'),
                          key: 'n',
                          render: (_: unknown, r: string) => <Typography.Text code>{r || '—'}</Typography.Text>,
                        },
                      ]}
                    />
                  )}
              </Card>
            </Col>

            <Col xs={24} lg={8}>
              <Card size="small" title={t('rksvHub.integrityPage.cardPwiTitle')} extra={severityTag(pwi?.count ?? 0, t)}>
                <Descriptions column={1} size="small">
                  <Descriptions.Item label={t('rksvHub.integrityPage.pwiCountLabel')}>{pwi?.count ?? 0}</Descriptions.Item>
                </Descriptions>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
                  {t('rksvHub.integrityPage.pwiHint')}{' '}
                  <Link href="/payments">{t('rksvHub.integrityPage.paymentsLink')}</Link>{' '}
                  {t('rksvHub.integrityPage.pwiHintAfter')}
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
                        title: t('rksvHub.integrityPage.colPaymentId'),
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

          <Card size="small" title={t('rksvHub.integrityPage.relatedToolsTitle')} style={{ marginTop: 16 }}>
            <Space wrap>
              <Link href="/rksv/fiscal-export-diagnostics">{t('rksvHub.integrityPage.linkFiscalExportDiagShort')}</Link>
              <span>·</span>
              <Link href="/rksv/finanz-online-queue">{t('rksvHub.integrityPage.linkFoQueueShort')}</Link>
              <span>·</span>
              <Link href="/rksv/incident">{t('rksvHub.integrityPage.linkIncidentCorr')}</Link>
              <span>·</span>
              <Link href="/rksv/payload-hash-conflicts">{t('rksvHub.link.payloadHash')}</Link>
              <span>·</span>
              <Link href="/rksv/offline-intent-coverage">{t('rksvHub.integrityPage.linkOfflineCoverage')}</Link>
              <span>·</span>
              <Link href="/payments">{t('rksvHub.integrityPage.paymentsLink')}</Link>
            </Space>
          </Card>
        </>
      )}
    </div>
  );
}
