'use client';

import { useState } from 'react';
import { GlobalOutlined, MobileOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, Modal, Row, Space, Tag } from 'antd';
import Link from 'next/link';
import { CardSkeleton } from '@/components/Skeleton';
import {
  canPreviewDigitalApp,
  canPreviewDigitalWeb,
  canRequestDigitalApp,
  canRequestDigitalWeb,
} from '@/features/digital/digitalServicePermissions';
import {
  findPendingRequest,
  useRequestDigitalService,
  useTenantDigitalServiceRequests,
} from '@/features/digital-services/hooks/useDigitalServiceRequests';
import { useTenantDigitalService } from '@/features/digital-services/hooks/useTenantDigitalServices';
import type {
  DigitalProvisionStatus,
  DigitalServiceType,
  TenantDigitalServiceState,
} from '@/features/digital-services/api/tenantDigitalServicesApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';

type ManagerDigitalRequestPanelProps = {
  tenantId: string;
};

function provisionTagColor(status: DigitalProvisionStatus): string {
  switch (status) {
    case 'published':
      return 'success';
    case 'created':
      return 'blue';
    case 'pending':
      return 'processing';
    case 'rejected':
      return 'error';
    default:
      return 'default';
  }
}

/**
 * Mandanten-Admin: view status, preview links, request website/app creation (no generate).
 */
