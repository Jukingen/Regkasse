'use client';

import { Card, Statistic, Typography } from 'antd';
import React from 'react';

import { formatCurrency, useI18n } from '@/i18n';

type Props = {
  periodLabel?: string | null;
  receiptCount: number;
  totalRevenue: number;
  zeroSales: boolean;
  loading?: boolean;
};

export function PeriodSalesSummaryCard({
  periodLabel,
  receiptCount,
  totalRevenue,
  zeroSales,
  loading,
}: Props) {
  const { t, formatLocale } = useI18n();

  return (
    <Card size="small" loading={loading} data-testid="period-sales-summary" style={{ minWidth: 260 }}>
      <Typography.Text type="secondary">{t('reporting.periodSales.period')}</Typography.Text>
      <Typography.Paragraph strong style={{ marginBottom: 8 }}>
        {periodLabel?.trim() || '—'}
      </Typography.Paragraph>
      <Statistic
        title={t('reporting.periodSales.revenue')}
        value={formatCurrency(totalRevenue, formatLocale)}
      />
      <Statistic title={t('reporting.periodSales.receipts')} value={receiptCount} />
      {zeroSales ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 8 }}>
          {t('reporting.periodSales.zeroSales')}
        </Typography.Paragraph>
      ) : null}
    </Card>
  );
}
