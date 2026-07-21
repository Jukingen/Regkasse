'use client';

import { Alert, Space } from 'antd';

import { useTenantDigitalService } from '@/features/digital-services/hooks/useTenantDigitalServices';
import { ManagerDigitalRequestPanel } from '@/features/digital/components/ManagerDigitalRequestPanel';
import {
  canGenerateDigitalApp,
  canGenerateDigitalWebsite,
} from '@/features/digital/digitalServicePermissions';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { DigitalServicePricingCards } from '@/features/website-generator/components/DigitalServicePricingCards';
import { WebsiteGeneratorPanel } from '@/features/website-generator/components/WebsiteGeneratorPanel';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';

type DigitalServicesProps = {
  /** Optional tenant override; defaults to ambient JWT tenant. */
  tenantId?: string;
};

/**
 * Digital-services content.
 * Super Admin: pricing + generators.
 * Manager: view / preview / request only (no create/generate).
 */
export function DigitalServices({ tenantId }: DigitalServicesProps) {
  const { t } = useI18n();
  const { user, isSuperAdmin } = usePermissions();
  const currentTenant = useCurrentTenant();
  const effectiveTenantId = tenantId ?? currentTenant.tenantId ?? undefined;
  const userPerms = user ? { permissions: user.permissions } : null;
  const { data: status } = useTenantDigitalService(effectiveTenantId);

  if (effectiveTenantId && !isSuperAdmin) {
    return <ManagerDigitalRequestPanel tenantId={effectiveTenantId} />;
  }

  const websiteEnabled = canGenerateDigitalWebsite(userPerms, isSuperAdmin, status);
  const appEnabled = canGenerateDigitalApp(userPerms, isSuperAdmin, status);

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <DigitalServicePricingCards />
      {!websiteEnabled && !appEnabled ? (
        <Alert
          type="warning"
          showIcon
          title={t('tenants.digitalServices.generatorsDisabledTitle')}
          description={t('tenants.digitalServices.generatorsDisabledBody')}
        />
      ) : (
        <WebsiteGeneratorPanel websiteEnabled={websiteEnabled} appEnabled={appEnabled} />
      )}
    </Space>
  );
}
