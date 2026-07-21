'use client';

/**
 * Backup & DR sayfası: bölüm başlığı + isteğe bağlı açıklama; tarama ve operatör güvenliği için görsel hiyerarşi.
 */
import { Typography } from 'antd';
import React from 'react';

export interface BackupDrSectionProps {
  /** Sayfa içi bağlantılar için (ör. kontrol listesi atlaması) */
  id?: string;
  titleKey: string;
  descriptionKey?: string;
  children: React.ReactNode;
  t: (key: string, options?: Record<string, string | number>) => string;
}

export function BackupDrSection({
  id,
  titleKey,
  descriptionKey,
  children,
  t,
}: BackupDrSectionProps) {
  return (
    <section id={id} style={{ width: '100%', marginBottom: 0, scrollMarginTop: 72 }}>
      <Typography.Title level={4} style={{ marginTop: 0, marginBottom: descriptionKey ? 6 : 12 }}>
        {t(titleKey)}
      </Typography.Title>
      {descriptionKey ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 14, marginTop: 0 }}>
          {t(descriptionKey)}
        </Typography.Paragraph>
      ) : null}
      {children}
    </section>
  );
}
