'use client';

import { useQuery } from '@tanstack/react-query';
import { Card, Empty, Spin, Timeline, Typography } from 'antd';
import React, { useMemo } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { getTaxHistory, taxHistoryQueryKey } from '@/features/tax/api/taxHistory';
import { useI18n } from '@/i18n';
import { useDateFormatter } from '@/lib/hooks/useDateFormatter';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function TaxHistoryPage() {
  const { t } = useI18n();
  const { formatDate } = useDateFormatter();

  const { data: taxHistory, isLoading, isError } = useQuery({
    queryKey: taxHistoryQueryKey,
    queryFn: () => getTaxHistory({ take: 100 }),
  });

  const timelineItems = useMemo(() => {
    const items = taxHistory ?? [];
    return items.map((item) => ({
      key: item.id,
      color: item.newRate > item.oldRate ? 'red' : item.newRate < item.oldRate ? 'green' : 'blue',
      content: (
        <div>
          <div
            style={{
              display: 'flex',
              flexWrap: 'wrap',
              justifyContent: 'space-between',
              gap: 8,
              alignItems: 'baseline',
            }}
          >
            <Typography.Text strong>{item.productName || item.productId}</Typography.Text>
            <Typography.Text>
              {item.oldRate}% → {item.newRate}%
            </Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {formatDate(item.changedAt)}
            </Typography.Text>
          </div>
          {item.taxGroupName ? (
            <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
              {item.taxGroupName}
            </Typography.Text>
          ) : null}
          {item.reason ? (
            <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
              {item.reason}
            </Typography.Text>
          ) : null}
          {item.invoiceNumber ? (
            <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
              {t('settings.taxHistory.invoiceNumber')}: {item.invoiceNumber}
            </Typography.Text>
          ) : null}
        </div>
      ),
    }));
  }, [taxHistory, formatDate, t]);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settings'), href: '/settings' },
    { title: t('settings.taxHistory.pageTitle') },
  ];

  return (
    <div>
      <AdminPageHeader title={t('settings.taxHistory.pageTitle')} breadcrumbs={breadcrumbs} />
      <Card title={t('settings.taxHistory.cardTitle')}>
        <Typography.Paragraph type="secondary">{t('settings.taxHistory.description')}</Typography.Paragraph>
        {isLoading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : isError ? (
          <Typography.Text type="danger">{t('settings.taxHistory.loadFailed')}</Typography.Text>
        ) : timelineItems.length === 0 ? (
          <Empty description={t('settings.taxHistory.empty')} />
        ) : (
          <Timeline items={timelineItems} />
        )}
      </Card>
    </div>
  );
}
