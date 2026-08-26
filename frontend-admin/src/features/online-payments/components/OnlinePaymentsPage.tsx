'use client';

import { ReloadOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Tabs } from 'antd';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useOnlinePaymentsList } from '@/features/online-payments/api/onlinePaymentsApi';
import { OnlinePaymentDetailDrawer } from '@/features/online-payments/components/OnlinePaymentDetailDrawer';
import { OnlinePaymentTestConsole } from '@/features/online-payments/components/OnlinePaymentTestConsole';
import { OnlinePaymentsTable } from '@/features/online-payments/components/OnlinePaymentsTable';
import { useI18n } from '@/i18n';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

export function OnlinePaymentsPage() {
  const { t } = useI18n();
  const ts = (key: string) => t(`onlinePayments.${key}`);
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.ONLINE_PAYMENTS_MANAGE);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const listQuery = useOnlinePaymentsList(pageNumber, pageSize, allowed);
  const selected =
    listQuery.data?.items.find((row) => row.id === selectedId) ?? null;

  if (!allowed) {
    return (
      <AdminPageShell>
        <Alert type="error" showIcon title={ts('forbidden')} />
      </AdminPageShell>
    );
  }

  return (
    <>
      <AdminPageHeader
        title={ts('title')}
        breadcrumbs={[
          ADMIN_OVERVIEW_CRUMB,
          { title: t('nav.sales') },
          { title: ts('title') },
        ]}
      />
      <AdminPageShell>
        <Tabs
          destroyOnHidden
          items={[
            {
              key: 'transactions',
              label: ts('tabs.transactions'),
              children: (
                <Card
                  extra={
                    <Button
                      icon={<ReloadOutlined />}
                      onClick={() => void listQuery.refetch()}
                      loading={listQuery.isFetching}
                    >
                      {ts('actions.refresh')}
                    </Button>
                  }
                >
                  {listQuery.isError ? (
                    <Alert type="error" showIcon title={ts('loadError')} />
                  ) : (
                    <OnlinePaymentsTable
                      payments={listQuery.data?.items ?? []}
                      loading={listQuery.isLoading}
                      emptyText={ts('empty')}
                      onViewDetails={(row) => setSelectedId(row.id)}
                      pagination={{
                        current: pageNumber,
                        pageSize,
                        total: listQuery.data?.totalCount ?? 0,
                        showSizeChanger: true,
                        onChange: (p, ps) => {
                          setPageNumber(p);
                          setPageSize(ps);
                        },
                      }}
                    />
                  )}
                </Card>
              ),
            },
            {
              key: 'test-console',
              label: ts('tabs.testConsole'),
              children: (
                <OnlinePaymentTestConsole
                  onPaymentCreated={(row) => setSelectedId(row.id)}
                />
              ),
            },
          ]}
        />
      </AdminPageShell>
      <OnlinePaymentDetailDrawer payment={selected} onClose={() => setSelectedId(null)} />
    </>
  );
}
