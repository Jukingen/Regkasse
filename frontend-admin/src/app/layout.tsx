import React, { ReactNode } from 'react';

import { CsrfTokenBootstrap } from '@/components/CsrfTokenBootstrap';
import { TokenRefreshListener } from '@/components/TokenRefreshListener';
import { PerformanceMonitoring } from '@/components/monitoring/PerformanceMonitoring';
import StyledComponentsRegistry from '@/lib/AntdRegistry';
import { THEME_BOOTSTRAP_SCRIPT } from '@/lib/personalization/themeBootstrapScript';
import { AppProviders } from '@/providers/AppProviders';

import './globals.css';

export const metadata = {
  title: 'Regkasse Admin',
  description: 'Admin Panel for Regkasse POS',
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="de" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: THEME_BOOTSTRAP_SCRIPT }} />
      </head>
      <body style={{ margin: 0, padding: 0 }} suppressHydrationWarning>
        <AppProviders>
          {/* Auto-refresh access token ~5 min before JWT expiry (silent). */}
          <TokenRefreshListener />
          {/* Issue CSRF token on load (cookie + API cache) for mutation requests. */}
          <CsrfTokenBootstrap />
          {/* Core Web Vitals → Sentry (+ optional Vercel Speed Insights / self-hosted beacon). */}
          <PerformanceMonitoring />
          <StyledComponentsRegistry>{children}</StyledComponentsRegistry>
        </AppProviders>
      </body>
    </html>
  );
}
