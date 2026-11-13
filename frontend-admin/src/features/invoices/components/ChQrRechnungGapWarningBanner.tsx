'use client';

/**
 * Informational CH QR print-gap warning. Does not change the PDF,
 * does not enable bank submission, and does not block invoice actions.
 */
import { Alert } from 'antd';
import Link from 'next/link';
import { useMemo } from 'react';

import { useGetApiAdminTenantsTenantIdChQrGapAcceptance } from '@/api/generated/admin/admin';
import { useCompanySettings } from '@/features/settings/hooks/useCompanySettings';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { useI18n } from '@/i18n';

function gapLabel(t: (key: string) => string, id: string): string {
  switch (id) {
    case 'official-swiss-cross':
      return t('invoices.chQr.gapWarning.gap.officialSwissCross');
    case 'font-embedding-liberation-arial':
      return t('invoices.chQr.gapWarning.gap.fontEmbeddingLiberationArial');
    case 'pain001':
      return t('invoices.chQr.gapWarning.gap.pain001');
    case 'bank-scan':
      return t('invoices.chQr.gapWarning.gap.bankScan');
    case 'perforation-line':
      return t('invoices.chQr.gapWarning.gap.perforationLine');
    default:
      return id;
  }
}

export function ChQrRechnungGapWarningBanner() {
  const { t } = useI18n();
  const { tenant } = useTenant();
  const settings = useCompanySettings();
  const tenantId = tenant?.id ?? '';
  const country = settings.data?.country;
  const enabled = country === 'CH' && tenantId.length > 0;
  const gapsQuery = useGetApiAdminTenantsTenantIdChQrGapAcceptance(tenantId, {
    query: { enabled, retry: false },
  });

  const outstanding = useMemo(() => {
    if (!enabled) return [];
    const known = gapsQuery.data?.knownGaps ?? [];
    const accepted = new Set(gapsQuery.data?.acceptance?.acceptedGaps ?? []);
    return known
      .filter((gap) => gap.present === false && typeof gap.id === 'string' && gap.id.length > 0 && !accepted.has(gap.id))
      .map((gap) => gap.id as string);
  }, [enabled, gapsQuery.data]);

  if (!enabled || gapsQuery.isLoading || gapsQuery.isError || outstanding.length === 0) {
    return null;
  }

  return (
    <Alert
      type="warning"
      showIcon
      data-testid="ch-qr-gap-warning-banner"
      style={{ marginBottom: 16 }}
      title={t('invoices.chQr.gapWarning.title')}
      description={
        <>
          <div>{t('invoices.chQr.gapWarning.description')}</div>
          <ul style={{ margin: '8px 0' }}>
            {outstanding.map((id) => (
              <li key={id} data-testid={`ch-qr-gap-warning-${id}`}>
                {gapLabel(t, id)}
              </li>
            ))}
          </ul>
          <Link href={`/admin/tenants/${tenantId}`}>{t('invoices.chQr.gapWarning.link')}</Link>
        </>
      }
    />
  );
}
