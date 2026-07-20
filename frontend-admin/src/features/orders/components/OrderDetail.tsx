'use client';

import { Button, Descriptions, Divider, Space, Timeline, Typography } from 'antd';
import type { OnlineOrder } from '@/features/orders/api/onlineOrdersApi';
import {
  getNextOnlineOrderStatus,
  useUpdateOnlineOrderStatus,
} from '@/features/orders/hooks/useOrders';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const { Text } = Typography;

const KNOWN_STATUSES = new Set([
  'pending',
  'accepted',
  'preparing',
  'ready',
  'completed',
  'cancelled',
]);

type OrderDetailProps = {
  order: OnlineOrder;
  onOrderUpdated?: (order: OnlineOrder) => void;
};

function orderTypeLabel(t: (key: string) => string, orderType: string): string {
  const known = ['dine-in', 'takeaway', 'delivery'] as const;
  if ((known as readonly string[]).includes(orderType)) {
    return t(`onlineOrders.orderTypes.${orderType}`);
  }
  return orderType;
}

function nextActionLabel(t: (key: string) => string, nextStatus: string): string {
  switch (nextStatus) {
    case 'accepted':
      return t('onlineOrders.actions.accept');
    case 'preparing':
      return t('onlineOrders.actions.prepare');
    case 'ready':
      return t('onlineOrders.actions.markReady');
    case 'completed':
      return t('onlineOrders.actions.complete');
    default:
      return t(`onlineOrders.status.${nextStatus}`);
  }
}

/**
 * Online-order detail — status lifecycle only (no POS cart / TSE / fiscal fields).
 */
export function OrderDetail({ order, onOrderUpdated }: OrderDetailProps) {
  const { t, formatLocale } = useI18n();
  const { message } = useAntdApp();
  const updateStatus = useUpdateOnlineOrderStatus();

  const money = (value: number) => {
    try {
      return new Intl.NumberFormat(formatLocale, { style: 'currency', currency: 'EUR' }).format(
        value,
      );
    } catch {
      return `€ ${value.toFixed(2)}`;
    }
  };

  const applyStatus = (status: string) => {
    updateStatus.mutate(
      { id: order.id, status },
      {
        onSuccess: (result) => {
          if (!result.succeeded) {
            message.error(result.error ?? t('common.errors.http500'));
            return;
          }
          message.success(t('onlineOrders.statusUpdateSuccess'));
          if (result.order) {
            onOrderUpdated?.(result.order);
          }
        },
        onError: (err) => openApiErrorMessage(message.open, t, err, { logContext: 'OrderDetail.updateStatus' }),
      },
    );
  };

  const history = order.statusHistory ?? [];
  const next = getNextOnlineOrderStatus(order.orderStatus);
  const canCancel =
    order.orderStatus !== 'completed' && order.orderStatus !== 'cancelled';

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Descriptions column={1} size="small" bordered>
        <Descriptions.Item label={t('onlineOrders.detail.customer')}>
          {order.customerName}
        </Descriptions.Item>
        <Descriptions.Item label={t('onlineOrders.detail.phone')}>
          {order.customerPhone}
        </Descriptions.Item>
        {order.customerEmail ? (
          <Descriptions.Item label={t('onlineOrders.detail.email')}>
            {order.customerEmail}
          </Descriptions.Item>
        ) : null}
        <Descriptions.Item label={t('onlineOrders.detail.orderType')}>
          {orderTypeLabel(t, order.orderType)}
        </Descriptions.Item>
        {order.tableNumber ? (
          <Descriptions.Item label={t('onlineOrders.detail.table')}>
            {order.tableNumber}
          </Descriptions.Item>
        ) : null}
        {order.deliveryAddress ? (
          <Descriptions.Item label={t('onlineOrders.detail.deliveryAddress')}>
            {order.deliveryAddress}
          </Descriptions.Item>
        ) : null}
        <Descriptions.Item label={t('onlineOrders.detail.payment')}>
          {order.paymentMethod} / {order.paymentStatus}
        </Descriptions.Item>
        <Descriptions.Item label={t('onlineOrders.detail.source')}>
          {order.source}
        </Descriptions.Item>
        <Descriptions.Item label={t('onlineOrders.detail.status')}>
          {KNOWN_STATUSES.has(order.orderStatus.toLowerCase())
            ? t(`onlineOrders.status.${order.orderStatus.toLowerCase()}`)
            : order.orderStatus}
        </Descriptions.Item>
        {order.notes ? (
          <Descriptions.Item label={t('onlineOrders.detail.notes')}>
            {order.notes}
          </Descriptions.Item>
        ) : null}
      </Descriptions>

      <Space wrap>
        {next ? (
          <Button
            type="primary"
            loading={
              updateStatus.isPending && updateStatus.variables?.status === next
            }
            onClick={() => applyStatus(next)}
          >
            {nextActionLabel(t, next)}
          </Button>
        ) : null}
        {canCancel ? (
          <Button
            danger
            loading={
              updateStatus.isPending &&
              updateStatus.variables?.status === 'cancelled'
            }
            onClick={() => applyStatus('cancelled')}
          >
            {t('onlineOrders.actions.cancel')}
          </Button>
        ) : null}
      </Space>

      {history.length > 0 ? (
        <>
          <Divider style={{ margin: 0 }}>{t('onlineOrders.detail.timeline')}</Divider>
          <Timeline
            items={history.map((h) => ({
              children: (
                <span>
                  {KNOWN_STATUSES.has(h.toStatus)
                    ? t(`onlineOrders.status.${h.toStatus}`)
                    : h.toStatus}{' '}
                  <Text type="secondary">
                    {h.changedAt
                      ? new Date(h.changedAt).toLocaleString(formatLocale)
                      : ''}
                  </Text>
                </span>
              ),
            }))}
          />
        </>
      ) : null}

      <Divider style={{ margin: 0 }}>{t('onlineOrders.detail.items')}</Divider>

      <Space orientation="vertical" size="small" style={{ width: '100%' }}>
        {order.items.map((item) => (
          <div key={item.id}>
            <Text>
              {item.quantity}× {item.productName} — {money(item.total)}
            </Text>
            {item.modifiers.length > 0 ? (
              <div>
                <Text type="secondary">
                  {item.modifiers
                    .map((m) =>
                      m.quantity > 1 ? `${m.name} ×${m.quantity}` : m.name,
                    )
                    .join(', ')}
                </Text>
              </div>
            ) : null}
          </div>
        ))}
      </Space>

      <Descriptions column={1} size="small">
        <Descriptions.Item label={t('onlineOrders.detail.subtotal')}>
          {money(order.subtotal)}
        </Descriptions.Item>
        <Descriptions.Item label={t('onlineOrders.detail.tax')}>
          {money(order.tax)}
        </Descriptions.Item>
        <Descriptions.Item label={t('onlineOrders.detail.total')}>
          <Text strong>{money(order.total)}</Text>
        </Descriptions.Item>
      </Descriptions>
    </Space>
  );
}
