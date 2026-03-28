'use client';

/**
 * Shows raw server text in an operator/diagnostic context: short localized intro + copyable body (kept separate from main UI copy).
 */
import { Typography } from 'antd';
import { useI18n } from '@/i18n';

type Props = {
  introKey: string;
  body: string | null | undefined;
  /** Ant Typography.Text type */
  textType?: 'secondary' | 'warning' | 'danger';
};

export function BackendRawTextBlock({ introKey, body, textType = 'secondary' }: Props) {
  const { t } = useI18n();
  const s = typeof body === 'string' ? body.trim() : '';
  if (!s) return null;
  return (
    <div>
      <Typography.Text type={textType} style={{ display: 'block', marginBottom: 4 }}>
        {t(introKey)}
      </Typography.Text>
      <Typography.Text code copyable>
        {s}
      </Typography.Text>
    </div>
  );
}
