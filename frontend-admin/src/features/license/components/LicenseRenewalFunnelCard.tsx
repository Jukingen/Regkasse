'use client';

import { Card, Empty, Flex, Progress, Skeleton, Typography } from 'antd';
import React, { useMemo } from 'react';

import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { useLicenseRenewalFunnel } from '@/features/license/hooks/useLicenseRenewalFunnel';
import {
  getLicenseRenewalFunnelStepPercent,
  getLicenseRenewalFunnelStrokeColor,
} from '@/features/license/utils/licenseRenewalFunnel';
import { useI18n } from '@/i18n';

type FunnelStepKey = 'reminder' | 'pageView' | 'renewed' | 'activated';

/**
 * Super Admin license renewal conversion funnel (reminder → view → renew → activate).
 */
export function LicenseRenewalFunnelCard() {
  const { t } = useI18n();
  const canAccess = useBillingAccess();
  const funnelQuery = useLicenseRenewalFunnel({}, canAccess);

  const steps = useMemo(() => {
    const data = funnelQuery.data;
    const total = data?.total ?? 0;
    const rows: { key: FunnelStepKey; count: number; labelKey: string }[] = [
      {
        key: 'reminder',
        count: data?.reminderSent ?? 0,
        labelKey: 'license.renewalFunnel.steps.reminderSent',
      },
      {
        key: 'pageView',
        count: data?.pageViewed ?? 0,
        labelKey: 'license.renewalFunnel.steps.pageViewed',
      },
      {
        key: 'renewed',
        count: data?.renewed ?? 0,
        labelKey: 'license.renewalFunnel.steps.renewed',
      },
      {
        key: 'activated',
        count: data?.activated ?? 0,
        labelKey: 'license.renewalFunnel.steps.activated',
      },
    ];
    return rows.map((row) => {
      const percent = getLicenseRenewalFunnelStepPercent(row.count, total);
      return {
        ...row,
        total,
        percent,
        strokeColor: getLicenseRenewalFunnelStrokeColor(row.key, percent),
      };
    });
  }, [funnelQuery.data]);

  if (!canAccess) return null;

  return (
    <Card
      title={t('license.renewalFunnel.title')}
      style={{ marginTop: 16 }}
      extra={
        funnelQuery.data ? (
          <Typography.Text type="secondary">
            {t('license.renewalFunnel.lookbackHint')}
          </Typography.Text>
        ) : null
      }
    >
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('license.renewalFunnel.subtitle')}
      </Typography.Paragraph>

      {funnelQuery.isLoading ? (
        <Skeleton active paragraph={{ rows: 6 }} />
      ) : funnelQuery.isError ? (
        <Typography.Text type="danger">{t('license.renewalFunnel.loadFailed')}</Typography.Text>
      ) : !funnelQuery.data || funnelQuery.data.total === 0 ? (
        <Empty description={t('license.renewalFunnel.empty')} />
      ) : (
        <Flex vertical gap={16}>
          {steps.map((step) => (
            <Flex key={step.key} vertical gap={4}>
              <Flex justify="space-between">
                <Typography.Text>{t(step.labelKey)}</Typography.Text>
                <Typography.Text type="secondary">
                  {step.count} / {step.total}
                </Typography.Text>
              </Flex>
              <Progress
                percent={step.percent}
                size="small"
                strokeColor={step.strokeColor}
                showInfo={false}
              />
            </Flex>
          ))}
          <Typography.Text type="secondary">
            {t('license.renewalFunnel.conversionRate', {
              rate: funnelQuery.data.conversionRate,
            })}
          </Typography.Text>
        </Flex>
      )}
    </Card>
  );
}
