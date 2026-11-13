'use client';

import { DeleteOutlined, DownloadOutlined } from '@ant-design/icons';
import { Alert, Button, Card, List, Space, Tag, Typography } from 'antd';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { PageSkeleton } from '@/components/Skeleton';
import { DataDeletionRequestModal } from '@/features/data-management/components/DataDeletionRequestModal';
import { DataRetentionPolicyCard } from '@/features/data-management/components/DataRetentionPolicyCard';
import { useAccountManagement } from '@/hooks/useAccountManagement';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

const { Paragraph, Text } = Typography;

export default function AccountManagementPage() {
  const { t, formatLocale } = useI18n();
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.BACKUP_MANAGE,
  });
  const {
    tenant,
    tenantId,
    licenseStatus,
    inventory,
    canExport,
    canRequestClosure,
    requestDataExport,
    requestAccountClosure,
    isExporting,
    isLoading,
    isSummaryError,
    closureModalOpen,
    setClosureModalOpen,
  } = useAccountManagement();

  const isLocked = licenseStatus?.state === 'Locked' || licenseStatus?.state === 'Archived';
  const numberFmt = new Intl.NumberFormat(formatLocale);

  const pageTitle = t('dataManagement.account.pageTitle');
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: pageTitle },
  ];

  if (!isAuthorized) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
        <AdminPageHeader title={pageTitle} breadcrumbs={breadcrumbs} />
        <Alert
          type="error"
          showIcon
          title={t('tenants.accessDenied.title')}
          description={t('tenants.accessDenied.body')}
        />
      </div>
    );
  }

  if (!tenant.isRealTenantSlug || !tenantId) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
        <AdminPageHeader title={pageTitle} breadcrumbs={breadcrumbs} />
        <Alert type="warning" showIcon title={t('dataManagement.noTenantContext')} />
      </div>
    );
  }

  if (isLoading) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
        <AdminPageHeader title={pageTitle} breadcrumbs={breadcrumbs} />
        <PageSkeleton widgets={3} />
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24, maxWidth: 896 }}>
      <AdminPageHeader title={pageTitle} breadcrumbs={breadcrumbs} />

      {isSummaryError ? (
        <Alert type="error" showIcon title={t('dataManagement.loadFailed')} />
      ) : null}

      {isLocked ? (
        <Alert
          type="error"
          showIcon
          title={t('dataManagement.account.licenseExpiredTitle')}
          description={
            <div>
              <Paragraph style={{ marginBottom: 8 }}>
                {t('dataManagement.account.licenseExpiredBody')}
              </Paragraph>
              <Text type="secondary">{t('dataManagement.account.licenseExpiredRksv')}</Text>
            </div>
          }
        />
      ) : null}

      <Card
        title={t('dataManagement.account.exportTitle')}
        extra={<Tag color="green">{t('dataManagement.account.gdprTag')}</Tag>}
      >
        <Paragraph>{t('dataManagement.account.exportDescription')}</Paragraph>
        <List
          size="small"
          style={{ marginBottom: 16 }}
          dataSource={[
            {
              key: 'products',
              label: t('dataManagement.account.inventory.products'),
              tag: (
                <Tag color="blue">
                  {t('dataManagement.account.entries', {
                    count: numberFmt.format(inventory.productsAndCategories),
                  })}
                </Tag>
              ),
            },
            {
              key: 'customers',
              label: t('dataManagement.account.inventory.customers'),
              tag: (
                <Tag color="blue">
                  {t('dataManagement.account.entries', {
                    count: numberFmt.format(inventory.customers),
                  })}
                </Tag>
              ),
            },
            {
              key: 'transactions',
              label: t('dataManagement.account.inventory.transactions'),
              tag: (
                <Tag color="blue">
                  {t('dataManagement.account.entries', {
                    count: numberFmt.format(inventory.transactions),
                  })}
                </Tag>
              ),
            },
            {
              key: 'rksv',
              label: t('dataManagement.account.inventory.rksv'),
              tag: <Tag color="orange">{t('dataManagement.account.rksvRetentionTag')}</Tag>,
            },
          ]}
          renderItem={(item) => (
            <List.Item>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  width: '100%',
                  gap: 12,
                }}
              >
                <span>{item.label}</span>
                {item.tag}
              </div>
            </List.Item>
          )}
        />
        <Space wrap>
          <Button
            type="primary"
            icon={<DownloadOutlined />}
            onClick={() => void requestDataExport()}
            loading={isExporting}
            disabled={!canExport}
          >
            {t('dataManagement.account.exportButton')}
          </Button>
          <Text type="secondary">{t('dataManagement.account.exportHint')}</Text>
        </Space>
      </Card>

      <Card
        title={t('dataManagement.account.closureTitle')}
        extra={<Tag color="red">{t('dataManagement.account.irreversibleTag')}</Tag>}
      >
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('dataManagement.account.closureWarningTitle')}
          description={
            <ul style={{ margin: '8px 0 0', paddingLeft: 20 }}>
              <li>{t('dataManagement.account.closureBullets.login')}</li>
              <li>{t('dataManagement.account.closureBullets.nonRksv')}</li>
              <li>{t('dataManagement.account.closureBullets.rksv')}</li>
              <li>{t('dataManagement.account.closureBullets.irreversible')}</li>
            </ul>
          }
        />

        <div
          style={{
            padding: 16,
            background: 'var(--ant-color-fill-quaternary, rgba(0,0,0,0.02))',
            borderRadius: 8,
          }}
        >
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 16,
              flexWrap: 'wrap',
            }}
          >
            <div>
              <div style={{ fontWeight: 600 }}>{t('dataManagement.account.whatHappensTitle')}</div>
              <div style={{ marginTop: 8 }}>
                <Text type="secondary" style={{ display: 'block' }}>
                  {t('dataManagement.account.whatHappens.products')}
                </Text>
                <Text type="secondary" style={{ display: 'block' }}>
                  {t('dataManagement.account.whatHappens.rksv')}
                </Text>
                <Text type="secondary" style={{ display: 'block' }}>
                  {t('dataManagement.account.whatHappens.users')}
                </Text>
              </div>
              {!canRequestClosure ? (
                <Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
                  {t('dataManagement.deleteWarning')}
                </Text>
              ) : null}
            </div>
            <Button
              danger
              icon={<DeleteOutlined />}
              onClick={requestAccountClosure}
              disabled={!canRequestClosure}
            >
              {t('dataManagement.account.closureButton')}
            </Button>
          </div>
        </div>
      </Card>

      <DataRetentionPolicyCard />

      <DataDeletionRequestModal
        tenantId={tenantId}
        open={closureModalOpen}
        onClose={() => setClosureModalOpen(false)}
      />
    </div>
  );
}
