'use client';

/**
 * Live subdomain preview, availability status, and allowed/disallowed examples below the slug input.
 */
import React from 'react';
import { Spin, Typography } from 'antd';
import { LinkOutlined } from '@ant-design/icons';

import { useI18n } from '@/i18n';
import type { SlugAvailabilityUi } from '@/features/super-admin/hooks/useTenantCreateFormFields';
import { getTenantSlugPreviewSegment } from '@/features/super-admin/lib/tenantSlug';
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
            {previewSegment ? (
                <p className={styles.preview}>
                    <LinkOutlined aria-hidden style={{ marginRight: 6 }} />
                    {t('tenants.create.fields.slug.previewLabel')}{' '}
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

            <div className={styles.example}>
                <p className={styles.exampleLine}>
                    <Typography.Text type="success">✅ {t('tenants.create.fields.slug.allowedTitle')}</Typography.Text>{' '}
                    {t('tenants.create.fields.slug.allowedExamples')}
                </p>
                <p className={styles.exampleLine}>
                    <Typography.Text type="danger">❌ {t('tenants.create.fields.slug.disallowedTitle')}</Typography.Text>{' '}
                    {t('tenants.create.fields.slug.disallowedExamples')}
                </p>
            </div>
        </div>
    );
}
