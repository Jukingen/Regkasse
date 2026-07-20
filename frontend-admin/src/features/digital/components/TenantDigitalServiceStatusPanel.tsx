'use client';

import { GlobalOutlined, MobileOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, Row, Space, Tag } from 'antd';
import Link from 'next/link';
import { CardSkeleton } from '@/components/Skeleton';
import { useTenantDigitalService } from '@/features/digital-services/hooks/useTenantDigitalServices';
import {
  canCreateDigitalApp,
  canCreateDigitalWeb,
  canPreviewDigitalApp,
  canPreviewDigitalWeb,
} from '@/features/digital/digitalServicePermissions';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';

type TenantDigitalServiceStatusPanelProps = {
  tenantId: string;
  /** When true, shows manage CTAs for users with create permission. */
  showManageActions?: boolean;
};

/**
 * Status cards for website / app availability.
 * Generate CTAs only when the user has create permission (Super Admin).
 */
export function TenantDigitalServiceStatusPanel({
  tenantId,
  showManageActions = true,
}: TenantDigitalServiceStatusPanelProps) {
  const { t } = useI18n();
  const { user, isSuperAdmin } = usePermissions();
  const userPerms = user ? { permissions: user.permissions } : null;
  const canCreateWeb = canCreateDigitalWeb(userPerms, isSuperAdmin);
  const canCreateApp = canCreateDigitalApp(userPerms, isSuperAdmin);
  const canPreviewWeb = canPreviewDigitalWeb(userPerms, isSuperAdmin);
  const canPreviewApp = canPreviewDigitalApp(userPerms, isSuperAdmin);
  const { data, isLoading, isError } = useTenantDigitalService(tenantId);

  if (isLoading) {
    return <CardSkeleton count={2} />;
  }

  if (isError || !data) {
    return <Alert type="error" showIcon message={t('tenants.digitalServices.statusLoadFailed')} />;
  }

  const webActive = data.website.isAvailable;
  const appActive = data.app.isAvailable;

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={12}>
        <Card
          title={
            <Space>
              <GlobalOutlined />
              {t('tenants.digitalServices.websiteCardTitle')}
              <Tag color={webActive ? 'success' : 'warning'}>
                {webActive
                  ? t('tenants.digitalServices.statusActive')
                  : t('tenants.digitalServices.statusInactive')}
              </Tag>
            </Space>
          }
        >
          {webActive ? (
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
              <Alert
                type="success"
                showIcon
                message={t('tenants.digitalServices.webActiveTitle')}
                description={t('tenants.digitalServices.webActiveBody')}
              />
              {showManageActions ? (
                <Space wrap>
                  {canPreviewWeb ? (
                    <Link href={`/tenant/${tenantId}/customize`}>
                      <Button type="primary">{t('tenants.digitalServices.manageWebsite')}</Button>
                    </Link>
                  ) : null}
                  <Link href={`/tenant/${tenantId}/domain`}>
                    <Button>{t('tenants.domainManagement.openAction')}</Button>
                  </Link>
                  {canCreateWeb ? (
                    <Button
                      onClick={() =>
                        document.getElementById('digital-generate-web')?.scrollIntoView({
                          behavior: 'smooth',
                          block: 'start',
                        })
                      }
                    >
                      {t('tenants.digitalServices.generateWebsite')}
                    </Button>
                  ) : null}
                </Space>
              ) : null}
            </Space>
          ) : (
            <Alert
              type="warning"
              showIcon
              message={t('tenants.digitalServices.webInactiveTitle')}
              description={
                !data.website.isAvailable
                  ? t('tenants.digitalServices.webInactiveBody')
                  : t('tenants.digitalServices.webNoPermissionBody')
              }
            />
          )}
        </Card>
      </Col>

      <Col xs={24} lg={12}>
        <Card
          title={
            <Space>
              <MobileOutlined />
              {t('tenants.digitalServices.appCardTitle')}
              <Tag color={appActive ? 'success' : 'warning'}>
                {appActive
                  ? t('tenants.digitalServices.statusActive')
                  : t('tenants.digitalServices.statusInactive')}
              </Tag>
            </Space>
          }
        >
          {appActive ? (
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
              <Alert
                type="success"
                showIcon
                message={t('tenants.digitalServices.appActiveTitle')}
                description={t('tenants.digitalServices.appActiveBody')}
              />
              {showManageActions && canCreateApp ? (
                <Button
                  type="primary"
                  onClick={() =>
                    document.getElementById('digital-generate-app')?.scrollIntoView({
                      behavior: 'smooth',
                      block: 'start',
                    })
                  }
                >
                  {t('tenants.digitalServices.manageApp')}
                </Button>
              ) : null}
              {showManageActions && !canCreateApp && canPreviewApp ? (
                <Alert
                  type="info"
                  showIcon
                  message={t('tenants.digitalServices.managerInfoTitle')}
                  description={t('tenants.digitalServices.managerInfoBody')}
                />
              ) : null}
            </Space>
          ) : (
            <Alert
              type="warning"
              showIcon
              message={t('tenants.digitalServices.appInactiveTitle')}
              description={
                !data.app.isAvailable
                  ? t('tenants.digitalServices.appInactiveBody')
                  : t('tenants.digitalServices.appNoPermissionBody')
              }
            />
          )}
        </Card>
      </Col>
    </Row>
  );
}
