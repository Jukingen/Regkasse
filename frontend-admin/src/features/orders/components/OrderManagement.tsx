'use client';

import { ShoppingOutlined } from '@ant-design/icons';
import { Badge, Button, Card, Col, Modal, Row, Space, Statistic, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect, useState } from 'react';

import { VirtualTable } from '@/components/VirtualTable';
import { adminTablePaginationDefaults } from '@/components/ui/adminTablePagination';
import type { OnlineOrder } from '@/features/orders/api/onlineOrdersApi';
import { OrderDetail } from '@/features/orders/components/OrderDetail';
import {
  getNextOnlineOrderStatus,
  useOnlineOrderAnalytics,
  useOnlineOrderDetail,
  useOrders,
  useUpdateOnlineOrderStatus,
} from '@/features/orders/hooks/useOrders';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const STATUS_FILTER_BADGES = ['pending', 'accepted', 'preparing', 'ready', 'completed'] as const;

const KNOWN_STATUSES = new Set([
  'pending',
  'accepted',
  'preparing',
  'ready',
  'completed',
  'cancelled',
]);

function statusColor(status: string): string {
  switch (status.toLowerCase()) {
    case 'pending':
      return 'orange';
    case 'accepted':
      return 'blue';
    case 'preparing':
      return 'cyan';
    case 'ready':
      return 'green';
    case 'completed':
      return 'green';
    case 'cancelled':
      return 'red';
    default:
      return 'default';
  }
}

/**
 * Manager FA inbox for website/app online orders only.
 * Status lifecycle only — never calls POS accept/push, TSE, or fiscal APIs.
 */
