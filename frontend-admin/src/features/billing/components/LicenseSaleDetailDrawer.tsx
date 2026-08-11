'use client';

import { useQuery } from '@tanstack/react-query';
import { Drawer, Spin } from 'antd';

import type { LicenseSaleResponse } from '@/api/generated/model';
import { LicenseSaleDetailPanel } from '@/features/billing/components/LicenseSaleDetailPanel';
import { useBillingSale } from '@/features/billing/hooks/useBillingSale';
import { getAdminTenantById } from '@/features/super-admin/api/adminTenants';
import { useI18n } from '@/i18n';

export type LicenseSaleDetailDrawerProps = {
  saleId: string | null;
  /** List-row snapshot used until the detail query resolves. */
  initialSale?: LicenseSaleResponse | null;
  open: boolean;
  onClose: () => void;
  pdfLoading?: boolean;
  onDownloadInvoice?: (sale: LicenseSaleResponse) => void;
  onCancelSale?: (sale: LicenseSaleResponse) => void;
};

export function LicenseSaleDetailDrawer({
  saleId,
  initialSale,
  open,
  onClose,
  pdfLoading,
  onDownloadInvoice,
  onCancelSale,
}: LicenseSaleDetailDrawerProps) {
  const { t } = useI18n();
  const saleQuery = useBillingSale(open && saleId ? saleId : undefined);
  const sale = saleQuery.data ?? initialSale ?? null;
  const tenantId = sale?.tenantId;

  const tenantQuery = useQuery({
    queryKey: ['admin-tenant', 'billing-sale-detail', tenantId],
    queryFn: () => getAdminTenantById(tenantId!),
    enabled: open && !!tenantId,
    staleTime: 60_000,
  });

  return (
    <Drawer
      title={t('billing.licenseSales.detail.title')}
      open={open}
      onClose={onClose}
      size={640}
      destroyOnHidden
    >
      <Spin spinning={saleQuery.isLoading && !sale}>
        {sale ? (
          <LicenseSaleDetailPanel
            sale={sale}
            tenant={tenantQuery.data}
            tenantLoading={tenantQuery.isLoading}
            pdfLoading={pdfLoading}
            onDownloadInvoice={
              onDownloadInvoice && sale.id ? () => onDownloadInvoice(sale) : undefined
            }
            onCancelSale={onCancelSale && sale.id ? () => onCancelSale(sale) : undefined}
            showFullPageLink
          />
        ) : (
          <div>{t('billing.licenseSales.detail.notFound')}</div>
        )}
      </Spin>
    </Drawer>
  );
}
