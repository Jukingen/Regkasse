'use client';

import React, { useMemo, useState } from 'react';
import {
  Table,
  Card,
  Typography,
  Tag,
  Space,
  Button,
  DatePicker,
  Select,
  Drawer,
  Descriptions,
  Alert,
  Statistic,
  Row,
  Col,
  Input,
  InputNumber,
  Modal,
  message,
} from 'antd';
import { CreditCardOutlined, InfoCircleOutlined, ReloadOutlined } from '@ant-design/icons';
import {
  cancelLegacyPayment,
  refundLegacyPayment,
  useLegacyPaymentById,
  useLegacyPaymentList,
  useLegacyPaymentStatistics,
} from '@/api/legacy/payment';
import dayjs from 'dayjs';
import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { customInstance } from '@/lib/axios';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

const { Title } = Typography;
const { RangePicker } = DatePicker;

const DEFAULT_DATE_RANGE = { startDate: dayjs().subtract(30, 'day').format('YYYY-MM-DD'), endDate: dayjs().format('YYYY-MM-DD') };

type PaymentRow = {
  id?: string;
  transactionId?: string;
  createdAt?: string;
  amount?: number;
  method?: string;
  status?: string;
  currency?: string;
};

interface ReceiptByPaymentResponse {
  receiptId?: string;
}

interface PaymentStatisticsShape {
  totalPayments?: number;
  totalAmount?: number;
  averageAmount?: number;
  tseSignedPayments?: number;
  finanzOnlineSentPayments?: number;
}

