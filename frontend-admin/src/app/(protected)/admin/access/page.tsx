'use client';

import React from 'react';
import Link from 'next/link';
import { Alert, Card, Col, Row, Typography } from 'antd';
import {
    TeamOutlined,
    SafetyOutlined,
    AuditOutlined,
    FileSearchOutlined,
} from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { useUsersPolicy } from '@/shared/auth/usersPolicy';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { PERMISSIONS } from '@/shared/auth/permissions';

type HubCard = {
    titleKey: string;
    descriptionKey: string;
    href: string;
    icon: React.ReactNode;
    permission: string;
};

export default function AccessHubPage() {
    const { t } = useI18n();
    const policy = useUsersPolicy();
    const { user } = useAuth();
    const permissions = user?.permissions ?? [];

    const cards: HubCard[] = [
        {
            titleKey: 'access.hub.cardUsersTitle',
            descriptionKey: 'access.hub.cardUsersDescription',
            href: '/admin/users',
            icon: <TeamOutlined style={{ fontSize: 28, color: '#1677ff' }} />,
            permission: PERMISSIONS.USER_VIEW,
        },
        {
            titleKey: 'access.hub.cardRolesTitle',
            descriptionKey: 'access.hub.cardRolesDescription',
            href: '/admin/access/roles',
            icon: <SafetyOutlined style={{ fontSize: 28, color: '#722ed1' }} />,
            permission: PERMISSIONS.ROLE_MANAGE,
        },
        {
            titleKey: 'access.hub.cardMatrixTitle',
            descriptionKey: 'access.hub.cardMatrixDescription',
            href: '/admin/access/matrix',
            icon: <AuditOutlined style={{ fontSize: 28, color: '#13c2c2' }} />,
            permission: PERMISSIONS.ROLE_VIEW,
        },
        {
            titleKey: 'access.hub.cardAuditTitle',
            descriptionKey: 'access.hub.cardAuditDescription',
            href: '/audit-logs',
            icon: <FileSearchOutlined style={{ fontSize: 28, color: '#fa8c16' }} />,
            permission: PERMISSIONS.AUDIT_VIEW,
        },
    ];

    const visibleCards = cards.filter((c) => isMenuItemAllowed(c.href, permissions));

    if (!policy.canView) {
        return (
            <AdminPageShell>
                <AdminPageHeader
                    title={t('access.hub.pageTitle')}
                    breadcrumbs={[
                        adminOverviewCrumb(t),
                        { title: t('access.hub.pageTitle') },
                    ]}
                />
                <Alert
                    type="warning"
                    showIcon
                    title={t('access.hub.accessDeniedTitle')}
                    description={t('access.hub.accessDeniedDescription')}
                />
            </AdminPageShell>
        );
    }

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('access.hub.pageTitle')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('access.hub.pageTitle') },
                ]}
            />
            <Typography.Paragraph type="secondary">{t('access.hub.intro')}</Typography.Paragraph>
            <Row gutter={[16, 16]}>
                {visibleCards.map((card) => (
                    <Col xs={24} sm={12} lg={6} key={card.href}>
                        <Link href={card.href} prefetch={false} style={{ color: 'inherit' }}>
                            <Card hoverable style={{ height: '100%' }}>
                                <div style={{ marginBottom: 12 }}>{card.icon}</div>
                                <Typography.Title level={5} style={{ marginTop: 0 }}>
                                    {t(card.titleKey)}
                                </Typography.Title>
                                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                                    {t(card.descriptionKey)}
                                </Typography.Paragraph>
                            </Card>
                        </Link>
                    </Col>
                ))}
            </Row>
        </AdminPageShell>
    );
}
