'use client';

import {
  BookOutlined,
  CommentOutlined,
  CustomerServiceOutlined,
  MailOutlined,
  PhoneOutlined,
} from '@ant-design/icons';
import { Card, Col, Collapse, Flex, Row, Typography } from 'antd';
import type { ReactNode } from 'react';

import {
  getConfiguredLicenseSupportPhone,
  openLicenseSupportHref,
  resolveLicenseSupportLiveChatTarget,
  resolveLicenseSupportPhoneTarget,
  resolveLicenseSupportTicketTarget,
} from '@/features/license/utils/licenseSupportOptions';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

type SupportTileProps = {
  icon: ReactNode;
  title: string;
  description: string;
  onClick: () => void;
};

function SupportTile({ icon, title, description, onClick }: SupportTileProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={title}
      style={{
        display: 'block',
        width: '100%',
        textAlign: 'left',
        padding: 16,
        borderRadius: 8,
        border: '1px solid rgba(0,0,0,0.08)',
        background: '#fff',
        cursor: 'pointer',
        transition: 'box-shadow 0.2s ease, border-color 0.2s ease',
      }}
      onMouseEnter={(e) => {
        e.currentTarget.style.boxShadow = '0 4px 12px rgba(0,0,0,0.08)';
        e.currentTarget.style.borderColor = 'rgba(0,0,0,0.16)';
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.boxShadow = 'none';
        e.currentTarget.style.borderColor = 'rgba(0,0,0,0.08)';
      }}
    >
      <div style={{ fontSize: 22, lineHeight: 1 }}>{icon}</div>
      <Typography.Text strong style={{ display: 'block', marginTop: 8 }}>
        {title}
      </Typography.Text>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {description}
      </Typography.Text>
    </button>
  );
}

/**
 * Quick license support options: chat/mail, ticket, FAQ modal, phone.
 */
export function LicenseSupportOptionsCard() {
  const { t } = useI18n();
  const { modal } = useAntdApp();
  const tenant = useCurrentTenant();
  const { isAuthorized: canView } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_VIEW,
  });

  if (!canView) return null;
  if (!tenant.isRealTenantSlug || tenant.isSuperAdminPlatformMode) return null;

  const phoneDisplay = getConfiguredLicenseSupportPhone();

  const openFaq = () => {
    modal.info({
      title: t('dashboard.widgets.licenseSupportOptions.faq.modalTitle'),
      width: 560,
      okText: t('dashboard.widgets.licenseSupportOptions.faq.modalClose'),
      content: (
        <Collapse
          bordered={false}
          items={[
            {
              key: 'renew',
              label: t('dashboard.widgets.licenseSupportOptions.faq.items.renew.q'),
              children: t('dashboard.widgets.licenseSupportOptions.faq.items.renew.a'),
            },
            {
              key: 'grace',
              label: t('dashboard.widgets.licenseSupportOptions.faq.items.grace.q'),
              children: t('dashboard.widgets.licenseSupportOptions.faq.items.grace.a'),
            },
            {
              key: 'locked',
              label: t('dashboard.widgets.licenseSupportOptions.faq.items.locked.q'),
              children: t('dashboard.widgets.licenseSupportOptions.faq.items.locked.a'),
            },
            {
              key: 'key',
              label: t('dashboard.widgets.licenseSupportOptions.faq.items.key.q'),
              children: t('dashboard.widgets.licenseSupportOptions.faq.items.key.a'),
            },
          ]}
        />
      ),
    });
  };

  return (
    <Card
      size="small"
      title={
        <Flex align="center" gap={8}>
          <CustomerServiceOutlined aria-hidden />
          <span>{t('dashboard.widgets.licenseSupportOptions.title')}</span>
        </Flex>
      }
      style={{ marginBottom: 16 }}
      styles={{ body: { paddingBlock: 16 } }}
    >
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('dashboard.widgets.licenseSupportOptions.subtitle')}
      </Typography.Paragraph>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12}>
          <SupportTile
            icon={<CommentOutlined style={{ color: '#1677ff' }} aria-hidden />}
            title={t('dashboard.widgets.licenseSupportOptions.liveChat.title')}
            description={t('dashboard.widgets.licenseSupportOptions.liveChat.description')}
            onClick={() => openLicenseSupportHref(resolveLicenseSupportLiveChatTarget())}
          />
        </Col>
        <Col xs={24} sm={12}>
          <SupportTile
            icon={<MailOutlined style={{ color: '#1677ff' }} aria-hidden />}
            title={t('dashboard.widgets.licenseSupportOptions.ticket.title')}
            description={t('dashboard.widgets.licenseSupportOptions.ticket.description')}
            onClick={() => openLicenseSupportHref(resolveLicenseSupportTicketTarget())}
          />
        </Col>
        <Col xs={24} sm={12}>
          <SupportTile
            icon={<BookOutlined style={{ color: '#1677ff' }} aria-hidden />}
            title={t('dashboard.widgets.licenseSupportOptions.faq.title')}
            description={t('dashboard.widgets.licenseSupportOptions.faq.description')}
            onClick={openFaq}
          />
        </Col>
        <Col xs={24} sm={12}>
          <SupportTile
            icon={<PhoneOutlined style={{ color: '#1677ff' }} aria-hidden />}
            title={t('dashboard.widgets.licenseSupportOptions.phone.title')}
            description={t('dashboard.widgets.licenseSupportOptions.phone.description', {
              phone: phoneDisplay,
            })}
            onClick={() => openLicenseSupportHref(resolveLicenseSupportPhoneTarget())}
          />
        </Col>
      </Row>
    </Card>
  );
}
