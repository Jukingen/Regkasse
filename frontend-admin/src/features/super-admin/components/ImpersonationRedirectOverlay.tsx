'use client';

import { Spin, Typography } from 'antd';

import { useI18n } from '@/i18n';

/**
 * Full-screen overlay while navigating to tenant subdomain (production) or reloading (development).
 */
export function ImpersonationRedirectOverlay() {
  const { t } = useI18n();

  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy="true"
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
        {t('tenants.impersonationRedirect.title')}
      </Typography.Title>
      <Typography.Text type="secondary">{t('tenants.impersonationRedirect.hint')}</Typography.Text>
    </div>
  );
}
