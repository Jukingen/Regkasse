'use client';

import { Card, theme } from 'antd';
import { type CSSProperties, type ReactNode } from 'react';

import type { ResolvedTheme } from '@/lib/personalization/types';
import { surface } from '@/theme/palette';

type AuthPageFrameProps = {
  children: ReactNode;
  /** Optional absolute overlay (e.g. language switcher). */
  topRight?: ReactNode;
  className?: string;
  style?: CSSProperties;
};

/**
 * Full-viewport auth / gate shell. Uses Ant Design tokens so light/dark stay aligned
 * with ConfigProvider (no hardcoded `#f0f2f5`).
 */
export function AuthPageFrame({ children, topRight, className, style }: AuthPageFrameProps) {
  const { token } = theme.useToken();

  return (
    <div
      className={className}
      style={{
        position: 'relative',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        minHeight: '100vh',
        height: '100vh',
        background: token.colorBgLayout,
        ...style,
      }}
    >
      {topRight ? (
        <div style={{ position: 'absolute', top: token.marginMD, right: token.marginMD }}>
          {topRight}
        </div>
      ) : null}
      {children}
    </div>
  );
}

type AuthCardProps = {
  children: ReactNode;
  width?: number | string;
  className?: string;
  style?: CSSProperties;
};

/** Centered auth card with token-driven elevation. */
export function AuthCard({ children, width = 400, className, style }: AuthCardProps) {
  const { token } = theme.useToken();

  return (
    <Card
      className={className}
      style={{
        width,
        boxShadow: token.boxShadowSecondary,
        ...style,
      }}
    >
      {children}
    </Card>
  );
}

/** Fallback wash when tokens are unavailable (SSR bootstrap / non-ConfigProvider). */
export function authBackgroundForResolved(resolved: ResolvedTheme): string {
  return surface[resolved].colorBgAuth;
}
