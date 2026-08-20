'use client';

import { ArrowRightOutlined, ClockCircleOutlined } from '@ant-design/icons';
import { Card, Col, Empty, Row, Typography } from 'antd';
import Link from 'next/link';
import React, { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useRecentAdminMenuPaths } from '@/features/dashboard/hooks/useRecentAdminMenuPaths';
import { DASHBOARD_HANDY_TOOL_HREFS, filterAccessibleHandyToolHrefs } from '@/features/dashboard/utils/dashboardHandyTools';
import { getSidebarLabelKeyForPath } from '@/features/dashboard/utils/recentAdminMenuPaths';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { canAccessPath } from '@/shared/auth/canAccessPath';

function ShortcutLink({ href, label }: { href: string; label: string }) {
  return (
    <Link
      href={href}
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        fontWeight: 500,
        gap: 8,
      }}
    >
      <span>{label}</span>
      <ArrowRightOutlined style={{ opacity: 0.55 }} aria-hidden />
    </Link>
  );
}

export function DashboardHandyLinksCard() {
  const { t } = useI18n();
  const { user } = useAuth();
  const { userPermissions } = usePermissions();
  const recentPaths = useRecentAdminMenuPaths();
  const superAdmin = isSuperAdmin(user?.role);

  const toolHrefs = useMemo(
    () =>
      filterAccessibleHandyToolHrefs(
        DASHBOARD_HANDY_TOOL_HREFS,
        (href) => superAdmin || canAccessPath(href, userPermissions)
      ),
    [superAdmin, userPermissions]
  );

  const recentVisible = useMemo(
    () => recentPaths.filter((href) => superAdmin || canAccessPath(href, userPermissions)),
    [recentPaths, superAdmin, userPermissions]
  );

  if (toolHrefs.length === 0 && recentVisible.length === 0) {
    return null;
  }

  const labelFor = (href: string): string => {
    const key = getSidebarLabelKeyForPath(href);
    return key ? t(key) : href;
  };

  return (
    <Card title={t('dashboard.handyLinks.title')} style={{ marginTop: 24 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {t('dashboard.handyLinks.subtitle')}
      </Typography.Paragraph>

      {toolHrefs.length > 0 ? (
        <Row gutter={[16, 16]} style={{ marginBottom: recentVisible.length > 0 ? 8 : 0 }}>
          {toolHrefs.map((href) => (
            <Col xs={24} sm={12} md={8} lg={6} key={href}>
              <ShortcutLink href={href} label={labelFor(href)} />
            </Col>
          ))}
        </Row>
      ) : null}

      <Typography.Title level={5} style={{ marginTop: toolHrefs.length > 0 ? 20 : 0, marginBottom: 12 }}>
        <ClockCircleOutlined style={{ marginRight: 8 }} aria-hidden />
        {t('dashboard.handyLinks.recentTitle')}
      </Typography.Title>

      {recentVisible.length === 0 ? (
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description={t('dashboard.handyLinks.recentEmpty')}
        />
      ) : (
        <Row gutter={[16, 16]}>
          {recentVisible.map((href) => (
            <Col xs={24} sm={12} md={8} lg={6} key={href}>
              <ShortcutLink href={href} label={labelFor(href)} />
            </Col>
          ))}
        </Row>
      )}
    </Card>
  );
}
