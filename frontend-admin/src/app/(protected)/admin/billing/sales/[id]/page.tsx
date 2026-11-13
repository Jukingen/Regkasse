'use client';

import { ArrowLeftOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, Form, Input, Modal, Space, Tag } from 'antd';
import { useParams, useRouter } from 'next/navigation';
import { useState } from 'react';

import { CardSkeleton } from '@/components/Skeleton';
import { BillingAccessGate } from '@/features/billing/components/BillingAccessGate';
import { LicenseSaleDetailPanel } from '@/features/billing/components/LicenseSaleDetailPanel';
import { useBillingSale, useCancelLicenseSale } from '@/features/billing/hooks';
import { formatSaleStatusLabel } from '@/features/billing/utils/billingFormatters';
import { downloadLicenseSaleInvoicePdf } from '@/features/billing/utils/downloadInvoicePdf';
import { getAdminTenantById } from '@/features/super-admin/api/adminTenants';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const CANCEL_REASON_MIN_LENGTH = 10;

export default function BillingSaleDetailPage() {
  const params = useParams<{ id: string }>();
  const id = typeof params.id === 'string' ? params.id : '';
  const router = useRouter();
  const { message } = useAntdApp();
  const { t } = useI18n();
  const [cancelOpen, setCancelOpen] = useState(false);
  const [pdfLoading, setPdfLoading] = useState(false);

  const { data: sale, isLoading, refetch } = useBillingSale(id);
  const cancelMutation = useCancelLicenseSale();

  const tenantQuery = useQuery({
    queryKey: ['admin-tenant', 'billing-sale-detail', sale?.tenantId],
    queryFn: () => getAdminTenantById(sale!.tenantId!),
    enabled: !!sale?.tenantId,
    staleTime: 60_000,
  });

  const handlePdfDownload = async () => {
    if (!sale?.id) return;
    setPdfLoading(true);
    try {
      await downloadLicenseSaleInvoicePdf(
        sale.id,
        sale.invoiceNumber ? `${sale.invoiceNumber}.pdf` : undefined
      );
    } catch (err) {
      openApiErrorMessage(message.open, t, err, {
        logContext: 'BillingSaleDetailPage.downloadPdf',
      });
    } finally {
      setPdfLoading(false);
    }
  };

  if (isLoading) {
    return (
      <BillingAccessGate>
        <CardSkeleton count={2} />
      </BillingAccessGate>
    );
  }

  if (!sale) {
    return (
      <BillingAccessGate>
        <div style={{ padding: 24 }}>{t('billing.licenseSales.detail.notFound')}</div>
      </BillingAccessGate>
    );
  }

  const statusKey = (sale.status ?? '').toLowerCase();
  const statusColor =
    statusKey === 'active' ? 'green' : statusKey === 'cancelled' ? 'red' : statusKey === 'refunded' ? 'orange' : 'default';

  return (
    <BillingAccessGate>
      <div style={{ padding: 24 }}>
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              flexWrap: 'wrap',
              gap: 16,
            }}
          >
            <Space wrap>
              <Button icon={<ArrowLeftOutlined />} onClick={() => router.back()}>
                {t('billing.licenseSales.detail.back')}
              </Button>
              <h1 style={{ margin: 0 }}>
                {t('billing.licenseSales.detail.pageTitle', {
                  invoice: sale.invoiceNumber ?? sale.id ?? '',
                })}
              </h1>
              <Tag color={statusColor}>{formatSaleStatusLabel(sale.status, t)}</Tag>
            </Space>
          </div>

          <Card>
            <LicenseSaleDetailPanel
              sale={sale}
              tenant={tenantQuery.data}
              tenantLoading={tenantQuery.isLoading}
              pdfLoading={pdfLoading}
              onDownloadInvoice={() => void handlePdfDownload()}
              onCancelSale={() => setCancelOpen(true)}
              showFullPageLink={false}
            />
          </Card>
        </Space>
      </div>

      {cancelOpen ? (
        <BillingSaleDetailCancelModal
          loading={cancelMutation.isPending}
          onClose={() => setCancelOpen(false)}
          onSuccess={async () => {
            message.success(t('billing.sales.cancelSuccess'));
            setCancelOpen(false);
            await refetch();
          }}
          onError={(err) =>
            openApiErrorMessage(message.open, t, err, {
              logContext: 'BillingSaleDetailPage.cancelSale',
            })
          }
          onCancel={async (cancellationReason) => {
            await cancelMutation.mutateAsync({ id, data: { cancellationReason } });
          }}
        />
      ) : null}
    </BillingAccessGate>
  );
}

type BillingSaleDetailCancelModalProps = {
  loading: boolean;
  onClose: () => void;
  onSuccess: () => void | Promise<void>;
  onError: (err: unknown) => void;
  onCancel: (cancellationReason: string) => Promise<void>;
};

function BillingSaleDetailCancelModal({
  loading,
  onClose,
  onSuccess,
  onError,
  onCancel,
}: BillingSaleDetailCancelModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<{ cancellationReason: string }>();

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      await onCancel(values.cancellationReason);
      await onSuccess();
    } catch (err) {
      if (err && typeof err === 'object' && 'errorFields' in err) return;
      onError(err);
    }
  };

  return (
    <Modal
      open
      title={t('billing.sales.cancelConfirmTitle')}
      onCancel={onClose}
      onOk={handleOk}
      confirmLoading={loading}
      okText={t('billing.sales.cancelSale')}
      okButtonProps={{ danger: true }}
      cancelText={t('common.buttons.cancel')}
      destroyOnHidden
    >
      <p>{t('billing.detail.cancelConfirmMessage')}</p>
      <Form
        form={form}
        layout="vertical"
        style={{ marginTop: 16 }}
        initialValues={{ cancellationReason: t('billing.detail.cancelDefaultReason') }}
      >
        <Form.Item
          name="cancellationReason"
          label={t('billing.sales.cancelReasonLabel')}
          rules={[
            { required: true, message: t('billing.sales.cancelReasonRequired') },
            { min: CANCEL_REASON_MIN_LENGTH, message: t('billing.sales.cancelReasonRequired') },
          ]}
        >
          <Input.TextArea rows={3} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
