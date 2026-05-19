'use client';

import { useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { Alert, Button, Spin } from 'antd';
import Link from 'next/link';
import { getTenantSlugFromSubdomain } from '@/features/auth/services/devTenant';
import { applyImpersonationHandoffFromFragment } from '@/lib/auth/tokenHandler';
import { useI18n } from '@/i18n';

export function ImpersonateCallback() {
    const { t } = useI18n();
    const processed = useRef(false);
    const [errorKey, setErrorKey] = useState<string | null>(null);

    useLayoutEffect(() => {
        if (processed.current || typeof window === 'undefined') {
            return;
        }
        processed.current = true;

        const expectedTenant = getTenantSlugFromSubdomain();
        const result = applyImpersonationHandoffFromFragment(window.location.hash, expectedTenant);

        if (!result.ok) {
            setErrorKey(result.reason);
            return;
        }

        window.location.replace('/dashboard');
    }, []);

    if (errorKey) {
        const messageId = `tenants.impersonationCallback.errors.${errorKey}` as const;
        return (
            <CenteredPanel>
                <Alert
                    type="error"
                    showIcon
                    message={t('tenants.impersonationCallback.titleFailed')}
                    description={t(messageId)}
                    style={{ maxWidth: 480 }}
                />
                <Link href="/login" style={{ marginTop: 16 }}>
                    <Button type="primary">{t('common.auth.login')}</Button>
                </Link>
            </CenteredPanel>
        );
    }

    return (
        <CenteredPanel>
            <Spin size="large" tip={t('tenants.impersonationCallback.processing')} />
        </CenteredPanel>
    );
}

function CenteredPanel({ children }: { children: ReactNode }) {
    return (
        <div
            style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                minHeight: '100vh',
                background: '#f0f2f5',
                padding: 24,
            }}
        >
            {children}
        </div>
    );
}