export default function PaymentsPage() {
  const router = useRouter();
  const { hasPermission } = usePermissions();
  const canCancel = hasPermission(PERMISSIONS.PAYMENT_CANCEL);
  const canRefund = hasPermission(PERMISSIONS.REFUND_CREATE);
  const canOpenReceipt = hasPermission(PERMISSIONS.SALE_VIEW);

  const [dateRange, setDateRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
    dayjs(DEFAULT_DATE_RANGE.startDate),
    dayjs(DEFAULT_DATE_RANGE.endDate),
  ]);
  const [methodFilter, setMethodFilter] = useState<string | undefined>();
  const [statusFilter, setStatusFilter] = useState<string | undefined>();
  const [selectedPaymentId, setSelectedPaymentId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [refundReason, setRefundReason] = useState('');
  const [refundAmount, setRefundAmount] = useState<number | null>(null);

  const listParams = useMemo(
    () => ({
      startDate: dateRange[0].format('YYYY-MM-DD'),
      endDate: dateRange[1].format('YYYY-MM-DD'),
      pageSize: 500,
      pageNumber: 1,
    }),
    [dateRange]
  );

  const { data, isLoading, refetch } = useLegacyPaymentList(listParams);
  const { data: statsRaw, isLoading: statsLoading } = useLegacyPaymentStatistics({
    startDate: listParams.startDate,
    endDate: listParams.endDate,
  });
  const { data: paymentDetail, isLoading: detailLoading } = useLegacyPaymentById(selectedPaymentId ?? '', {
    query: { enabled: !!selectedPaymentId },
  });

  const payments: PaymentRow[] =
    data && typeof data === 'object' && 'items' in data && Array.isArray((data as { items?: unknown }).items)
      ? ((data as { items: PaymentRow[] }).items ?? [])
      : [];

  const stats: PaymentStatisticsShape | null =
    statsRaw && typeof statsRaw === 'object' ? (statsRaw as PaymentStatisticsShape) : null;

  const filteredPayments = useMemo(() => {
    return payments.filter((p) => {
      if (methodFilter && p.method !== methodFilter) return false;
      if (statusFilter && p.status !== statusFilter) return false;
      return true;
    });
  }, [payments, methodFilter, statusFilter]);

  const cancelMutation = useMutation({
    mutationFn: async () => {
      if (!selectedPaymentId) throw new Error('Kein Payment ausgewählt');
      return cancelLegacyPayment(selectedPaymentId, { reason: cancelReason.trim() });
    },
    onSuccess: async () => {
      message.success('Zahlung storniert');
      setCancelReason('');
      await refetch();
    },
    onError: (err: Error) => message.error(err?.message ?? 'Storno fehlgeschlagen'),
  });

  const refundMutation = useMutation({
    mutationFn: async () => {
      if (!selectedPaymentId) throw new Error('Kein Payment ausgewählt');
      if (!refundAmount || refundAmount <= 0) throw new Error('Refund-Betrag muss größer als 0 sein');
      return refundLegacyPayment(selectedPaymentId, { amount: refundAmount, reason: refundReason.trim() });
    },
    onSuccess: async () => {
      message.success('Rückerstattung verarbeitet');
      setRefundAmount(null);
      setRefundReason('');
      await refetch();
    },
    onError: (err: Error) => message.error(err?.message ?? 'Refund fehlgeschlagen'),
  });

  const openReceipt = async () => {
    if (!selectedPaymentId) return;
    try {
      const receipt = await customInstance<ReceiptByPaymentResponse>({
        url: `/api/Receipts/by-payment/${selectedPaymentId}`,
        method: 'GET',
      });
      if (!receipt?.receiptId) {
        message.warning('Kein Beleg für diese Zahlung gefunden');
        return;
      }
      router.push(`/receipts/${receipt.receiptId}`);
    } catch (err) {
      const e = err as Error;
      message.error(e?.message ?? 'Beleg konnte nicht geöffnet werden');
    }
  };

  const methodOptions = Array.from(new Set(payments.map((p) => p.method).filter(Boolean))) as string[];
  const statusOptions = Array.from(new Set(payments.map((p) => p.status).filter(Boolean))) as string[];

  const columns = [
    {
      title: 'Transaction ID',
      dataIndex: 'transactionId',
      key: 'transactionId',
      render: (text: string) => <code style={{ fontSize: '12px' }}>{text || '—'}</code>,
    },
    {
      title: 'Date',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => (date ? dayjs(date).format('DD.MM.YYYY HH:mm') : '—'),
    },
    {
      title: 'Amount',
      dataIndex: 'amount',
      key: 'amount',
      align: 'right' as const,
      render: (val: number, record: PaymentRow) => `${(val ?? 0).toFixed(2)} ${record.currency || 'EUR'}`,
    },
    {
      title: 'Method',
      dataIndex: 'method',
      key: 'method',
      render: (method: string) => <Tag color="blue">{method || '—'}</Tag>,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: string) => {
        const colors: Record<string, string> = {
          Success: 'green',
          Pending: 'orange',
          Failed: 'red',
          Cancelled: 'default',
          Refunded: 'purple',
        };
        return <Tag color={colors[status] || 'default'}>{status || '—'}</Tag>;
      },
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_: unknown, row: PaymentRow) => (
        <Button size="small" icon={<InfoCircleOutlined />} onClick={() => setSelectedPaymentId(row.id ?? null)}>
          Details
        </Button>
      ),
    },
  ];

  return (
    <Card>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <Title level={3} style={{ margin: 0 }}>
          Payments
        </Title>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => refetch()}>
            Aktualisieren
          </Button>
          <Button icon={<CreditCardOutlined />}>Terminal Status</Button>
        </Space>
      </div>

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space wrap>
          <RangePicker
            value={dateRange}
            onChange={(v) => {
              if (!v || !v[0] || !v[1]) return;
              setDateRange([v[0], v[1]]);
            }}
            format="DD.MM.YYYY"
            allowClear={false}
          />
          <Select
            placeholder="Methode"
            allowClear
            value={methodFilter}
            onChange={(v) => setMethodFilter(v)}
            style={{ width: 160 }}
            options={methodOptions.map((m) => ({ value: m, label: m }))}
          />
          <Select
            placeholder="Status"
            allowClear
            value={statusFilter}
            onChange={(v) => setStatusFilter(v)}
            style={{ width: 180 }}
            options={statusOptions.map((s) => ({ value: s, label: s }))}
          />
        </Space>
      </Card>

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic title="Anzahl Zahlungen" value={stats?.totalPayments ?? filteredPayments.length} loading={statsLoading} />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic title="Gesamtbetrag" value={stats?.totalAmount ?? 0} precision={2} suffix="EUR" loading={statsLoading} />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic title="Durchschnitt" value={stats?.averageAmount ?? 0} precision={2} suffix="EUR" loading={statsLoading} />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic title="TSE signiert" value={stats?.tseSignedPayments ?? 0} loading={statsLoading} />
          </Card>
        </Col>
      </Row>

      <Table columns={columns} dataSource={filteredPayments} loading={isLoading} rowKey="id" />

      <Drawer
        title="Payment Details"
        open={!!selectedPaymentId}
        onClose={() => setSelectedPaymentId(null)}
        width={560}
        destroyOnClose
      >
        {detailLoading ? (
          <Typography.Text type="secondary">Lade Details…</Typography.Text>
        ) : paymentDetail ? (
          <>
            <Descriptions size="small" column={1} bordered style={{ marginBottom: 16 }}>
              <Descriptions.Item label="Payment ID">
                <Typography.Text code copyable>
                  {selectedPaymentId}
                </Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label="Raw Detail">
                <Typography.Paragraph copyable style={{ marginBottom: 0 }}>
                  {JSON.stringify(paymentDetail)}
                </Typography.Paragraph>
              </Descriptions.Item>
            </Descriptions>

            <Space direction="vertical" style={{ width: '100%' }} size="middle">
              <Button onClick={openReceipt} disabled={!canOpenReceipt}>
                Beleg öffnen
              </Button>

              {!canCancel && (
                <Alert
                  type="info"
                  showIcon
                  message="Storno nicht erlaubt"
                  description="Für Storno ist die Berechtigung payment.cancel erforderlich."
                />
              )}
              {canCancel && (
                <Card size="small" title="Storno">
                  <Space direction="vertical" style={{ width: '100%' }}>
                    <Input
                      placeholder="Storno-Grund"
                      value={cancelReason}
                      onChange={(e) => setCancelReason(e.target.value)}
                    />
                    <Button
                      danger
                      loading={cancelMutation.isPending}
                      disabled={!cancelReason.trim()}
                      onClick={() =>
                        Modal.confirm({
                          title: 'Zahlung stornieren?',
                          content: 'Diese Aktion storniert die Zahlung. Fortfahren?',
                          okText: 'Stornieren',
                          okButtonProps: { danger: true },
                          cancelText: 'Abbrechen',
                          onOk: () => cancelMutation.mutate(),
                        })
                      }
                    >
                      Storno ausführen
                    </Button>
                  </Space>
                </Card>
              )}

              {!canRefund && (
                <Alert
                  type="info"
                  showIcon
                  message="Refund nicht erlaubt"
                  description="Für Refund ist die Berechtigung refund.create erforderlich."
                />
              )}
              {canRefund && (
                <Card size="small" title="Refund">
                  <Space direction="vertical" style={{ width: '100%' }}>
                    <InputNumber
                      min={0.01}
                      precision={2}
                      placeholder="Betrag"
                      value={refundAmount ?? undefined}
                      onChange={(v) => setRefundAmount(typeof v === 'number' ? v : null)}
                      style={{ width: '100%' }}
                    />
                    <Input
                      placeholder="Refund-Grund"
                      value={refundReason}
                      onChange={(e) => setRefundReason(e.target.value)}
                    />
                    <Button
                      loading={refundMutation.isPending}
                      disabled={!refundReason.trim() || !refundAmount || refundAmount <= 0}
                      onClick={() =>
                        Modal.confirm({
                          title: 'Refund ausführen?',
                          content: 'Diese Aktion erstellt eine Rückerstattung für die gewählte Zahlung. Fortfahren?',
                          okText: 'Refund ausführen',
                          cancelText: 'Abbrechen',
                          onOk: () => refundMutation.mutate(),
                        })
                      }
                    >
                      Refund ausführen
                    </Button>
                  </Space>
                </Card>
              )}
            </Space>
          </>
        ) : (
          <Alert type="warning" showIcon message="Keine Detaildaten verfügbar" />
        )}
      </Drawer>
    </Card>
  );
}