export function OrderManagement() {
  const { t, formatLocale } = useI18n();
  const { message } = useAntdApp();
  const [statusFilter, setStatusFilter] = useState<string | undefined>(undefined);
  const { data, isLoading, isError } = useOrders(statusFilter);
  const { data: analytics } = useOnlineOrderAnalytics();
  const updateStatus = useUpdateOnlineOrderStatus();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const detailQuery = useOnlineOrderDetail(selectedId);
  const [selectedOrder, setSelectedOrder] = useState<OnlineOrder | null>(null);

  useEffect(() => {
    if (detailQuery.data) {
      setSelectedOrder(detailQuery.data);
    }
  }, [detailQuery.data]);

  const orders = data?.orders ?? [];
  const badgeCounts: Record<(typeof STATUS_FILTER_BADGES)[number], number> = {
    pending: data?.pending ?? 0,
    accepted: data?.accepted ?? 0,
    preparing: data?.preparing ?? 0,
    ready: data?.ready ?? 0,
    completed: data?.completed ?? 0,
  };

  const money = (value: number) => {
    try {
      return new Intl.NumberFormat(formatLocale, { style: 'currency', currency: 'EUR' }).format(
        value
      );
    } catch {
      return `€ ${value.toFixed(2)}`;
    }
  };

  const statusLabel = (status: string) => {
    const normalized = status.toLowerCase();
    if (KNOWN_STATUSES.has(normalized)) {
      return t(`onlineOrders.status.${normalized}`);
    }
    return status;
  };

  const nextActionLabel = (nextStatus: string) => {
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
        return statusLabel(nextStatus);
    }
  };

  const handleAdvance = (id: string, nextStatus: string) => {
    updateStatus.mutate(
      { id, status: nextStatus },
      {
        onSuccess: (result) => {
          if (!result.succeeded) {
            message.error(result.error ?? t('common.errors.http500'));
            return;
          }
          message.success(t('onlineOrders.statusUpdateSuccess'));
          if (result.order) {
            setSelectedId(result.order.id);
            setSelectedOrder(result.order);
          }
        },
        onError: (err) =>
          openApiErrorMessage(message.open, t, err, { logContext: 'OrderManagement.updateStatus' }),
      }
    );
  };

  const columns: ColumnsType<OnlineOrder> = [
    {
      title: t('onlineOrders.columns.orderNumber'),
      dataIndex: 'orderNumber',
      key: 'orderNumber',
    },
    {
      title: t('onlineOrders.columns.customer'),
      dataIndex: 'customerName',
      key: 'customerName',
    },
    {
      title: t('onlineOrders.columns.total'),
      dataIndex: 'total',
      key: 'total',
      render: (value: number) => money(value),
    },
    {
      title: t('onlineOrders.columns.status'),
      dataIndex: 'orderStatus',
      key: 'orderStatus',
      render: (status: string) => <Tag color={statusColor(status)}>{statusLabel(status)}</Tag>,
    },
    {
      title: t('onlineOrders.columns.createdAt'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => (date ? new Date(date).toLocaleString(formatLocale) : '—'),
    },
    {
      title: t('onlineOrders.columns.actions'),
      key: 'action',
      render: (_, record) => {
        const next = getNextOnlineOrderStatus(record.orderStatus);
        return (
          <Space wrap>
            <Button
              size="small"
              onClick={() => {
                setSelectedId(record.id);
                setSelectedOrder(record);
              }}
            >
              {t('onlineOrders.actions.detail')}
            </Button>
            {next ? (
              <Button
                size="small"
                type="primary"
                loading={
                  updateStatus.isPending &&
                  updateStatus.variables?.id === record.id &&
                  updateStatus.variables.status === next
                }
                onClick={() => handleAdvance(record.id, next)}
              >
                {nextActionLabel(next)}
              </Button>
            ) : null}
          </Space>
        );
      },
    },
  ];

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      {analytics ? (
        <Row gutter={[16, 16]}>
          <Col xs={12} md={6}>
            <Card size="small">
              <Statistic
                title={t('onlineOrders.analytics.totalOrders')}
                value={analytics.totalOrders}
              />
            </Card>
          </Col>
          <Col xs={12} md={6}>
            <Card size="small">
              <Statistic title={t('onlineOrders.analytics.pending')} value={analytics.pending} />
            </Card>
          </Col>
          <Col xs={12} md={6}>
            <Card size="small">
              <Statistic
                title={t('onlineOrders.analytics.completed')}
                value={analytics.completed}
              />
            </Card>
          </Col>
          <Col xs={12} md={6}>
            <Card size="small">
              <Statistic
                title={t('onlineOrders.analytics.revenue')}
                value={money(analytics.revenue)}
              />
            </Card>
          </Col>
        </Row>
      ) : null}

      <Card
        title={
          <Space>
            <ShoppingOutlined />
            {t('onlineOrders.cardTitle')}
          </Space>
        }
        extra={
          <Space wrap>
            {STATUS_FILTER_BADGES.map((status) => (
              <Badge key={status} count={badgeCounts[status] ?? 0} overflowCount={99}>
                <Button
                  type={statusFilter === status ? 'primary' : 'default'}
                  onClick={() => setStatusFilter((prev) => (prev === status ? undefined : status))}
                >
                  {t(`onlineOrders.status.${status}`)}
                </Button>
              </Badge>
            ))}
          </Space>
        }
      >
        {isError ? <Tag color="error">{t('common.errors.http500')}</Tag> : null}
        <VirtualTable<OnlineOrder>
          dataSource={orders}
          columns={columns}
          loading={isLoading}
          rowKey="id"
          pagination={{ ...adminTablePaginationDefaults }}
          scroll={{ x: 1100 }}
          locale={{ emptyText: t('onlineOrders.empty') }}
        />
      </Card>

      <Modal
        title={
          selectedOrder
            ? t('onlineOrders.detailTitle', { number: selectedOrder.orderNumber })
            : t('onlineOrders.detailTitleFallback')
        }
        open={!!selectedId}
        onCancel={() => {
          setSelectedId(null);
          setSelectedOrder(null);
        }}
        footer={null}
        width={640}
        destroyOnHidden
      >
        {selectedOrder ? (
          <OrderDetail order={selectedOrder} onOrderUpdated={(order) => setSelectedOrder(order)} />
        ) : null}
      </Modal>
    </Space>
  );
}
