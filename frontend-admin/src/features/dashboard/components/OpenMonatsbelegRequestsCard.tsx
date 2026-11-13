'use client';

import { Card, Statistic } from 'antd';
import Link from 'next/link';

import { useMonatsbelegManagerContactRequests } from '@/features/dashboard/hooks/useMonatsbelegManagerContactRequests';
import { useI18n } from '@/i18n/I18nProvider';

export function OpenMonatsbelegRequestsCard() {
  const { t } = useI18n();
  const query = useMonatsbelegManagerContactRequests();
  const count = query.data ?? 0;

  return (
    <Card
      style={{ marginBottom: 16 }}
      title={t('dashboard.manager.openMonatsbelegRequests.title')}
      extra={
        <Link href="/rksv/monatsbelege" data-testid="open-monatsbeleg-requests-cta">
          {t('dashboard.manager.openMonatsbelegRequests.cta')}
        </Link>
      }
      data-testid="open-monatsbeleg-requests-card">
      <Statistic
        title={t('dashboard.manager.openMonatsbelegRequests.count')}
        value={count}
        loading={query.isLoading}
      />
      {count === 0 && !query.isLoading ? (
        <p style={{ margin: '8px 0 0', color: '#64748b' }} data-testid="open-monatsbeleg-requests-empty">
          {t('dashboard.manager.openMonatsbelegRequests.empty')}
        </p>
      ) : null}
    </Card>
  );
}
