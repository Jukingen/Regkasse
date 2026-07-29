'use client';

import { useQuery } from '@tanstack/react-query';
import { Card, Empty, Segmented, Skeleton, Typography } from 'antd';
import React, { useEffect, useState } from 'react';

import {
  type LicenseReminderEmailPreviewDto,
  getLicenseReminderEmailPreview,
  licenseQueryKeys,
} from '@/api/manual/adminLicense';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

const HORIZONS = [30, 14, 7, 1, 0] as const;

type HorizonDays = (typeof HORIZONS)[number];

/**
 * Super Admin preview of the mandant license reminder HTML email (same composer as SMTP).
 */
export function LicenseReminderEmailPreviewCard() {
  const { t } = useI18n();
  const notify = useNotify();
  const [daysUntilExpiry, setDaysUntilExpiry] = useState<HorizonDays>(7);

  const previewQuery = useQuery({
    queryKey: licenseQueryKeys.reminderEmailPreview(daysUntilExpiry),
    queryFn: () => getLicenseReminderEmailPreview({ daysUntilExpiry }),
  });

  useEffect(() => {
    if (previewQuery.isError) {
      notify.apiError(previewQuery.error, {
        logContext: 'LicenseReminderEmailPreviewCard.load',
        fallbackKey: 'license.emailPreview.error',
      });
    }
  }, [notify, previewQuery.error, previewQuery.isError]);

  const preview: LicenseReminderEmailPreviewDto | undefined = previewQuery.data;

  return (
    <Card title={t('license.emailPreview.title')} style={{ marginTop: 16 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {t('license.emailPreview.subtitle')}
      </Typography.Paragraph>

      <Segmented
        value={daysUntilExpiry}
        onChange={(value) => setDaysUntilExpiry(value as HorizonDays)}
        options={HORIZONS.map((days) => ({
          label: t(`license.emailPreview.horizons.d${days}`),
          value: days,
        }))}
        style={{ marginBottom: 16 }}
      />

      {previewQuery.isLoading ? (
        <Skeleton active paragraph={{ rows: 8 }} />
      ) : !preview ? (
        <Empty description={t('license.emailPreview.error')} />
      ) : (
        <>
          <Typography.Text type="secondary">{t('license.emailPreview.subjectLabel')}</Typography.Text>
          <Typography.Paragraph strong style={{ marginTop: 4 }}>
            {preview.subject}
          </Typography.Paragraph>

          <Typography.Text type="secondary">{t('license.emailPreview.htmlLabel')}</Typography.Text>
          <iframe
            title={t('license.emailPreview.iframeTitle')}
            srcDoc={preview.htmlBody}
            sandbox=""
            style={{
              width: '100%',
              height: 420,
              marginTop: 8,
              border: '1px solid #f0f0f0',
              borderRadius: 8,
              background: '#fff',
            }}
          />
        </>
      )}
    </Card>
  );
}
