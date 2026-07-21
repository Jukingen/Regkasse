'use client';

import { ArrowLeftOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Descriptions, Space, Typography } from 'antd';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';

import { useGetApiAdminPaymentsId } from '@/api/generated/admin/admin';
import type { AdminPaymentDetailDto } from '@/api/generated/model';
import { CardSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ReprintButton } from '@/features/payments/components/ReprintButton';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime } from '@/i18n/formatting';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';

function isNotFoundError(error: unknown): boolean {
  const status =
    (error as { response?: { status?: number } })?.response?.status ??
    (error as { normalized?: { status?: number } })?.normalized?.status;
  return status === 404;
}

export default function AdminPaymentStandaloneDetailPage() {
  const { id } = useParams<{ id: string }>();
  const paymentId = typeof id === 'string' ? id.trim() : '';
  const router = useRouter();
  const { t, formatLocale } = useI18n();

  const { data, isLoading, isError, error, refetch } = useGetApiAdminPaymentsId(paymentId, {
    query: { enabled: paymentId.length > 0 },
  });
  const payment = data as AdminPaymentDetailDto | undefined;

  const handleBack = () => {
    if (typeof window !== 'undefined' && window.history.length > 1) {
      router.back();
    } else {
      router.push('/payments');
    }
  };

  if (!paymentId) {
    return (
      <Alert
        type="warning"
        showIcon
        title={t('payments.standaloneDetail.notFound')}
        action={
          <Button type="primary" onClick={() => router.push('/payments')}>
            {t('payments.standaloneDetail.backToPayments')}
          </Button>
        }
      />
    );
  }

  if (isLoading) {
    return <CardSkeleton count={2} />;
  }

  if (isError && isNotFoundError(error)) {
    return (
      <Alert
        type="warning"
        showIcon
        title={t('payments.standaloneDetail.notFound')}
        action={
          <Button type="primary" onClick={() => router.push('/payments')}>
            {t('payments.standaloneDetail.backToPayments')}
          </Button>
        }
      />
    );
  }

  if (isError) {
    return (
      <Alert
        type="error"
        showIcon
        title={t('payments.standaloneDetail.loadFailed')}
        action={
          <Space>
            <Button onClick={() => void refetch()}>{t('payments.toolbar.retryAfterError')}</Button>
            <Button onClick={handleBack}>{t('payments.standaloneDetail.backToPayments')}</Button>
          </Space>
        }
      />
    );
  }

  if (!payment?.id) {
    return (
      <Alert
        type="warning"
        showIcon
        title={t('payments.standaloneDetail.notFound')}
        action={
          <Button onClick={handleBack}>{t('payments.standaloneDetail.backToPayments')}</Button>
        }
      />
    );
  }

  const currency = payment.currency || 'EUR';
  const crumbTitle = payment.transactionId?.trim() || payment.id;

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <AdminPageHeader
        title={t('payments.standaloneDetail.pageTitle')}
        breadcrumbs={[
          ADMIN_OVERVIEW_CRUMB,
          { title: ADMIN_NAV_LABELS.payments, href: '/payments' },
          { title: crumbTitle },
        ]}
      />

      <div>
        <Button type="text" icon={<ArrowLeftOutlined />} onClick={handleBack}>
          {t('payments.standaloneDetail.backToPayments')}
        </Button>
      </div>

      <Card>
        <Space wrap style={{ marginBottom: 16 }}>
          <ReprintButton paymentId={payment.id} receiptNumber={payment.receiptNumber} />
          {payment.receiptId ? (
            <Link href={`/receipts/${payment.receiptId}`}>
              <Button type="default">{t('payments.detail.buttonOpenReceipt')}</Button>
            </Link>
          ) : null}
        </Space>

        <Descriptions bordered size="small" column={1}>
          <Descriptions.Item label={t('payments.detail.labelPaymentId')}>
            <Typography.Text code copyable>
              {payment.id}
            </Typography.Text>
          </Descriptions.Item>
          <Descriptions.Item label={t('payments.detail.labelTransaction')}>
            {payment.transactionId?.trim() || FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
          <Descriptions.Item label={t('payments.detail.labelReceiptNumber')}>
            {payment.receiptNumber?.trim() || FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
          <Descriptions.Item label={t('payments.detail.labelTimestampServer')}>
            {payment.createdAt
              ? formatDateTime(payment.createdAt, formatLocale)
              : FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
          <Descriptions.Item label={t('payments.detail.labelAmount')}>
            {formatCurrency(payment.totalAmount ?? 0, formatLocale, { currency })}
          </Descriptions.Item>
          <Descriptions.Item label={t('payments.detail.labelPaymentMethodRaw')}>
            {payment.method?.trim() || FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
          <Descriptions.Item label={t('payments.table.colStatus')}>
            {payment.status?.trim() || FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
        </Descriptions>
      </Card>
    </Space>
  );
}
