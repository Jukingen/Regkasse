'use client';

import { LinkOutlined } from '@ant-design/icons';
import { Alert, Modal, Space, Spin, Table, Tag, Timeline, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import React, { useMemo } from 'react';

import {
  type AdminVoucherLedgerLineDto,
  useAdminVoucherDetail,
  useAdminVoucherLedger,
} from '@/api/admin/vouchers';
import { useI18n } from '@/i18n';
import { formatCurrency, formatDateTime } from '@/i18n/formatting';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

export type VoucherHistoryInlineProps = {
  voucherId: string;
  /** Requires `voucher.audit.view` — when false, query is disabled and an info alert is shown. */
  ledgerEnabled: boolean;
  currency: string;
};

export type VoucherHistoryModalModeProps = {
  voucherId: string;
  visible: boolean;
  onClose: () => void;
};

export type VoucherHistoryProps = VoucherHistoryInlineProps | VoucherHistoryModalModeProps;

function isModalMode(props: VoucherHistoryProps): props is VoucherHistoryModalModeProps {
  return 'visible' in props && 'onClose' in props;
}

function shortId(value?: string | null): string {
  if (!value) return '—';
  return value.length > 14 ? `${value.slice(0, 8)}…` : value;
}

function creatorFallback(userId?: string | null): string {
  if (!userId) return '—';
  const trimmed = userId.trim();
  if (!trimmed) return '—';
  return trimmed.length > 14 ? `${trimmed.slice(0, 8)}…` : trimmed;
}

function formatCreatorLabel(input: {
  createdByUserId?: string | null;
  createdByDisplayName?: string | null;
  createdByEmail?: string | null;
  createdByRoles?: string[] | null;
}): string {
  const parts = [input.createdByDisplayName?.trim(), input.createdByEmail?.trim()].filter(
    (x): x is string => !!x
  );
  const roleText = (input.createdByRoles ?? []).filter(Boolean).join(', ');
  if (parts.length > 0 && roleText) return `${parts.join(' · ')} (${roleText})`;
  if (parts.length > 0) return parts.join(' · ');
  if (roleText) return `${creatorFallback(input.createdByUserId)} (${roleText})`;
  return creatorFallback(input.createdByUserId);
}

function ledgerTypeTagColor(type: string): string {
  switch (type) {
    case 'Issue':
      return 'green';
    case 'Redeem':
      return 'blue';
    case 'Cancel':
      return 'red';
    case 'Refund':
      return 'orange';
    case 'Expire':
      return 'default';
    default:
      return 'default';
  }
}

/**
 * Gutschein-Saldo-Verlauf: Buchungsjournal mit Tabelle und chronologischer Timeline.
 */
function VoucherHistoryLedgerBody({
  voucherId,
  ledgerEnabled,
  currency,
}: VoucherHistoryInlineProps) {
  const { t, formatLocale } = useI18n();
  const ledgerQuery = useAdminVoucherLedger(voucherId, ledgerEnabled && !!voucherId);

  const ledgerColumns: ColumnsType<AdminVoucherLedgerLineDto> = useMemo(
    () => [
      {
        title: t('vouchers.ledger.type'),
        dataIndex: 'type',
        key: 'type',
        render: (type: string) => {
          const map: Record<string, string> = {
            Issue: 'vouchers.ledger.typeIssue',
            Redeem: 'vouchers.ledger.typeRedeem',
            Refund: 'vouchers.ledger.typeRefund',
            Cancel: 'vouchers.ledger.typeCancel',
            Expire: 'vouchers.ledger.typeExpire',
          };
          const k = map[type];
          const label = k ? t(k) : type;
          return <Tag color={ledgerTypeTagColor(type)}>{label}</Tag>;
        },
      },
      {
        title: t('vouchers.ledger.amount'),
        dataIndex: 'amount',
        key: 'amount',
        render: (v: number) => formatCurrency(v, formatLocale, { currency }),
      },
      {
        title: t('vouchers.ledger.balanceAfter'),
        dataIndex: 'balanceAfter',
        key: 'balanceAfter',
        render: (v: number) => formatCurrency(v, formatLocale, { currency }),
      },
      {
        title: t('vouchers.ledger.paymentId'),
        dataIndex: 'paymentId',
        key: 'paymentId',
        render: (pid: string | null | undefined) => {
          const full = typeof pid === 'string' ? pid.trim() : '';
          if (!full) return '—';
          return (
            <Space size="small" align="center" wrap>
              <Typography.Text copyable={{ text: full }}>{shortId(full)}</Typography.Text>
              <Link
                href={`/payments?paymentId=${encodeURIComponent(full)}`}
                title={t('vouchers.ledger.linkOpenPayments')}
                aria-label={t('vouchers.ledger.linkOpenPayments')}
              >
                <LinkOutlined />
              </Link>
            </Space>
          );
        },
      },
      {
        title: t('vouchers.ledger.receiptNumber'),
        dataIndex: 'receiptNumber',
        key: 'receiptNumber',
        render: (x: string | null | undefined) => {
          const rn = typeof x === 'string' ? x.trim() : '';
          if (!rn) return '—';
          return (
            <Link
              href={`/receipts?receiptNumber=${encodeURIComponent(rn)}`}
              title={t('vouchers.ledger.linkOpenReceipts')}
            >
              {rn}
            </Link>
          );
        },
      },
      {
        title: t('vouchers.ledger.createdAt'),
        dataIndex: 'createdAtUtc',
        key: 'createdAtUtc',
        render: (iso: string) => formatDateTime(iso, formatLocale),
      },
      {
        title: t('vouchers.ledger.createdBy'),
        dataIndex: 'createdByUserId',
        key: 'createdByUserId',
        ellipsis: true,
        render: (_: string, row) => formatCreatorLabel(row),
      },
    ],
    [t, formatLocale, currency]
  );

  const timelineItems = useMemo(() => {
    const rows = ledgerQuery.data ?? [];
    const sorted = [...rows].sort(
      (a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime()
    );
    return sorted.map((entry) => {
      const type = entry.type ?? '';
      const map: Record<string, string> = {
        Issue: 'vouchers.ledger.typeIssue',
        Redeem: 'vouchers.ledger.typeRedeem',
        Refund: 'vouchers.ledger.typeRefund',
        Cancel: 'vouchers.ledger.typeCancel',
        Expire: 'vouchers.ledger.typeExpire',
      };
      const k = map[type];
      const typeLabel = k ? t(k) : type;
      const amount = entry.amount ?? 0;
      const balanceAfter = entry.balanceAfter ?? 0;
      return {
        color: type === 'Redeem' ? 'blue' : type === 'Cancel' ? 'red' : 'green',
        children: (
          <>
            <strong>{formatDateTime(entry.createdAtUtc, formatLocale)}</strong>
            <div>
              {typeLabel}: {formatCurrency(amount, formatLocale, { currency })}
            </div>
            <small>
              {t('vouchers.ledger.balanceAfter')}:{' '}
              {formatCurrency(balanceAfter, formatLocale, { currency })}
            </small>
          </>
        ),
      };
    });
  }, [ledgerQuery.data, t, formatLocale, currency]);

  if (!ledgerEnabled) {
    return <Alert type="info" title={t('vouchers.ledger.permissionDenied')} showIcon />;
  }

  return (
    <>
      <Table<AdminVoucherLedgerLineDto>
        rowKey="id"
        loading={ledgerQuery.isLoading}
        dataSource={ledgerQuery.data ?? []}
        columns={ledgerColumns}
        scroll={{ x: true }}
        locale={{ emptyText: t('vouchers.ledger.empty') }}
        pagination={false}
      />

      {(ledgerQuery.data?.length ?? 0) > 0 ? (
        <Timeline items={timelineItems} style={{ marginTop: 24 }} />
      ) : null}
    </>
  );
}

function VoucherHistoryModalShell({ voucherId, visible, onClose }: VoucherHistoryModalModeProps) {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const ledgerEnabled = hasPermission(PERMISSIONS.VOUCHER_AUDIT_VIEW);
  const detailQuery = useAdminVoucherDetail(voucherId, { enabled: visible && !!voucherId });

  return (
    <Modal
      title={t('vouchers.ledger.title')}
      open={visible}
      onCancel={onClose}
      footer={null}
      width={800}
      destroyOnHidden
    >
      <Spin spinning={detailQuery.isLoading}>
        <VoucherHistoryLedgerBody
          voucherId={voucherId}
          ledgerEnabled={ledgerEnabled}
          currency={detailQuery.data?.currency ?? 'EUR'}
        />
      </Spin>
    </Modal>
  );
}

/**
 * Inline ledger on the voucher detail page, or controlled modal from the list (`visible` / `onClose`).
 */
export function VoucherHistory(props: VoucherHistoryProps) {
  if (isModalMode(props)) {
    return <VoucherHistoryModalShell {...props} />;
  }
  return <VoucherHistoryLedgerBody {...props} />;
}

export type VoucherHistoryModalProps = VoucherHistoryModalModeProps;

/** Same as {@link VoucherHistory} in modal mode (`visible` / `onClose`). */
export function VoucherHistoryModal(props: VoucherHistoryModalProps) {
  return <VoucherHistory {...props} />;
}