export function ManagerDigitalRequestPanel({ tenantId }: ManagerDigitalRequestPanelProps) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const { user, isSuperAdmin } = usePermissions();
  const userPerms = user ? { permissions: user.permissions } : null;
  const { data, isLoading, isError } = useTenantDigitalService(tenantId);
  const { data: requests } = useTenantDigitalServiceRequests(tenantId);
  const requestMutation = useRequestDigitalService();

  const [requestModalOpen, setRequestModalOpen] = useState(false);
  const [requestType, setRequestType] = useState<DigitalServiceType>('website');

  const canPreviewWeb = canPreviewDigitalWeb(userPerms, isSuperAdmin);
  const canPreviewApp = canPreviewDigitalApp(userPerms, isSuperAdmin);
  const canRequestWeb = canRequestDigitalWeb(userPerms, isSuperAdmin);
  const canRequestApp = canRequestDigitalApp(userPerms, isSuperAdmin);

  const pendingWeb = findPendingRequest(requests, 'website');
  const pendingApp = findPendingRequest(requests, 'app');

  const openRequest = (type: DigitalServiceType) => {
    setRequestType(type);
    setRequestModalOpen(true);
  };

  const handleRequest = async () => {
    try {
      await requestMutation.mutateAsync({ tenantId, serviceType: requestType });
      message.success(t('tenants.digitalServices.requestSuccess'));
      setRequestModalOpen(false);
    } catch (err) {
      message.error(
        err instanceof Error ? err.message : t('tenants.digitalServices.requestFailed'),
      );
    }
  };

  const provisionLabel = (status: DigitalProvisionStatus) => {
    switch (status) {
      case 'pending':
        return t('tenants.digitalServices.statusPendingRequest');
      case 'created':
        return t('tenants.digitalServices.statusProvisionCreated');
      case 'published':
        return t('tenants.digitalServices.statusProvisionPublished');
      case 'rejected':
        return t('tenants.digitalServices.statusProvisionRejected');
      default:
        return t('tenants.digitalServices.statusProvisionNone');
    }
  };

  if (isLoading) {
    return <CardSkeleton count={2} />;
  }

  if (isError || !data) {
    return <Alert type="error" showIcon message={t('tenants.digitalServices.statusLoadFailed')} />;
  }

  const renderCard = (
    type: DigitalServiceType,
    state: TenantDigitalServiceState,
    canPreview: boolean,
    canRequest: boolean,
    pendingFromQueue: boolean,
  ) => {
    const isWebsite = type === 'website';
    const title = isWebsite
      ? t('tenants.digitalServices.websiteCardTitle')
      : t('tenants.digitalServices.appCardTitle');
    const Icon = isWebsite ? GlobalOutlined : MobileOutlined;
    const status: DigitalProvisionStatus =
      pendingFromQueue || state.hasRequest ? 'pending' : state.status;
    const isPending = status === 'pending';
    const isReady = status === 'created' || status === 'published';
    const canRequestNow =
      canRequest && (status === 'none' || status === 'rejected') && !isPending;

    return (
      <Card
        title={
          <Space>
            <Icon />
            {title}
            <Tag color={provisionTagColor(status)}>{provisionLabel(status)}</Tag>
            {!state.isAvailable ? (
              <Tag color="warning">{t('tenants.digitalServices.statusInactive')}</Tag>
            ) : null}
          </Space>
        }
      >
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          {isPending ? (
            <Alert
              type="info"
              showIcon
              message={t('tenants.digitalServices.requestPendingTitle')}
              description={t('tenants.digitalServices.requestPendingBody')}
            />
          ) : isReady ? (
            <Alert
              type="success"
              showIcon
              message={
                isWebsite
                  ? t('tenants.digitalServices.webActiveTitle')
                  : t('tenants.digitalServices.appActiveTitle')
              }
              description={
                state.url
                  ? state.url
                  : isWebsite
                    ? t('tenants.digitalServices.webActiveBody')
                    : t('tenants.digitalServices.appActiveBody')
              }
            />
          ) : status === 'rejected' ? (
            <Alert
              type="warning"
              showIcon
              message={t('tenants.digitalServices.statusProvisionRejected')}
              description={t('tenants.digitalServices.requestHintBody')}
            />
          ) : (
            <Alert
              type="warning"
              showIcon
              message={
                isWebsite
                  ? t('tenants.digitalServices.webInactiveTitle')
                  : t('tenants.digitalServices.appInactiveTitle')
              }
              description={t('tenants.digitalServices.requestHintBody')}
            />
          )}

          <Space wrap>
            {canPreview && isWebsite ? (
              <Link href={`/tenant/${tenantId}/website-preview`}>
                <Button>{t('tenants.digitalServices.previewAction')}</Button>
              </Link>
            ) : null}
            {canPreview && isWebsite && isReady ? (
              <Link href={`/tenant/${tenantId}/orders`}>
                <Button>{t('tenants.digitalServices.ordersAction')}</Button>
              </Link>
            ) : null}
            {isReady && isWebsite ? (
              <Link href={`/tenant/${tenantId}/domain`}>
                <Button>{t('tenants.domainManagement.openAction')}</Button>
              </Link>
            ) : null}
            {canRequestNow ? (
              <Button type="primary" onClick={() => openRequest(type)}>
                {t('tenants.digitalServices.requestAction')}
              </Button>
            ) : null}
          </Space>
        </Space>
      </Card>
    );
  };

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        message={t('tenants.digitalServices.managerInfoTitle')}
        description={t('tenants.digitalServices.managerInfoBody')}
      />
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          {renderCard('website', data.website, canPreviewWeb, canRequestWeb, Boolean(pendingWeb))}
        </Col>
        <Col xs={24} lg={12}>
          {renderCard('app', data.app, canPreviewApp, canRequestApp, Boolean(pendingApp))}
        </Col>
      </Row>

      <Modal
        title={t('tenants.digitalServices.requestModalTitle')}
        open={requestModalOpen}
        onCancel={() => setRequestModalOpen(false)}
        onOk={() => void handleRequest()}
        confirmLoading={requestMutation.isPending}
        okText={t('tenants.digitalServices.requestSubmit')}
        cancelText={t('common.buttons.cancel')}
      >
        <p>
          {requestType === 'website'
            ? t('tenants.digitalServices.requestModalWebsite')
            : t('tenants.digitalServices.requestModalApp')}
        </p>
        <p>{t('tenants.digitalServices.requestModalBody')}</p>
        <p style={{ color: 'var(--ant-color-text-secondary)', fontSize: 12 }}>
          {t('tenants.digitalServices.requestModalSla')}
        </p>
      </Modal>
    </Space>
  );
}
