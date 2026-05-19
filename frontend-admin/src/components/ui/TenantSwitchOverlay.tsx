'use client';

import { Spin, Typography } from 'antd';

import { useI18n } from '@/i18n';

export type TenantSwitchOverlayProps = {
    visible: boolean;
};

/** Full-screen overlay while mandant context is being cleared and the shell reloads. */
export function TenantSwitchOverlay({ visible }: TenantSwitchOverlayProps) {
    const { t } = useI18n();

    if (!visible) {
        return null;
    }

    return (
        <div
            role="status"
            aria-live="polite"
            aria-busy="true"
            aria-label={t('adminShell.tenant.switching.message')}
            style={{
                position: 'fixed',
                inset: 0,
                zIndex: 10000,
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                background: 'rgba(255, 255, 255, 0.92)',
                gap: 16,
            }}
        >
            <Spin size="large" />
            <Typography.Title level={5} style={{ margin: 0, fontWeight: 500 }}>
                {t('adminShell.tenant.switching.message')}
            </Typography.Title>
        </div>
    );
}
