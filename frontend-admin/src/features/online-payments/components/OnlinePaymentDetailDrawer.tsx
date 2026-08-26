'use client';

import { Descriptions, Drawer, Tag, Typography } from 'antd';

import type { AdminOnlinePaymentDto } from '@/features/online-payments/api/onlinePaymentsApi';
import { useI18n } from '@/i18n';
import { formatCurrency, formatDateTime } from '@/i18n/formatting';

const STATUS_COLORS: Record<string, string> = {
  PENDING: 'processing',
  AWAITING_PAYMENT_GATEWAY: 'processing',
  GATEWAY_SUCCEEDED: 'success',
  Succeeded: 'success',
  COMPLETED: 'success',
  FAILED: 'error',
  REFUNDED: 'purple',
  Created: 'blue',
  Pending: 'processing',
  Failed: 'error',
  Cancelled: 'default',
  Expired: 'warning',
  Refunded: 'purple',
};

type OnlinePaymentDetailDrawerProps = {
  payment: AdminOnlinePaymentDto | null;
  onClose: () => void;
};

export function OnlinePaymentDetailDrawer({ payment, onClose }: OnlinePaymentDetailDrawerProps) {
  const { t, formatLocale } = useI18n();
  const ts = (key: string) => t(`onlinePayments.${key}`);

  return (
    <Drawer
      title={ts('drawer.title')}
      open={payment !== null}
      onClose={onClose}
      destroyOnHidden
      width={480}
    >
      {payment ? (
        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label={ts('drawer.id')}>
            <Typography.Text copyable>{payment.id}</Typography.Text>
          </Descriptions.Item>
          <Descriptions.Item label={ts('drawer.tenant')}>
            {payment.tenantName || payment.tenantSlug || payment.tenantId}
          </Descriptions.Item>
          <Descriptions.Item label={ts('drawer.amount')}>
            {formatCurrency(payment.amount, formatLocale, { currency: payment.currency })}
          </Descriptions.Item>
          <Descriptions.Item label={ts('drawer.status')}>
            <Tag color={STATUS_COLORS[payment.status] ?? 'default'}>
              {t(`onlinePayments.status.${payment.status}`)}
            </Tag>
          </Descriptions.Item>
          <Descriptions.Item label={ts('drawer.method')}>{payment.paymentMethod}</Descriptions.Item>
          <Descriptions.Item label={ts('drawer.provider')}>{payment.provider || '—'}</Descriptions.Item>
          <Descriptions.Item label={ts('drawer.paymentIntentId')}>
            {payment.paymentIntentId ? (
              <Typography.Text copyable>{payment.paymentIntentId}</Typography.Text>
            ) : (
              '—'
            )}
          </Descriptions.Item>
          <Descriptions.Item label={ts('drawer.createdAt')}>
            {formatDateTime(payment.createdAtUtc, formatLocale)}
          </Descriptions.Item>
          <Descriptions.Item label={ts('drawer.completedAt')}>
            {payment.completedAtUtc ? formatDateTime(payment.completedAtUtc, formatLocale) : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={ts('drawer.webhookEvent')}>
            {payment.lastWebhookEvent ?? '—'}
          </Descriptions.Item>
          <Descriptions.Item label={ts('drawer.synthetic')}>
            {payment.isSynthetic ? ts('source.synthetic') : ts('source.live')}
          </Descriptions.Item>
          {payment.errorMessage ? (
            <Descriptions.Item label={ts('drawer.error')}>{payment.errorMessage}</Descriptions.Item>
          ) : null}
        </Descriptions>
      ) : null}
    </Drawer>
  );
}
