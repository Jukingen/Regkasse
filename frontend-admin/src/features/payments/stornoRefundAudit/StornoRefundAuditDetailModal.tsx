'use client';

import { Alert, Descriptions, Modal, Table, Typography } from 'antd';
import React, { useMemo } from 'react';

import { useGetApiAdminPaymentsId } from '@/api/generated/admin/admin';
import type { AdminPaymentDetailDto } from '@/api/generated/model';
import { StornoReason } from '@/api/generated/model/stornoReason';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY, createIntlFormatters } from '@/i18n/formatting';

const { Text } = Typography;

function stornoReasonLabelKey(reason: StornoReason | undefined): string | null {
  if (reason === undefined) return null;
  switch (reason) {
    case StornoReason.NUMBER_0:
      return 'payments.stornoRefundAudit.stornoReason.falscherBetrag';
    case StornoReason.NUMBER_1:
      return 'payments.stornoRefundAudit.stornoReason.kundeStorniert';
    case StornoReason.NUMBER_2:
      return 'payments.stornoRefundAudit.stornoReason.technischerFehler';
    case StornoReason.NUMBER_3:
      return 'payments.stornoRefundAudit.stornoReason.anderes';
    default:
      return null;
  }
}

function formatDurationSeconds(
  seconds: number | null | undefined,
  t: (k: string, o?: Record<string, string | number>) => string
): string {
  if (seconds == null || !Number.isFinite(seconds)) return FORMAT_EMPTY_DISPLAY;
  const s = Math.abs(seconds);
  const hrs = Math.floor(s / 3600);
  const mins = Math.floor((s % 3600) / 60);
  const secs = Math.floor(s % 60);
  const parts: string[] = [];
  if (hrs > 0) parts.push(t('payments.stornoRefundAudit.detail.durationHours', { count: hrs }));
  if (mins > 0) parts.push(t('payments.stornoRefundAudit.detail.durationMinutes', { count: mins }));
  if (secs > 0 || parts.length === 0)
    parts.push(t('payments.stornoRefundAudit.detail.durationSeconds', { count: secs }));
  return parts.join(' ');
}

export type StornoRefundAuditDetailModalProps = {
  paymentId: string | null;
  open: boolean;
  onClose: () => void;
};

