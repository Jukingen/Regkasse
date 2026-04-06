'use client';

// Dashboard shortcuts for common shift-lead operations; permissions match target routes.

import React, { useMemo } from 'react';
import Link from 'next/link';
import { Card, Col, Row, Typography } from 'antd';
import { ArrowRightOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n/I18nProvider';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { isAdminInventoryNavEnabled } from '@/shared/config/adminInventoryNavUi';

const OPS_CENTER_ANY: readonly string[] = [
  PERMISSIONS.SALE_VIEW,
  PERMISSIONS.REPORT_VIEW,
  PERMISSIONS.TSE_SIGN,
  PERMISSIONS.RECEIPT_REPRINT,
  PERMISSIONS.REPORT_EXPORT,
];

type LinkDef = { href: string; labelKey: string; visible: boolean };

export function HospitalityQuickLinksCard() {
  const { t } = useI18n();
  const { hasPermission, hasAnyPermission } = usePermissions();
  const inventoryNavEnabled = isAdminInventoryNavEnabled();

  const links: LinkDef[] = useMemo(
    () => [
      { href: '/reporting', labelKey: 'adminShell.hospitalityHub.linkReporting', visible: hasPermission(PERMISSIONS.REPORT_VIEW) },
      { href: '/tagesabschluss', labelKey: 'adminShell.hospitalityHub.linkTagesabschluss', visible: hasPermission(PERMISSIONS.TSE_SIGN) },
      {
        href: '/operations-center',
        labelKey: 'adminShell.hospitalityHub.linkOperations',
        visible: hasAnyPermission([...OPS_CENTER_ANY]),
      },
      { href: '/receipts', labelKey: 'adminShell.hospitalityHub.linkReceipts', visible: hasPermission(PERMISSIONS.SALE_VIEW) },
      {
        href: '/inventory',
        labelKey: 'adminShell.hospitalityHub.linkInventory',
        visible: inventoryNavEnabled && hasPermission(PERMISSIONS.INVENTORY_VIEW),
      },
      { href: '/pricing-rules', labelKey: 'adminShell.hospitalityHub.linkPricing', visible: hasPermission(PERMISSIONS.PRODUCT_VIEW) },
      { href: '/settings/payment-methods', labelKey: 'adminShell.hospitalityHub.linkPaymentsConfig', visible: hasPermission(PERMISSIONS.SETTINGS_VIEW) },
      { href: '/tables', labelKey: 'adminShell.hospitalityHub.linkTables', visible: hasPermission(PERMISSIONS.TABLE_VIEW) },
      { href: '/rksv/status', labelKey: 'adminShell.hospitalityHub.linkRksvStatus', visible: hasPermission(PERMISSIONS.SETTINGS_VIEW) },
    ],
    [hasPermission, hasAnyPermission, inventoryNavEnabled],
  );

  const visibleLinks = links.filter((l) => l.visible);
  if (visibleLinks.length === 0) return null;

  return (
    <Card title={t('adminShell.hospitalityHub.title')} style={{ marginBottom: 24 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {t('adminShell.hospitalityHub.subtitle')}
      </Typography.Paragraph>
      <Row gutter={[16, 16]}>
        {visibleLinks.map((l) => (
          <Col xs={24} sm={12} md={8} lg={6} key={l.href}>
            <Link
              href={l.href}
              style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontWeight: 500 }}
            >
              <span>{t(l.labelKey)}</span>
              <ArrowRightOutlined style={{ opacity: 0.55 }} />
            </Link>
          </Col>
        ))}
      </Row>
    </Card>
  );
}
