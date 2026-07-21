'use client';

/**
 * Live address preview and availability below the subdomain field.
 */
import { LinkOutlined } from '@ant-design/icons';
import { Spin, Typography } from 'antd';
import React from 'react';

import type { SlugAvailabilityUi } from '@/features/super-admin/hooks/useTenantCreateFormFields';
import { getTenantSlugPreviewSegment } from '@/features/super-admin/lib/tenantSlug';
import { useI18n } from '@/i18n';
import styles from '@/styles/tenant-form.module.css';

export type TenantSlugFieldExtrasProps = {
  slugValue: string | undefined;
  baseDomain: string;
  portalUrl?: string | null;
  availabilityUi: SlugAvailabilityUi;
};

export function TenantSlugFieldExtras({
  slugValue,
  baseDomain,
  portalUrl,
  availabilityUi,
}: TenantSlugFieldExtrasProps) {
  const { t } = useI18n();
  const previewSegment = getTenantSlugPreviewSegment(slugValue);

  return (
    <div>
      {availabilityUi === 'checking' ? (
        <p className={`${styles.availability} ${styles.availabilityChecking}`}>
          <Spin size="small" style={{ marginRight: 8 }} />
          {t('tenants.create.fields.slug.checkingShort')}
        </p>
      ) : null}
      {availabilityUi === 'available' ? (
        <p className={`${styles.availability} ${styles.availabilityAvailable}`}>
          {t('tenants.create.fields.slug.availableShort')}
        </p>
      ) : null}
      {availabilityUi === 'taken' ? (
        <p className={`${styles.availability} ${styles.availabilityTaken}`}>
          {t('tenants.create.fields.slug.takenShort')}
        </p>
      ) : null}

      {previewSegment && availabilityUi === 'available' ? (
        <p className={styles.preview}>
          <LinkOutlined aria-hidden style={{ marginRight: 6 }} />
          {portalUrl ? (
            <Typography.Link href={portalUrl} target="_blank" rel="noopener noreferrer" strong>
              {previewSegment}.{baseDomain}
            </Typography.Link>
          ) : (
            <Typography.Text strong>
              {previewSegment}.{baseDomain}
            </Typography.Text>
          )}
        </p>
      ) : null}
    </div>
  );
}
