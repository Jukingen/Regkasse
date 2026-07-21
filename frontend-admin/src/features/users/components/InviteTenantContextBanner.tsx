'use client';

import { MailOutlined } from '@ant-design/icons';
import { Alert, Typography } from 'antd';
import React from 'react';

import { buildTenantPortalUrl } from '@/features/super-admin/api/adminTenants';
import { getMandantLicenseBadgeDisplay } from '@/features/tenant/utils/mandantLicenseBadge';
import type { InviteTenantContextFields } from '@/features/users/utils/inviteTenantDisplay';
import { formatInviteTenantLicenseShort } from '@/features/users/utils/inviteTenantDisplay';
import { useI18n } from '@/i18n';

export type InviteTenantContextBannerProps = {
  tenant: InviteTenantContextFields;
  /** Compact strip inside modal vs full alert on tenant users tab */
  variant?: 'modal' | 'page';
};

export function InviteTenantContextBanner({
  tenant,
  variant = 'modal',
}: InviteTenantContextBannerProps) {
  const { t } = useI18n();
  const portalUrl = buildTenantPortalUrl(tenant.slug);
  const licenseBadge = getMandantLicenseBadgeDisplay(
    tenant.licenseValidUntilUtc,
    tenant.licenseKey,
    t
  );
  const licenseLine = licenseBadge?.label ?? formatInviteTenantLicenseShort(tenant, t);

  const message = t('users.create.targetTenant.title', {
    name: tenant.name,
    slug: tenant.slug,
  });

  const description = (
    <>
      <Typography.Link href={portalUrl} target="_blank" rel="noopener noreferrer">
        {portalUrl}
      </Typography.Link>
      <br />
      <Typography.Text type="secondary">
        {t('users.create.targetTenant.licenseLine', { license: licenseLine })}
      </Typography.Text>
    </>
  );

  if (variant === 'page') {
    return (
      <Alert
        type="info"
        showIcon
        icon={<MailOutlined />}
        title={message}
        description={description}
        style={{ marginBottom: 0 }}
      />
    );
  }

  return (
    <Alert
      type="info"
      showIcon
      icon={<MailOutlined />}
      title={t('users.create.targetTenant.modalLabel')}
      description={
        <>
          <Typography.Text strong>
            {tenant.name} ({tenant.slug})
          </Typography.Text>
          <br />
          <Typography.Link href={portalUrl} target="_blank" rel="noopener noreferrer">
            {portalUrl}
          </Typography.Link>
          <br />
          <Typography.Text type="secondary">
            {t('users.create.targetTenant.licenseLine', { license: licenseLine })}
          </Typography.Text>
        </>
      }
      style={{ marginBottom: 16 }}
    />
  );
}
