'use client';

/**
 * Read-only preview of the auto-generated tenant admin sign-in email.
 */
import React, { useMemo } from 'react';
import { Alert, Form, Input, Space } from 'antd';

import { useI18n } from '@/i18n';
import { getTenantAppBaseDomain } from '@/lib/auth/impersonationHandoff';
import { CopyIconButton } from '@/features/super-admin/components/CopyIconButton';
import { normalizeTenantSlugInput } from '@/features/super-admin/lib/tenantSlug';
import styles from '@/styles/tenant-form.module.css';

export type TenantAdminAccessPreviewProps = {
    slugValue: string | undefined;
};

export function TenantAdminAccessPreview({ slugValue }: TenantAdminAccessPreviewProps) {
    const { t } = useI18n();
    const baseDomain = getTenantAppBaseDomain();
    const fallbackSlug = t('tenants.create.fields.adminEmail.slugFallback');

    const adminEmail = useMemo(() => {
        const segment = normalizeTenantSlugInput(slugValue ?? '') || fallbackSlug;
        return `admin@${segment}.${baseDomain}`;
    }, [slugValue, baseDomain, fallbackSlug]);

    const slugReady = Boolean(normalizeTenantSlugInput(slugValue ?? ''));

    return (
        <Form.Item
            label={t('tenants.create.fields.adminEmail.label')}
            tooltip={t('tenants.create.fields.adminEmail.tooltip')}
            validateStatus={slugReady ? 'success' : undefined}
            hasFeedback={slugReady}
            extra={
                <div>
                    <div className={styles.hint}>{t('tenants.create.fields.adminEmail.hint')}</div>
                    <Alert
                        type="warning"
                        showIcon
                        message={t('tenants.create.fields.adminEmail.passwordAlert')}
                        style={{ marginTop: 8, marginBottom: 0 }}
                    />
                </div>
            }
        >
            <Space.Compact style={{ width: '100%' }}>
                <Input disabled value={adminEmail} readOnly style={{ flex: 1 }} />
                <CopyIconButton text={adminEmail} ariaLabel={t('tenants.create.fields.adminEmail.copy')} />
            </Space.Compact>
        </Form.Item>
    );
}
