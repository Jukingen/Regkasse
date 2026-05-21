'use client';

/**
 * RKSV diagnostic compliance report (5 checks). Read-only; not a legal Finanzamt proof.
 */

import React, { useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Collapse,
  DatePicker,
  Descriptions,
  Dropdown,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { MenuProps } from 'antd';
import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import Link from 'next/link';
import dayjs, { type Dayjs } from 'dayjs';
import type { TranslateFn } from '@/shared/errors/userFacingApiError';
import { useQuery } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminTableScrollXy, shouldUseAdminTableVirtual } from '@/components/ui/adminTableVirtual';
import { ADMIN_NAV_GROUP_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import {
  downloadRksvComplianceReportPdf,
  getAdminCashRegisters,
  getRksvComplianceReportJson,
} from '@/api/admin-rksv/client';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import {
  exportComplianceReportCsv,
  exportComplianceReportJson,
} from '@/features/rksv/compliance/exportComplianceReport';
import { findRegistersMissingStartbeleg } from '@/features/rksv/compliance/startbelegCoverage';
import type {
  RksvComplianceReport,
  RksvComplianceReportQueryParams,
  RksvComplianceSignatureChainItem,
} from '@/features/rksv/compliance/types';

const { RangePicker } = DatePicker;

type CheckSeverity = 'ok' | 'warn' | 'fail';

function severityFromCount(count: number): CheckSeverity {
  if (count <= 0) return 'ok';
  if (count <= 3) return 'warn';
  return 'fail';
}

function CheckStatusBadge({
  severity,
  count,
  t,
}: {
  severity: CheckSeverity;
  count: number;
  t: TranslateFn;
}) {
  if (severity === 'ok') {
    return <Tag color="success">{t('rksvHub.compliancePage.statusOk')}</Tag>;
  }
  if (severity === 'warn') {
    return (
      <Tag color="warning">
        {t('rksvHub.compliancePage.statusReview', { count })}
      </Tag>
    );
  }
  return (
    <Tag color="error">
      {t('rksvHub.compliancePage.statusAction', { count })}
    </Tag>
  );
}

function chainStatusTag(status: string | undefined, t: (key: string) => string) {
  if (status === 'Pass') return <Tag color="success">{status}</Tag>;
  if (status === 'Warn') return <Tag color="warning">{status}</Tag>;
  return <Tag color="error">{status ?? t('rksvHub.compliancePage.chainUnknown')}</Tag>;
}

function complianceTableVirtualProps(rowCount: number, scrollX = 900) {
  if (!shouldUseAdminTableVirtual(rowCount)) {
    return {};
  }
  return { virtual: true as const, scroll: adminTableScrollXy(scrollX, rowCount) };
}

export default function RksvComplianceDashboard() {
  const { t } = useI18n();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null]>([
    dayjs().subtract(30, 'day').startOf('day'),
    dayjs().endOf('day'),
  ]);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>(undefined);
  const [pdfLoading, setPdfLoading] = useState(false);

  const fromUtc = range[0]?.toISOString();
  const toUtc = range[1]?.toISOString();

  const queryParams = useMemo<RksvComplianceReportQueryParams>(
    () => ({
      cashRegisterId,
      fromUtc,
      toUtc,
    }),
    [cashRegisterId, fromUtc, toUtc],
  );

  const { data: cashRegisters, isLoading: registersLoading } = useQuery({
    queryKey: rksvAdminQueryKeys.cashRegisters,
    queryFn: getAdminCashRegisters,
    staleTime: 60_000,
  });

  const { data, isLoading, isFetching, error, refetch } = useQuery({
    queryKey: rksvAdminQueryKeys.complianceReport(queryParams),
    queryFn: () => getRksvComplianceReportJson(queryParams),
    enabled: Boolean(fromUtc && toUtc),
  });

  const report = data as RksvComplianceReport | undefined;
  const summary = report?.summary;

  const startbelegMissing = useMemo(
    () =>
      findRegistersMissingStartbeleg(
        cashRegisters ?? [],
        report?.specialReceipts ?? [],
        cashRegisterId,
      ),
    [cashRegisters, report?.specialReceipts, cashRegisterId],
  );

  const chainIssues = useMemo(
    () => (report?.signatureChain ?? []).filter((c) => c.status && c.status !== 'Pass'),
    [report?.signatureChain],
  );

  const checkSeverities = useMemo(
    () => ({
      startbeleg: severityFromCount(startbelegMissing.length),
      chain: severityFromCount(summary?.signatureChainBreaks ?? chainIssues.length),
      sequence: severityFromCount(summary?.sequenceGapCount ?? 0),
      tse: severityFromCount(summary?.tseSignatureMissingCount ?? 0),
      qr: severityFromCount(
        (summary?.qrFormatInvalidCount ?? 0) + (summary?.qrFormatMissingCount ?? 0),
      ),
    }),
    [startbelegMissing.length, summary, chainIssues.length],
  );

  const handlePdfDownload = async () => {
    if (!fromUtc || !toUtc) return;
    setPdfLoading(true);
    try {
      const blob = await downloadRksvComplianceReportPdf(queryParams);
      const url = globalThis.URL.createObjectURL(blob);
      const a = globalThis.document.createElement('a');
      a.href = url;
      a.download = `rksv-compliance-report_${dayjs().format('YYYYMMDD_HHmmss')}_UTC.pdf`;
      a.click();
      globalThis.URL.revokeObjectURL(url);
    } finally {
      setPdfLoading(false);
    }
  };

  const exportMenu: MenuProps['items'] = useMemo(
    () => [
      {
        key: 'json',
        label: t('rksvHub.compliancePage.exportJson'),
        onClick: () => report && exportComplianceReportJson(report),
        disabled: !report,
      },
      {
        key: 'csv',
        label: t('rksvHub.compliancePage.exportCsv'),
        onClick: () => report && exportComplianceReportCsv(report),
        disabled: !report,
      },
      {
        key: 'pdf',
        label: t('rksvHub.compliancePage.exportPdf'),
        disabled: !fromUtc || !toUtc || pdfLoading,
        onClick: () => void handlePdfDownload(),
      },
    ],
    [t, report, fromUtc, toUtc, pdfLoading, queryParams],
  );

  const collapseItems = [
    {
      key: 'startbeleg',
      label: (
        <Space>
          <Typography.Text strong>{t('rksvHub.compliancePage.checkStartbelegTitle')}</Typography.Text>
          <CheckStatusBadge severity={checkSeverities.startbeleg} count={startbelegMissing.length} t={t} />
        </Space>
      ),
      children: (
        <>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
            {t('rksvHub.compliancePage.checkStartbelegHint')}{' '}
            <Link href="/rksv/sonderbelege">{t('rksvHub.compliancePage.linkSonderbelege')}</Link>
          </Typography.Paragraph>
          {startbelegMissing.length > 0 ? (
            <Table
              size="small"
              pagination={{ pageSize: 10 }}
              rowKey="cashRegisterId"
              dataSource={startbelegMissing}
              {...complianceTableVirtualProps(startbelegMissing.length, 640)}
              columns={[
                {
                  title: t('rksvHub.compliancePage.colRegister'),
                  key: 'reg',
                  render: (_: unknown, row) => (
                    <Typography.Text code>{row.registerNumber ?? row.cashRegisterId.slice(0, 8)}</Typography.Text>
                  ),
                },
                {
                  title: t('rksvHub.compliancePage.colRegisterId'),
                  dataIndex: 'cashRegisterId',
                  key: 'id',
                  render: (id: string) => <Typography.Text type="secondary">{id}</Typography.Text>,
                },
              ]}
            />
          ) : (
            <Alert type="success" showIcon message={t('rksvHub.compliancePage.startbelegAllPresent')} />
          )}
          {(report?.specialReceipts?.length ?? 0) > 0 && (
            <Table
              size="small"
              style={{ marginTop: 16 }}
              pagination={{ pageSize: 8 }}
              rowKey={(r) => `${r.paymentId}-${r.kind}`}
              dataSource={report?.specialReceipts}
              {...complianceTableVirtualProps(report?.specialReceipts?.length ?? 0, 960)}
              columns={[
                { title: t('rksvHub.compliancePage.colKind'), dataIndex: 'kind', key: 'kind' },
                {
                  title: t('rksvHub.compliancePage.colReceiptNumber'),
                  dataIndex: 'receiptNumber',
                  key: 'receiptNumber',
                  render: (n: string) => <Typography.Text code>{n}</Typography.Text>,
                },
                {
                  title: t('rksvHub.compliancePage.colRegister'),
                  key: 'reg',
                  render: (_: unknown, r) => r.registerNumber ?? '—',
                },
                {
                  title: t('rksvHub.compliancePage.colTse'),
                  key: 'tse',
                  render: (_: unknown, r) =>
                    r.hasTseSignature ? (
                      <Tag color="success">{t('rksvHub.compliancePage.tsePresent')}</Tag>
                    ) : (
                      <Tag color="error">{t('rksvHub.compliancePage.tseMissing')}</Tag>
                    ),
                },
              ]}
            />
          )}
        </>
      ),
    },
    {
      key: 'chain',
      label: (
        <Space>
          <Typography.Text strong>{t('rksvHub.compliancePage.checkChainTitle')}</Typography.Text>
          <CheckStatusBadge
            severity={checkSeverities.chain}
            count={summary?.signatureChainBreaks ?? chainIssues.length}
            t={t}
          />
        </Space>
      ),
      children: (
        <>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
            <Link href="/rksv/signature-chain">{t('rksvHub.compliancePage.linkSignatureChainTool')}</Link>
          </Typography.Paragraph>
          {chainIssues.length > 0 ? (
        <Table
          size="small"
          pagination={{ pageSize: 10 }}
          rowKey={(r: RksvComplianceSignatureChainItem) => r.receiptId ?? r.receiptNumber ?? ''}
          dataSource={chainIssues}
          {...complianceTableVirtualProps(chainIssues.length, 1100)}
          columns={[
            {
              title: t('rksvHub.compliancePage.colReceiptNumber'),
              dataIndex: 'receiptNumber',
              key: 'receiptNumber',
              render: (n: string) => <Typography.Text code>{n}</Typography.Text>,
            },
            {
              title: t('rksvHub.compliancePage.colStatus'),
              dataIndex: 'status',
              key: 'status',
              render: (s: string) => chainStatusTag(s, t),
            },
            { title: t('rksvHub.compliancePage.colIssue'), dataIndex: 'issue', key: 'issue' },
            {
              title: t('rksvHub.compliancePage.colIssuedUtc'),
              dataIndex: 'issuedAtUtc',
              key: 'issuedAtUtc',
              render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY HH:mm:ss') : '—'),
            },
          ]}
        />
      ) : (
        <Alert type="success" showIcon message={t('rksvHub.compliancePage.chainOk')} />
      )}
        </>
      ),
    },
    {
      key: 'sequence',
      label: (
        <Space>
          <Typography.Text strong>{t('rksvHub.compliancePage.checkSequenceTitle')}</Typography.Text>
          <CheckStatusBadge severity={checkSeverities.sequence} count={summary?.sequenceGapCount ?? 0} t={t} />
        </Space>
      ),
      children: (report?.sequenceGaps?.length ?? 0) > 0 ? (
        <Table
          size="small"
          pagination={{ pageSize: 10 }}
          rowKey={(r) =>
            `${r.cashRegisterId}-${r.sequenceDateUtc}-${r.expectedSequence}-${r.previousReceiptNumber}`
          }
          dataSource={report?.sequenceGaps}
          {...complianceTableVirtualProps(report?.sequenceGaps?.length ?? 0, 1000)}
          columns={[
            {
              title: t('rksvHub.compliancePage.colRegister'),
              key: 'reg',
              render: (_: unknown, r) => r.registerNumber ?? '—',
            },
            {
              title: t('rksvHub.compliancePage.colSequenceDate'),
              dataIndex: 'sequenceDateUtc',
              key: 'sequenceDateUtc',
              render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY') : '—'),
            },
            {
              title: t('rksvHub.compliancePage.colExpectedSeq'),
              dataIndex: 'expectedSequence',
              key: 'expectedSequence',
            },
            {
              title: t('rksvHub.compliancePage.colPrevReceipt'),
              dataIndex: 'previousReceiptNumber',
              key: 'previousReceiptNumber',
              render: (n: string | null) => <Typography.Text code>{n ?? '—'}</Typography.Text>,
            },
            {
              title: t('rksvHub.compliancePage.colNextReceipt'),
              dataIndex: 'nextReceiptNumber',
              key: 'nextReceiptNumber',
              render: (n: string | null) => <Typography.Text code>{n ?? '—'}</Typography.Text>,
            },
          ]}
        />
      ) : (
        <Alert type="success" showIcon message={t('rksvHub.compliancePage.sequenceOk')} />
      ),
    },
    {
      key: 'tse',
      label: (
        <Space>
          <Typography.Text strong>{t('rksvHub.compliancePage.checkTseTitle')}</Typography.Text>
          <CheckStatusBadge severity={checkSeverities.tse} count={summary?.tseSignatureMissingCount ?? 0} t={t} />
        </Space>
      ),
      children: (report?.tseSignatureMissing?.length ?? 0) > 0 ? (
        <Table
          size="small"
          pagination={{ pageSize: 10 }}
          rowKey={(r) => r.paymentId ?? r.receiptNumber ?? ''}
          dataSource={report?.tseSignatureMissing}
          {...complianceTableVirtualProps(report?.tseSignatureMissing?.length ?? 0, 900)}
          columns={[
            {
              title: t('rksvHub.compliancePage.colReceiptNumber'),
              dataIndex: 'receiptNumber',
              key: 'receiptNumber',
              render: (n: string) => <Typography.Text code>{n}</Typography.Text>,
            },
            {
              title: t('rksvHub.compliancePage.colRegister'),
              key: 'reg',
              render: (_: unknown, r) => r.registerNumber ?? '—',
            },
            {
              title: t('rksvHub.compliancePage.colTseSources'),
              key: 'src',
              render: (_: unknown, r) => (
                <Space size={4} wrap>
                  {r.paymentSignatureMissing && (
                    <Tag>{t('rksvHub.compliancePage.tseSourcePayment')}</Tag>
                  )}
                  {r.receiptSignatureMissing && (
                    <Tag>{t('rksvHub.compliancePage.tseSourceReceipt')}</Tag>
                  )}
                </Space>
              ),
            },
          ]}
        />
      ) : (
        <Alert type="success" showIcon message={t('rksvHub.compliancePage.tseOk')} />
      ),
    },
    {
      key: 'qr',
      label: (
        <Space>
          <Typography.Text strong>{t('rksvHub.compliancePage.checkQrTitle')}</Typography.Text>
          <CheckStatusBadge
            severity={checkSeverities.qr}
            count={(summary?.qrFormatInvalidCount ?? 0) + (summary?.qrFormatMissingCount ?? 0)}
            t={t}
          />
        </Space>
      ),
      children: (report?.qrPayloadValidation?.length ?? 0) > 0 ? (
        <Table
          size="small"
          pagination={{ pageSize: 10 }}
          rowKey={(r) => r.receiptId ?? r.receiptNumber ?? ''}
          dataSource={report?.qrPayloadValidation}
          {...complianceTableVirtualProps(report?.qrPayloadValidation?.length ?? 0, 800)}
          columns={[
            {
              title: t('rksvHub.compliancePage.colReceiptNumber'),
              dataIndex: 'receiptNumber',
              key: 'receiptNumber',
              render: (n: string) => <Typography.Text code>{n}</Typography.Text>,
            },
            {
              title: t('rksvHub.compliancePage.colQrStatus'),
              key: 'qr',
              render: (_: unknown, r) =>
                r.qrPayloadMissing ? (
                  <Tag color="error">{t('rksvHub.compliancePage.qrMissing')}</Tag>
                ) : r.isValidFormat ? (
                  <Tag color="success">{t('rksvHub.compliancePage.qrValid')}</Tag>
                ) : (
                  <Tag color="error">{t('rksvHub.compliancePage.qrInvalid')}</Tag>
                ),
            },
            {
              title: t('rksvHub.compliancePage.colQrErrors'),
              key: 'errors',
              render: (_: unknown, r) => (r.errors?.length ? r.errors.join('; ') : '—'),
            },
          ]}
        />
      ) : (
        <Alert type="success" showIcon message={t('rksvHub.compliancePage.qrOk')} />
      ),
    },
  ];

  return (
    <div>
      <AdminPageHeader
        title={t('rksvHub.compliancePage.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_GROUP_LABEL_KEYS.rksv), href: '/rksv' },
          { title: t('rksvHub.compliancePage.breadcrumb') },
        ]}
      />

      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 16 }}
        message={t('rksvHub.compliancePage.legalAlertTitle')}
        description={report?.legalNoticeDe ?? t('rksvHub.compliancePage.legalAlertFallback')}
      />

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space wrap align="center">
          <Typography.Text strong>{t('rksvHub.compliancePage.filterRegisterLabel')}</Typography.Text>
          <Select
            allowClear
            showSearch
            placeholder={t('rksvHub.compliancePage.filterRegisterAll')}
            style={{ minWidth: 220 }}
            loading={registersLoading}
            value={cashRegisterId}
            onChange={(v) => setCashRegisterId(v)}
            options={(cashRegisters ?? [])
              .filter((r) => r.id)
              .map((r) => ({
                value: r.id as string,
                label: r.registerNumber ? `${r.registerNumber} (${r.id?.slice(0, 8)}…)` : r.id,
              }))}
          />
          <Typography.Text strong>{t('rksvHub.compliancePage.filterRangeLabel')}</Typography.Text>
          <RangePicker
            value={range}
            onChange={(v) => setRange(v ?? [null, null])}
            allowClear={false}
            showTime
          />
          <Button icon={<ReloadOutlined />} loading={isLoading || isFetching} onClick={() => refetch()}>
            {t('common.buttons.refresh')}
          </Button>
          <Dropdown menu={{ items: exportMenu }}>
            <Button icon={<DownloadOutlined />} disabled={!report}>
              {t('rksvHub.compliancePage.exportButton')}
            </Button>
          </Dropdown>
        </Space>
        {report?.generatedAtUtc && (
          <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
            {t('rksvHub.compliancePage.generatedUtc', {
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
          message={t('rksvHub.compliancePage.loadFailedTitle')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={error}
              logContext="RksvComplianceReport.load"
              fallbackKey="rksvHub.compliancePage.loadFailedUnknown"
            />
          }
        />
      )}

      {!error && report && (
        <>
          {summary?.overallPass ? (
            <Alert type="success" showIcon style={{ marginBottom: 16 }} message={t('rksvHub.compliancePage.overallPass')} />
          ) : (
            <Alert type="warning" showIcon style={{ marginBottom: 16 }} message={t('rksvHub.compliancePage.overallFail')} />
          )}

          <Card size="small" style={{ marginBottom: 16 }}>
            <Descriptions column={{ xs: 1, sm: 2, md: 3 }} size="small">
              <Descriptions.Item label={t('rksvHub.compliancePage.summaryRegisters')}>
                {summary?.registersCovered ?? 0}
              </Descriptions.Item>
              <Descriptions.Item label={t('rksvHub.compliancePage.summaryReceipts')}>
                {summary?.fiscalReceiptsScanned ?? 0}
              </Descriptions.Item>
              <Descriptions.Item label={t('rksvHub.compliancePage.summarySonderbelege')}>
                {summary?.specialReceiptsCount ?? 0}
              </Descriptions.Item>
            </Descriptions>
          </Card>

          <Collapse defaultActiveKey={['startbeleg', 'chain']} items={collapseItems} />
        </>
      )}
    </div>
  );
}
