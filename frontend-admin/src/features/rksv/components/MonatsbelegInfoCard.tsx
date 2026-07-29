'use client';

/**
 * Explains why Monatsbeleg is not auto-submitted to FinanzOnline (P1-1 NotRequired decision).
 */
import { Alert, Typography } from 'antd';
import React from 'react';

import { useI18n } from '@/i18n';

const BMF_HANDBUCH =
  'https://www.bmf.gv.at/dam/jcr:0af97a40-da60-4c81-8e1e-22c3ecca52a4/BMF_Handbuch_Registrierkassen.pdf';
const WKO_JAHRESBELEG =
  'https://www.wko.at/steuern/pruefung-jahresbeleg-registrierkasse';

export type MonatsbelegInfoCardProps = {
  className?: string;
};

export function MonatsbelegInfoCard({ className }: MonatsbelegInfoCardProps) {
  const { t } = useI18n();

  return (
    <Alert
      className={className}
      type="info"
      showIcon
      title={t('rksvHub.sonderbelege.monatsbelegFonInfoTitle')}
      description={
        <Typography.Paragraph style={{ marginBottom: 0 }}>
          {t('rksvHub.sonderbelege.monatsbelegFonInfoBody')}{' '}
          <Typography.Link href={WKO_JAHRESBELEG} target="_blank" rel="noopener noreferrer">
            {t('rksvHub.sonderbelege.monatsbelegFonInfoWkoLink')}
          </Typography.Link>
          {' · '}
          <Typography.Link href={BMF_HANDBUCH} target="_blank" rel="noopener noreferrer">
            {t('rksvHub.sonderbelege.monatsbelegFonInfoBmfLink')}
          </Typography.Link>
        </Typography.Paragraph>
      }
    />
  );
}
