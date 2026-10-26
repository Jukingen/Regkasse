'use client';

import { useQuery } from '@tanstack/react-query';
import { Table } from 'antd';
import { useEffect } from 'react';

import { listDemoRequests, type DemoRequestListItem } from '@/features/super-admin/api/demoRequests';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { translateApiError } from '@/lib/api/errorTranslator';

function statusLabel(status: string, t: (key: string) => string): string {
  if (status === 'New') return t('tenants.demoRequests.statusNew');
  if (status === 'Contacted') return t('tenants.demoRequests.statusContacted');
  if (status === 'Closed') return t('tenants.demoRequests.statusClosed');
  return status;
}

export function DemoRequestsPageContent() {
  const { t } = useI18n();
  const notify = useNotify();
  const query = useQuery({
    queryKey: ['admin', 'demo-requests'],
    queryFn: () => listDemoRequests(),
  });

  useEffect(() => {
    if (!query.isError) return;
    notify.error(
      translateApiError(t, query.error, {
        logContext: 'DemoRequests.load',
        fallbackKey: 'tenants.demoRequests.loadError',
      }) || t('tenants.demoRequests.loadError'),
    );
  }, [notify, query.error, query.isError, t]);

  return (
    <>
    <h1>{t('tenants.demoRequests.title')}</h1>
    <Table<DemoRequestListItem>
      rowKey="id"
      loading={query.isLoading}
      dataSource={query.data?.items ?? []}
      locale={{ emptyText: t('tenants.demoRequests.empty') }}
      pagination={false}
      data-testid="demo-requests-table"
      columns={[
        {
          title: t('tenants.demoRequests.columns.name'),
          dataIndex: 'name',
          render: (name: string, row) => (
            <div>
              <div>{name}</div>
              {row.message ? <div>{row.message}</div> : null}
            </div>
          ),
        },
        { title: t('tenants.demoRequests.columns.email'), dataIndex: 'email' },
        { title: t('tenants.demoRequests.columns.company'), dataIndex: 'company' },
        { title: t('tenants.demoRequests.columns.industry'), dataIndex: 'industry' },
        {
          title: t('tenants.demoRequests.columns.status'),
          dataIndex: 'status',
          render: (status: string) => statusLabel(status, t),
        },
        {
          title: t('tenants.demoRequests.columns.created'),
          dataIndex: 'createdAtUtc',
          render: (value: string) =>
            new Intl.DateTimeFormat('de-DE', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)),
        },
      ]}
    />
    </>
  );
}
