'use client';

/**
 * Kritik ve uyarı banner’ları: kritik varken uyarılar gizlenmez (yanıltıcı sessizlik önlenir).
 */

import React from 'react';
import { Alert, Button, Typography } from 'antd';

export interface HealthBannerProps {
  critical: string[];
  warn: string[];
  t: (k: string) => string;
  onRefresh?: () => void;
}

export function HealthBanner({ critical, warn, t: tt, onRefresh }: HealthBannerProps) {
  return (
    <>
      {critical.length > 0 && (
        <Alert
          type="error"
          showIcon
          message={tt('backupDr.banner.criticalTitle')}
          action={onRefresh ? <Button onClick={onRefresh}>{tt('backupDr.actions.refresh')}</Button> : undefined}
          description={
            <ul style={{ marginBottom: 0, paddingLeft: 20 }}>
              {critical.map((x, i) => (
                <li key={i}>
                  <Typography.Text>{x}</Typography.Text>
                </li>
              ))}
            </ul>
          }
        />
      )}
      {warn.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message={tt('backupDr.banner.warningTitle')}
          description={
            <ul style={{ marginBottom: 0, paddingLeft: 20 }}>
              {warn.map((x, i) => (
                <li key={i}>
                  <Typography.Text>{x}</Typography.Text>
                </li>
              ))}
            </ul>
          }
        />
      )}
    </>
  );
}
