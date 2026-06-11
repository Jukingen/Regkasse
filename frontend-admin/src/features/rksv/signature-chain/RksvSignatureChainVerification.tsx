'use client';

/**
 * Dedicated RKSV signature chain verification (compliance-report API, chain + sequence + TSE scope).
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  DownloadOutlined,
  PrinterOutlined,
  SearchOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import dayjs, { type Dayjs } from 'dayjs';
import { useQuery } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_GROUP_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import { getAdminCashRegisters, getRksvComplianceReportJson } from '@/api/admin-rksv/client';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type {
  RksvComplianceReport,
  RksvComplianceReportQueryParams,
  RksvComplianceSignatureChainItem,
} from '@/features/rksv/compliance/types';
import { exportSignatureChainIssuesFromChain } from '@/features/rksv/signature-chain/exportSignatureChainCsv';
import {
  computeSignatureChainOutcome,
  filterReportForRegister,
  formatSignaturePreview,
  prevSignatureMatches,
} from '@/features/rksv/signature-chain/signatureChainUtils';
import styles from '@/features/rksv/signature-chain/signatureChainPage.module.css';

const { RangePicker } = DatePicker;

function chainStatusTag(status: string | undefined, t: (key: string) => string) {
  if (status === 'Pass') return <Tag color="success">{status}</Tag>;
  if (status === 'Warn') return <Tag color="warning">{status}</Tag>;
  return <Tag color="error">{status ?? t('rksvHub.signatureChainPage.statusUnknown')}</Tag>;
}

export default function RksvSignatureChainVerification({ embedded = false }: { embedded?: boolean }) {
  const { t } = useI18n();
  const searchParams = useSearchParams();
  const highlightReceiptId = searchParams.get('receiptId') ?? undefined;

  const [range, setRange] = useState<[Dayjs | null, Dayjs | null]>([
    dayjs().subtract(7, 'day').startOf('day'),
    dayjs().endOf('day'),
  ]);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>(undefined);
  const [verifiedParams, setVerifiedParams] = useState<RksvComplianceReportQueryParams | null>(null);

  const applySearchParams = useCallback(() => {
    const reg = searchParams.get('cashRegisterId');
    const from = searchParams.get('fromUtc');
    const to = searchParams.get('toUtc');
    if (reg) setCashRegisterId(reg);
    if (from && to) {
      setRange([dayjs(from), dayjs(to)]);
    }
    if (searchParams.get('autoVerify') === '1' && reg && from && to) {
      setVerifiedParams({ cashRegisterId: reg, fromUtc: from, toUtc: to });
    }
  }, [searchParams]);

  useEffect(() => {
    applySearchParams();
  }, [applySearchParams]);

  const fromUtc = range[0]?.toISOString();
  const toUtc = range[1]?.toISOString();

  const { data: cashRegisters, isLoading: registersLoading } = useQuery({
    queryKey: rksvAdminQueryKeys.cashRegisters,
    queryFn: getAdminCashRegisters,
    staleTime: 60_000,
  });

  const { data, isLoading, isFetching, error, refetch } = useQuery({
    queryKey: rksvAdminQueryKeys.complianceReport(verifiedParams),
    queryFn: () => getRksvComplianceReportJson(verifiedParams ?? {}),
    enabled: Boolean(verifiedParams?.fromUtc && verifiedParams?.toUtc),
  });

  const report = data as RksvComplianceReport | undefined;
  const activeRegisterId = verifiedParams?.cashRegisterId;

  const { chain, chainIssues, sequenceGaps, tseMissing } = useMemo(
    () => filterReportForRegister(report, activeRegisterId),
    [report, activeRegisterId],
  );

  const outcome = useMemo(
    () => computeSignatureChainOutcome(chainIssues, sequenceGaps, tseMissing),
    [chainIssues, sequenceGaps, tseMissing],
  );

  const handleVerify = () => {
    if (!cashRegisterId || !fromUtc || !toUtc) return;
    setVerifiedParams({ cashRegisterId, fromUtc, toUtc });
  };

  const handlePrint = () => {
    globalThis.print();
  };

  const handleExportCsv = () => {
    if (!report?.generatedAtUtc) return;
    exportSignatureChainIssuesFromChain(chain, report.generatedAtUtc);
  };

  const outcomeAlert = (() => {
    if (!verifiedParams || !report) return null;
    if (outcome === 'intact') {
      return (
        <Alert
          type="success"
          showIcon
          icon={<CheckCircleOutlined />}
          title={t('rksvHub.signatureChainPage.chainIntact')}
          description={t('rksvHub.signatureChainPage.chainIntactDetail', { count: chain.length })}
        />
      );
    }
    if (outcome === 'review') {
      return (
        <Alert
          type="warning"
          showIcon
          icon={<WarningOutlined />}
          title={t('rksvHub.signatureChainPage.chainReview')}
          description={t('rksvHub.signatureChainPage.chainReviewDetail', {
            count: chainIssues.length,
          })}
        />
      );
    }
    return (
      <Alert
        type="error"
        showIcon
        icon={<CloseCircleOutlined />}
        title={t('rksvHub.signatureChainPage.chainBroken')}
        description={t('rksvHub.signatureChainPage.chainBrokenDetail', {
          breaks: chainIssues.filter((c) => c.status === 'Fail').length,
          gaps: sequenceGaps.length,
          tse: tseMissing.length,
        })}
      />
    );
  })();

  const chainColumns = useMemo(
    () => [
      {
        title: t('rksvHub.signatureChainPage.receiptNumber'),
        dataIndex: 'receiptNumber',
        key: 'receiptNumber',
        render: (n: string, row: RksvComplianceSignatureChainItem) => (
          <Space size={4}>
            <Typography.Text code>{n}</Typography.Text>
            {row.receiptId && (
              <Link href={`/receipts/${row.receiptId}`}>{t('rksvHub.signatureChainPage.viewReceipt')}</Link>
            )}
          </Space>
        ),
      },
      {
        title: t('rksvHub.signatureChainPage.colIssuedUtc'),
        dataIndex: 'issuedAtUtc',
        key: 'issuedAtUtc',
        render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY HH:mm:ss') : '—'),
      },
      {
        title: t('rksvHub.signatureChainPage.signaturePreview'),
        key: 'sig',
        render: (_: unknown, row: RksvComplianceSignatureChainItem) => (
          <Typography.Text code>{formatSignaturePreview(row.signaturePrefix)}</Typography.Text>
        ),
      },
      {
        title: t('rksvHub.signatureChainPage.previousMatch'),
        key: 'match',
        render: (_: unknown, row: RksvComplianceSignatureChainItem) =>
          prevSignatureMatches(row) ? (
            <Tag color="success">{t('rksvHub.signatureChainPage.matchYes')}</Tag>
          ) : (
            <Tag color="error">{t('rksvHub.signatureChainPage.matchNo')}</Tag>
          ),
      },
      {
        title: t('rksvHub.signatureChainPage.colStatus'),
        dataIndex: 'status',
        key: 'status',
        render: (s: string) => chainStatusTag(s, t),
      },
      {
        title: t('rksvHub.signatureChainPage.colIssue'),
        dataIndex: 'issue',
        key: 'issue',
        render: (issue: string | null) => issue ?? '—',
      },
    ],
    [t],
  );

  return (
    <div className="rksv-signature-chain-page">
      {!embedded ? (
        <AdminPageHeader
          title={t('rksvHub.signatureChainPage.title')}
          breadcrumbs={[
            adminOverviewCrumb(t),
            { title: t(ADMIN_NAV_GROUP_LABEL_KEYS.rksv), href: '/rksv' },
            { title: t('rksvHub.signatureChainPage.breadcrumb') },
          ]}
        />
      ) : null}

      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('rksvHub.signatureChainPage.legalAlertTitle')}
        description={report?.legalNoticeDe ?? t('rksvHub.signatureChainPage.legalAlertFallback')}
      />

      <Card size="small" className={styles.noPrint} style={{ marginBottom: 16 }}>
        <Space wrap align="center">
          <Typography.Text strong>{t('rksvHub.signatureChainPage.selectRegister')}</Typography.Text>
          <Select
            showSearch
            placeholder={t('rksvHub.signatureChainPage.selectRegisterPlaceholder')}
            style={{ minWidth: 240 }}
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
          <Typography.Text strong>{t('rksvHub.signatureChainPage.filterRangeLabel')}</Typography.Text>
          <RangePicker
            value={range}
            onChange={(v) => setRange(v ?? [null, null])}
            allowClear={false}
            showTime
          />
          <Button
            type="primary"
            icon={<SearchOutlined />}
            loading={isLoading || isFetching}
            disabled={!cashRegisterId || !fromUtc || !toUtc}
            onClick={handleVerify}
          >
            {t('rksvHub.signatureChainPage.verify')}
          </Button>
          <Button
            icon={<DownloadOutlined />}
            disabled={!report || chainIssues.length === 0}
            onClick={handleExportCsv}
          >
            {t('rksvHub.signatureChainPage.exportCsv')}
          </Button>
          <Button icon={<PrinterOutlined />} disabled={!report} onClick={handlePrint}>
            {t('rksvHub.signatureChainPage.printReport')}
          </Button>
        </Space>
        {!cashRegisterId && (
          <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
            {t('rksvHub.signatureChainPage.registerRequired')}
          </Typography.Paragraph>
        )}
      </Card>

      {error && (
        <Alert
          type="error"
          showIcon
          className={styles.noPrint}
          style={{ marginBottom: 16 }}
          title={t('rksvHub.signatureChainPage.loadFailedTitle')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={error}
              logContext="RksvSignatureChain.verify"
              fallbackKey="rksvHub.signatureChainPage.loadFailedUnknown"
            />
          }
          action={
            <Button size="small" onClick={() => refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      )}

      {verifiedParams && report && (
        <div id="signature-chain-print-area">
          <div style={{ marginBottom: 16 }}>{outcomeAlert}</div>

          <Card size="small" style={{ marginBottom: 16 }} title={t('rksvHub.signatureChainPage.printMetaTitle')}>
            <Typography.Paragraph style={{ marginBottom: 4 }}>
              {t('rksvHub.signatureChainPage.printRegister')}:{' '}
              <Typography.Text code>{activeRegisterId}</Typography.Text>
            </Typography.Paragraph>
            <Typography.Paragraph style={{ marginBottom: 4 }}>
              {t('rksvHub.signatureChainPage.printRange')}:{' '}
              {verifiedParams.fromUtc && verifiedParams.toUtc
                ? `${dayjs(verifiedParams.fromUtc).format('DD.MM.YYYY HH:mm')} – ${dayjs(verifiedParams.toUtc).format('DD.MM.YYYY HH:mm')} UTC`
                : '—'}
            </Typography.Paragraph>
            {report.generatedAtUtc && (
              <Typography.Paragraph style={{ marginBottom: 0 }} type="secondary">
                {t('rksvHub.signatureChainPage.generatedUtc', {
                  ts: dayjs(report.generatedAtUtc).format('DD.MM.YYYY HH:mm:ss'),
                })}
              </Typography.Paragraph>
            )}
          </Card>

          <Card size="small" title={t('rksvHub.signatureChainPage.chainTableTitle')} style={{ marginBottom: 16 }}>
            <Table
              size="small"
              pagination={{ pageSize: 20, showSizeChanger: true }}
              rowKey={(r) => r.receiptId ?? r.receiptNumber ?? ''}
              dataSource={chain}
              columns={chainColumns}
              onRow={(row) => ({
                style: {
                  background:
                    row.receiptId && row.receiptId === highlightReceiptId
                      ? '#e6f4ff'
                      : row.status !== 'Pass'
                        ? '#fff2f0'
                        : undefined,
                },
              })}
            />
          </Card>

          {sequenceGaps.length > 0 && (
            <Card
              size="small"
              title={t('rksvHub.signatureChainPage.sequenceGapsTitle')}
              style={{ marginBottom: 16 }}
            >
              <Table
                size="small"
                pagination={{ pageSize: 10 }}
                rowKey={(r) =>
                  `${r.cashRegisterId}-${r.sequenceDateUtc}-${r.expectedSequence}-${r.previousReceiptNumber}`
                }
                dataSource={sequenceGaps}
                columns={[
                  {
                    title: t('rksvHub.signatureChainPage.colSequenceDate'),
                    dataIndex: 'sequenceDateUtc',
                    key: 'sequenceDateUtc',
                    render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY') : '—'),
                  },
                  {
                    title: t('rksvHub.signatureChainPage.colExpectedSeq'),
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
            </Card>
          )}

          {tseMissing.length > 0 && (
            <Card size="small" title={t('rksvHub.signatureChainPage.tseMissingTitle')}>
              <Table
                size="small"
                pagination={{ pageSize: 10 }}
                rowKey={(r) => r.paymentId ?? r.receiptNumber ?? ''}
                dataSource={tseMissing}
                columns={[
                  {
                    title: t('rksvHub.signatureChainPage.receiptNumber'),
                    dataIndex: 'receiptNumber',
                    key: 'receiptNumber',
                    render: (n: string, row) => (
                      <Space size={4}>
                        <Typography.Text code>{n}</Typography.Text>
                        {row.receiptId && (
                          <Link href={`/receipts/${row.receiptId}`}>
                            {t('rksvHub.signatureChainPage.viewReceipt')}
                          </Link>
                        )}
                      </Space>
                    ),
                  },
                  {
                    title: t('rksvHub.compliancePage.colIssuedUtc'),
                    dataIndex: 'issuedAtUtc',
                    key: 'issuedAtUtc',
                    render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY HH:mm:ss') : '—'),
                  },
                ]}
              />
            </Card>
          )}
        </div>
      )}

    </div>
  );
}
