'use client';

import {
  DownloadOutlined,
  FieldTimeOutlined,
  ShopOutlined,
} from '@ant-design/icons';
import { Button, Descriptions, Space, Spin, Tag, Typography } from 'antd';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import type { LicenseSaleResponse } from '@/api/generated/model';
import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import {
  formatLicenseDaysRemainingLabel,
  formatLicensePlanLabel,
  formatSaleStatusLabel,
  isSaleCancellable,
} from '@/features/billing/utils/billingFormatters';
import { formatCurrency, formatGermanDateTime, useI18n } from '@/i18n';

export type LicenseSaleDetailPanelProps = {
  sale: LicenseSaleResponse;
  tenant?: AdminTenantDetail | null;
  tenantLoading?: boolean;
  pdfLoading?: boolean;
  onDownloadInvoice?: () => void;
  onCancelSale?: () => void;
  /** When true, show a link to the full detail page. */
  showFullPageLink?: boolean;
};

function dash(value: string | number | null | undefined): string {
  if (value == null || value === '') return '—';
  return String(value);
}

export function LicenseSaleDetailPanel({
  sale,
  tenant,
  tenantLoading,
  pdfLoading,
  onDownloadInvoice,
  onCancelSale,
  showFullPageLink = true,
}: LicenseSaleDetailPanelProps) {
  const { t, formatLocale } = useI18n();
  const router = useRouter();

  const statusKey = (sale.status ?? '').toLowerCase();
  const statusColor =
    statusKey === 'active' ? 'green' : statusKey === 'cancelled' ? 'red' : statusKey === 'refunded' ? 'orange' : 'default';

  const tenantStatusLabel = tenant
    ? tenant.isActive === false
      ? t('billing.licenseSales.detail.tenantInactive')
      : (tenant.status ?? t('billing.licenseSales.detail.tenantActive'))
    : '—';

  const registerCount = tenant?.cashRegisterCount ?? tenant?.registerCount;
  const userCount = tenant?.activeUserCount ?? tenant?.userCount;

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap>
        {sale.tenantId ? (
          <Button
            type="primary"
            icon={<FieldTimeOutlined />}
            onClick={() => router.push(`/admin/billing/sales/new?tenantId=${sale.tenantId}`)}
          >
            {t('billing.licenseSales.detail.extendLicense')}
          </Button>
        ) : null}
        {sale.tenantId ? (
          <Button
            icon={<ShopOutlined />}
            onClick={() => router.push(`/admin/tenants/${sale.tenantId}`)}
          >
            {t('billing.licenseSales.detail.viewTenant')}
          </Button>
        ) : null}
        {onDownloadInvoice ? (
          <Button
            icon={<DownloadOutlined />}
            loading={pdfLoading}
            onClick={onDownloadInvoice}
          >
            {t('billing.licenseSales.detail.downloadInvoice')}
          </Button>
        ) : null}
        {onCancelSale && isSaleCancellable(sale) ? (
          <Button danger onClick={onCancelSale}>
            {t('billing.sales.cancelSale')}
          </Button>
        ) : null}
        {showFullPageLink && sale.id ? (
          <Link href={`/admin/billing/sales/${sale.id}`}>
            {t('billing.licenseSales.detail.openFullPage')}
          </Link>
        ) : null}
      </Space>

      <div>
        <Typography.Title level={5} style={{ marginTop: 0 }}>
          {t('billing.licenseSales.detail.sections.license')}
        </Typography.Title>
        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.licenseKey')}>
            <code>{dash(sale.licenseKey)}</code>
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.plan')}>
            {formatLicensePlanLabel(sale.licensePlan, t)}
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.licenseType')}>
            {dash(sale.licenseType)}
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.status')}>
            <Tag color={statusColor}>{formatSaleStatusLabel(sale.status, t)}</Tag>
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.validFrom')}>
            {sale.validFromUtc ? formatGermanDateTime(sale.validFromUtc) : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.validUntil')}>
            {sale.validUntilUtc ? formatGermanDateTime(sale.validUntilUtc) : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.daysRemaining')}>
            {formatLicenseDaysRemainingLabel(sale.validUntilUtc, t)}
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.appliedToTenant')}>
            {sale.appliedToTenant
              ? t('billing.licenseSales.detail.yes')
              : t('billing.licenseSales.detail.no')}
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.soldBy')}>
            {dash(sale.soldBy)}
          </Descriptions.Item>
          {sale.notes ? (
            <Descriptions.Item label={t('billing.licenseSales.detail.labels.notes')}>
              <span style={{ whiteSpace: 'pre-wrap' }}>{sale.notes}</span>
            </Descriptions.Item>
          ) : null}
          {sale.status === 'cancelled' && sale.cancellationReason ? (
            <Descriptions.Item label={t('billing.licenseSales.detail.labels.cancellationReason')}>
              {sale.cancellationReason}
            </Descriptions.Item>
          ) : null}
        </Descriptions>
      </div>

      <div>
        <Typography.Title level={5} style={{ marginTop: 0 }}>
          {t('billing.licenseSales.detail.sections.tenant')}
        </Typography.Title>
        <Spin spinning={!!tenantLoading}>
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={t('billing.licenseSales.detail.labels.tenantName')}>
              {dash(tenant?.name ?? sale.tenantName)}
            </Descriptions.Item>
            <Descriptions.Item label={t('billing.licenseSales.detail.labels.tenantSlug')}>
              {dash(tenant?.slug ?? sale.tenantSlug)}
            </Descriptions.Item>
            <Descriptions.Item label={t('billing.licenseSales.detail.labels.tenantStatus')}>
              {tenantLoading ? '…' : tenantStatusLabel}
            </Descriptions.Item>
          </Descriptions>
        </Spin>
      </div>

      <div>
        <Typography.Title level={5} style={{ marginTop: 0 }}>
          {t('billing.licenseSales.detail.sections.invoice')}
        </Typography.Title>
        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.invoiceNumber')}>
            {dash(sale.invoiceNumber)}
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.invoiceDate')}>
            {sale.soldAtUtc ? formatGermanDateTime(sale.soldAtUtc) : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.priceNet')}>
            {sale.priceNet != null
              ? formatCurrency(sale.priceNet, formatLocale, { currency: sale.currency ?? 'EUR' })
              : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.vat')}>
            {sale.vatAmount != null
              ? `${formatCurrency(sale.vatAmount, formatLocale, { currency: sale.currency ?? 'EUR' })} (${sale.vatRate ?? 0}%)`
              : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('billing.licenseSales.detail.labels.priceGross')}>
            {sale.priceGross != null
              ? formatCurrency(sale.priceGross, formatLocale, { currency: sale.currency ?? 'EUR' })
              : '—'}
          </Descriptions.Item>
        </Descriptions>
      </div>

      <div>
        <Typography.Title level={5} style={{ marginTop: 0 }}>
          {t('billing.licenseSales.detail.sections.usage')}
        </Typography.Title>
        <Spin spinning={!!tenantLoading}>
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={t('billing.licenseSales.detail.labels.registerCount')}>
              {tenantLoading ? '…' : dash(registerCount)}
            </Descriptions.Item>
            <Descriptions.Item label={t('billing.licenseSales.detail.labels.userCount')}>
              {tenantLoading ? '…' : dash(userCount)}
            </Descriptions.Item>
          </Descriptions>
        </Spin>
      </div>
    </Space>
  );
}
