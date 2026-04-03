'use client';

/**
 * Kritik / uyarı / bilgi: tek yüzeyde — çoklu Alert yorgunluğunu azaltır; yalnızca kritik varken bilgi bastırılır (uyarı + bilgi birlikte kalabilir).
 */

import React from 'react';
import { Button, Typography } from 'antd';

export interface HealthBannerProps {
  critical: string[];
  warn: string[];
  /** Fake/stub için beklenen notlar — üretim olayı gibi sunulmaz. */
  info?: string[];
  t: (k: string) => string;
  onRefresh?: () => void;
}

function panelStyle(kind: 'critical' | 'warn' | 'info'): React.CSSProperties {
  if (kind === 'critical') {
    return {
      borderLeft: '4px solid #cf1322',
      background: '#fff2f0',
      padding: '10px 12px',
      borderRadius: 4,
    };
  }
  if (kind === 'warn') {
    return {
      borderLeft: '4px solid #fa8c16',
      background: '#fff7e6',
      padding: '10px 12px',
      borderRadius: 4,
    };
  }
  return {
    borderLeft: '4px solid #1677ff',
    background: '#f0f5ff',
    padding: '10px 12px',
    borderRadius: 4,
  };
}

export function HealthBanner({ critical, warn, info = [], t: tt, onRefresh }: HealthBannerProps) {
  /** Uyarı varken bilgiyi gizle — yalnızca kritik varken; aksi halde stub açıklaması gibi önemli info kaybolur. */
  const infoEffective = critical.length > 0 ? [] : info;

  if (critical.length === 0 && warn.length === 0 && infoEffective.length === 0) {
    return null;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      {critical.length > 0 && (
        <div style={panelStyle('critical')}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
            <Typography.Text strong>{tt('backupDr.banner.criticalTitle')}</Typography.Text>
            {onRefresh ? (
              <Button size="small" onClick={onRefresh}>
                {tt('backupDr.actions.refresh')}
              </Button>
            ) : null}
          </div>
          <ul style={{ margin: '8px 0 0', paddingLeft: 20 }}>
            {critical.map((x, i) => (
              <li key={i}>
                <Typography.Text>{x}</Typography.Text>
              </li>
            ))}
          </ul>
        </div>
      )}
      {warn.length > 0 && (
        <div style={panelStyle('warn')}>
          <Typography.Text strong>{tt('backupDr.banner.warningTitle')}</Typography.Text>
          <ul style={{ margin: '8px 0 0', paddingLeft: 20 }}>
            {warn.map((x, i) => (
              <li key={i}>
                <Typography.Text>{x}</Typography.Text>
              </li>
            ))}
          </ul>
        </div>
      )}
      {infoEffective.length > 0 && (
        <div style={panelStyle('info')}>
          <Typography.Text strong>{tt('backupDr.banner.informationalTitle')}</Typography.Text>
          <ul style={{ margin: '8px 0 0', paddingLeft: 20 }}>
            {infoEffective.map((x, i) => (
              <li key={i}>
                <Typography.Text>{x}</Typography.Text>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
