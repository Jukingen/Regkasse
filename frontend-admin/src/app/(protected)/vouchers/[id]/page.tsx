'use client';

import React, { useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Descriptions,
  Form,
  Input,
  Modal,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import {
  useAdminVoucherDetail,
  useAdminVoucherLedger,
  useCancelAdminVoucher,
  type AdminVoucherLedgerLineDto,
} from '@/api/admin/vouchers';
import { formatCurrency, formatDateTime } from '@/i18n/formatting';

function shortId(value?: string | null): string {
  if (!value) return '—';
  return value.length > 14 ? `${value.slice(0, 8)}…` : value;
}

function statusColor(status: string): string {
  switch (status) {
    case 'Active':
      return 'green';
    case 'PartiallyRedeemed':
      return 'blue';
    case 'Redeemed':
      return 'default';
    case 'Cancelled':
      return 'red';
    case 'Expired':
      return 'orange';
    default:
      return 'default';
  }
}

export default function AdminVoucherDetailPage() {
  const params = useParams();
  const id = typeof params?.id === 'string' ? params.id : '';
  const { t, formatLocale } = useI18n();
  const { hasPermission } = usePermissions();
  const canRead = hasPermission(PERMISSIONS.VOUCHER_READ);
  const canAudit = hasPermission(PERMISSIONS.VOUCHER_AUDIT_VIEW);
  const canCancel = hasPermission(PERMISSIONS.VOUCHER_CANCEL);

  const detailQuery = useAdminVoucherDetail(id, { enabled: canRead && !!id });
  const ledgerQuery = useAdminVoucherLedger(id, canRead && canAudit && !!id);
  const cancelMutation = useCancelAdminVoucher();
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelForm] = Form.useForm();

  const d = detailQuery.data;

  const statusLabel = (s: string) => {
    const key = `vouchers.status.${s}`;
    const lbl = t(key);
    return lbl === key ? s : lbl;
  };

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
          return k ? t(k) : type;
        },
      },
      {
        title: t('vouchers.ledger.amount'),
        dataIndex: 'amount',
        key: 'amount',
        render: (v: number) =>
          formatCurrency(v, formatLocale, { currency: d?.currency ?? 'EUR' }),
      },
      {
        title: t('vouchers.ledger.balanceAfter'),
        dataIndex: 'balanceAfter',
        key: 'balanceAfter',
        render: (v: number) =>
          formatCurrency(v, formatLocale, { currency: d?.currency ?? 'EUR' }),
      },
      {
        title: t('vouchers.ledger.paymentId'),
        dataIndex: 'paymentId',
        key: 'paymentId',
        render: (pid: string | null | undefined) => shortId(pid ?? undefined),
      },
      {
        title: t('vouchers.ledger.receiptNumber'),
        dataIndex: 'receiptNumber',
        key: 'receiptNumber',
        render: (x: string | null | undefined) => x ?? '—',
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
      },
    ],
    [t, formatLocale, d?.currency]
  );

  const showCancel =
    canCancel &&
    d &&
    d.remainingAmount > 0 &&
    d.status !== 'Cancelled' &&
    d.status !== 'Redeemed';

  const submitCancel = async () => {
    const values = await cancelForm.validateFields();
    await cancelMutation.mutateAsync({ id, reason: values.reason as string });
    void message.success(t('vouchers.cancel.success'));
    setCancelOpen(false);
    cancelForm.resetFields();
    await detailQuery.refetch();
    if (canAudit) await ledgerQuery.refetch();
  };

  if (!canRead) {
    return (
      <AdminPageShell>
        <Alert type="error" message={t('vouchers.list.permissionDenied')} showIcon />
      </AdminPageShell>
    );
  }

  if (!id) {
    return (
      <AdminPageShell>
        <Alert type="error" message={t('vouchers.errors.loadFailed')} showIcon />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('vouchers.detail.heading')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('vouchers.title'), href: '/vouchers' },
          { title: d?.maskedCode ?? shortId(id) },
        ]}
        actions={
          <Space>
            <Link href="/vouchers">
              <Button>{t('vouchers.detail.back')}</Button>
            </Link>
            {showCancel ? (
              <Button danger onClick={() => setCancelOpen(true)}>
                {t('vouchers.detail.cancel')}
              </Button>
            ) : null}
          </Space>
        }
      />

      {detailQuery.isLoading ? (
        <Spin />
      ) : detailQuery.isError || !d ? (
        <Alert type="error" message={t('vouchers.errors.loadFailed')} showIcon />
      ) : (
        <>
          <Card style={{ marginBottom: 16 }}>
            <Space style={{ marginBottom: 12 }}>
              <Typography.Text type="secondary">{d.maskedCode}</Typography.Text>
              <Tag color={statusColor(d.status)}>{statusLabel(d.status)}</Tag>
            </Space>
            <Descriptions bordered column={{ xs: 1, sm: 2, md: 2 }} size="small">
              <Descriptions.Item label={t('vouchers.list.columns.initialAmount')}>
                {formatCurrency(d.initialAmount, formatLocale, { currency: d.currency || 'EUR' })}
              </Descriptions.Item>
              <Descriptions.Item label={t('vouchers.list.columns.remainingAmount')}>
                {formatCurrency(d.remainingAmount, formatLocale, { currency: d.currency || 'EUR' })}
              </Descriptions.Item>
              <Descriptions.Item label={t('vouchers.list.columns.validFrom')}>
                {formatDateTime(d.validFromUtc, formatLocale)}
              </Descriptions.Item>
              <Descriptions.Item label={t('vouchers.list.columns.expiresAt')}>
                {formatDateTime(d.expiresAtUtc, formatLocale)}
              </Descriptions.Item>
              <Descriptions.Item label={t('vouchers.list.columns.createdBy')}>{d.createdByUserId}</Descriptions.Item>
              <Descriptions.Item label={t('vouchers.list.columns.createdAt')}>
                {formatDateTime(d.createdAtUtc, formatLocale)}
              </Descriptions.Item>
              {d.internalNote ? (
                <Descriptions.Item label={t('vouchers.detail.internalNote')} span={2}>
                  {d.internalNote}
                </Descriptions.Item>
              ) : null}
              {d.cancelledAtUtc ? (
                <Descriptions.Item label={t('vouchers.detail.cancelledAt')}>
                  {formatDateTime(d.cancelledAtUtc, formatLocale)}
                </Descriptions.Item>
              ) : null}
              {d.cancellationReason ? (
                <Descriptions.Item label={t('vouchers.detail.cancellationReason')} span={2}>
                  {d.cancellationReason}
                </Descriptions.Item>
              ) : null}
            </Descriptions>
          </Card>

          <Card title={t('vouchers.ledger.title')}>
            {!canAudit ? (
              <Alert type="info" message={t('vouchers.ledger.permissionDenied')} showIcon />
            ) : (
              <Table<AdminVoucherLedgerLineDto>
                rowKey="id"
                loading={ledgerQuery.isLoading}
                dataSource={ledgerQuery.data ?? []}
                columns={ledgerColumns}
                scroll={{ x: true }}
                locale={{ emptyText: t('vouchers.ledger.empty') }}
                pagination={false}
              />
            )}
          </Card>
        </>
      )}

      <Modal
        open={cancelOpen}
        title={t('vouchers.cancel.title')}
        onCancel={() => {
          setCancelOpen(false);
          cancelForm.resetFields();
        }}
        onOk={() =>
          submitCancel().catch(() => {
            void message.error(t('vouchers.errors.cancelFailed'));
            return Promise.reject();
          })
        }
        confirmLoading={cancelMutation.isPending}
        okText={t('vouchers.cancel.confirm')}
        okButtonProps={{ danger: true }}
      >
        <Form form={cancelForm} layout="vertical">
          <Form.Item
            name="reason"
            label={t('vouchers.cancel.reasonLabel')}
            rules={[{ required: true, min: 5, message: t('vouchers.cancel.reasonPlaceholder') }]}
          >
            <Input.TextArea rows={4} maxLength={500} showCount placeholder={t('vouchers.cancel.reasonPlaceholder')} />
          </Form.Item>
        </Form>
      </Modal>
    </AdminPageShell>
  );
}
