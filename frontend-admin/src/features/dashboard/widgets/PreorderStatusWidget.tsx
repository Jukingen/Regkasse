'use client';

import { useQuery } from '@tanstack/react-query';
import { Button, Col, Row, Statistic } from 'antd';
import Link from 'next/link';

import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import {
  adminPreorderQueryKeys,
  fetchAdminPreorderStats,
} from '@/features/orders/api/preordersApi';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { PERMISSIONS } from '@/shared/auth/permissions';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

export function PreorderStatusWidget({ title, dragHandleProps, onRefresh }: Props) {
  const { t } = useI18n();
  const { isAuthorized } = useAuthorizationGate({ requiredPermission: PERMISSIONS.ORDER_VIEW });

  const query = useQuery({
    queryKey: adminPreorderQueryKeys.stats,
    queryFn: fetchAdminPreorderStats,
    enabled: isAuthorized,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
    refetchOnWindowFocus: true,
  });

  const handleRefresh = () => {
    void query.refetch();
    onRefresh?.();
  };

  const stats = query.data;

  return (
    <WidgetShell
      title={t('dashboard.preorderWidget.title', { defaultValue: title })}
      dragHandleProps={dragHandleProps}
      refreshing={query.isFetching}
      onRefresh={handleRefresh}>
      {query.isError ? (
        <div>{t('dashboard.preorderWidget.loadFailed')}</div>
      ) : (
      <Row gutter={16}>
        <Col span={8}>
          <Statistic title={t('dashboard.preorderWidget.pending')} value={stats?.pending ?? 0} />
        </Col>
        <Col span={8}>
          <Statistic title={t('dashboard.preorderWidget.ready')} value={stats?.ready ?? 0} />
        </Col>
        <Col span={8}>
          <Statistic title={t('dashboard.preorderWidget.collected')} value={stats?.collected ?? 0} />
        </Col>
      </Row>
      )}
      <div style={{ marginTop: 16 }}>
        <Link href="/orders/preorders">
          <Button type="link" style={{ padding: 0 }}>
            {t('dashboard.preorderWidget.openList')}
          </Button>
        </Link>
      </div>
    </WidgetShell>
  );
}
