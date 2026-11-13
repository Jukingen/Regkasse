'use client';

import { Typography } from 'antd';

import { displayLicenseKey } from '@/features/license/utils/tenantLicenseExtend';
import { useI18n } from '@/i18n';

type LicenseKeyRevealTextProps = {
  licenseKey: string | null | undefined;
  reveal: boolean;
};

export function LicenseKeyRevealText({ licenseKey, reveal }: LicenseKeyRevealTextProps) {
  const { t } = useI18n();
  const full = licenseKey?.trim() ?? '';
  if (!full) {
    return <Typography.Text type="secondary">—</Typography.Text>;
  }

  return (
    <Typography.Text
      code
      copyable={{
        text: full,
        tooltips: [
          t('license.management.copyKey'),
          t('license.generation.result.licenseKeyCopied'),
        ],
      }}
    >
      {displayLicenseKey(full, reveal)}
    </Typography.Text>
  );
}