export function StornoRefundAuditDetailModal({
  paymentId,
  open,
  onClose,
}: StornoRefundAuditDetailModalProps) {
  const { t, formatLocale } = useI18n();
  const fmt = useMemo(() => createIntlFormatters(formatLocale), [formatLocale]);

  const { data, isLoading } = useGetApiAdminPaymentsId(paymentId ?? '', {
    query: {
      enabled: open && Boolean(paymentId),
    },
  });

  const detail = data as AdminPaymentDetailDto | undefined;
  const audit = detail?.stornoRefundAudit;

  const lineCols = useMemo(
    () => [
      {
        title: t('payments.stornoRefundAudit.detail.colProduct'),
        dataIndex: 'productName' as const,
        key: 'productName',
      },
      {
        title: t('payments.stornoRefundAudit.detail.colQty'),
        dataIndex: 'quantity' as const,
        key: 'quantity',
        width: 72,
      },
      {
        title: t('payments.stornoRefundAudit.detail.colUnit'),
        dataIndex: 'unitPrice' as const,
        key: 'unitPrice',
        render: (v: number) => fmt.formatCurrency(v),
      },
      {
        title: t('payments.stornoRefundAudit.detail.colLineTotal'),
        dataIndex: 'totalPrice' as const,
        key: 'totalPrice',
        render: (v: number) => fmt.formatCurrency(v),
      },
    ],
    [fmt, t]
  );

  const auditEventCols = useMemo(
    () => [
      {
        title: t('payments.stornoRefundAudit.detail.colTime'),
        dataIndex: 'timestampUtc' as const,
        key: 'ts',
        render: (iso: string) => fmt.formatDateTime(iso),
        width: 180,
      },
      {
        title: t('payments.stornoRefundAudit.detail.colAction'),
        dataIndex: 'action' as const,
        key: 'action',
      },
      {
        title: t('payments.stornoRefundAudit.detail.colRole'),
        dataIndex: 'userRole' as const,
        key: 'userRole',
        width: 110,
      },
      {
        title: t('payments.stornoRefundAudit.detail.colDescription'),
        dataIndex: 'description' as const,
        key: 'description',
        ellipsis: true,
      },
    ],
    [fmt, t]
  );

  const srKey = stornoReasonLabelKey(detail?.stornoReason);
  const stornoReasonText = srKey ? t(srKey) : FORMAT_EMPTY_DISPLAY;

  return (
    <Modal
      title={t('payments.stornoRefundAudit.detail.title')}
      open={open}
      onCancel={onClose}
      footer={null}
      width={960}
      destroyOnHidden
    >
      {isLoading ? (
        <Text type="secondary">{t('payments.stornoRefundAudit.detail.loading')}</Text>
      ) : !detail ? (
        <Alert type="warning" title={t('payments.stornoRefundAudit.detail.empty')} showIcon />
      ) : (
        <>
          <Descriptions bordered size="small" column={2} style={{ marginBottom: 16 }}>
            <Descriptions.Item label={t('payments.stornoRefundAudit.table.colType')}>
              {detail.isStorno
                ? t('payments.stornoRefundAudit.type.storno')
                : detail.isRefund
                  ? t('payments.stornoRefundAudit.type.refund')
                  : FORMAT_EMPTY_DISPLAY}
            </Descriptions.Item>
            <Descriptions.Item label={t('payments.stornoRefundAudit.detail.labelCashier')}>
              {detail.cashierDisplayName ?? detail.cashierId ?? FORMAT_EMPTY_DISPLAY}
            </Descriptions.Item>
            <Descriptions.Item label={t('payments.stornoRefundAudit.table.colNewReceipt')}>
              {detail.receiptNumber ?? FORMAT_EMPTY_DISPLAY}
            </Descriptions.Item>
            <Descriptions.Item label={t('payments.stornoRefundAudit.detail.labelReversalAmount')}>
              {fmt.formatCurrency(detail.totalAmount ?? 0)}
            </Descriptions.Item>
            {detail.isStorno ? (
              <Descriptions.Item label={t('payments.stornoRefundAudit.table.colReason')}>
                {stornoReasonText}
              </Descriptions.Item>
            ) : null}
            {detail.isRefund && detail.refundReason ? (
              <Descriptions.Item label={t('payments.stornoRefundAudit.detail.labelRefundReason')}>
                {detail.refundReason}
              </Descriptions.Item>
            ) : null}
          </Descriptions>

          {audit?.secondsBetweenOriginalAndReversal != null ? (
            <Alert
              style={{ marginBottom: 16 }}
              type="info"
              showIcon
              title={t('payments.stornoRefundAudit.detail.timeDeltaTitle')}
              description={formatDurationSeconds(audit.secondsBetweenOriginalAndReversal, t)}
            />
          ) : null}

          <Typography.Title level={5}>
            {t('payments.stornoRefundAudit.detail.sectionOriginal')}
          </Typography.Title>
          <Descriptions size="small" column={2} style={{ marginBottom: 8 }}>
            <Descriptions.Item label={t('payments.stornoRefundAudit.table.colOriginalReceipt')}>
              {audit?.originalReceiptNumber ?? FORMAT_EMPTY_DISPLAY}
            </Descriptions.Item>
            <Descriptions.Item label={t('payments.stornoRefundAudit.detail.labelOriginalWhen')}>
              {audit?.originalCreatedAtUtc
                ? fmt.formatDateTime(audit.originalCreatedAtUtc)
                : FORMAT_EMPTY_DISPLAY}
            </Descriptions.Item>
            <Descriptions.Item label={t('payments.stornoRefundAudit.detail.labelOriginalTotal')}>
              {audit?.originalTotalAmount != null
                ? fmt.formatCurrency(audit.originalTotalAmount)
                : FORMAT_EMPTY_DISPLAY}
            </Descriptions.Item>
          </Descriptions>
          <Table
            size="small"
            pagination={false}
            rowKey={(_, i) => `o-${i}`}
            columns={lineCols}
            dataSource={audit?.originalLineItems ?? []}
            locale={{ emptyText: t('payments.stornoRefundAudit.detail.noLines') }}
            style={{ marginBottom: 24 }}
          />

          <Typography.Title level={5}>
            {t('payments.stornoRefundAudit.detail.sectionReversal')}
          </Typography.Title>
          <Table
            size="small"
            pagination={false}
            rowKey={(_, i) => `r-${i}`}
            columns={lineCols}
            dataSource={audit?.reversalLineItems ?? []}
            locale={{ emptyText: t('payments.stornoRefundAudit.detail.noLines') }}
            style={{ marginBottom: 24 }}
          />

          <Typography.Title level={5}>
            {t('payments.stornoRefundAudit.detail.sectionAuditTrail')}
          </Typography.Title>
          <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
            {t('payments.stornoRefundAudit.detail.auditTrailHint')}
          </Text>
          <Table
            size="small"
            pagination={{ pageSize: 10 }}
            rowKey={(_, i) => `a-${i}`}
            columns={auditEventCols}
            dataSource={audit?.relatedAuditEvents ?? []}
            locale={{ emptyText: t('payments.stornoRefundAudit.detail.noAuditEvents') }}
          />
        </>
      )}
    </Modal>
  );
}
