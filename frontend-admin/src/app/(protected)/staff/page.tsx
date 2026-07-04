'use client';

import React from 'react';
import Link from 'next/link';
import { Alert, Card, Col, Row, Typography } from 'antd';
import {
    TeamOutlined,
    BarChartOutlined,
    ClockCircleOutlined,
} from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
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

export default function StaffHubPage() {
    const { t } = useI18n();
    const { user } = useAuth();
    const permissions = user?.permissions ?? [];

    const cards: HubCard[] = [
        {
            titleKey: 'staff:hub.cardListTitle',
            descriptionKey: 'staff:hub.cardListDescription',
            href: '/staff/list',
            icon: <TeamOutlined style={{ fontSize: 28, color: '#1677ff' }} />,
            permission: PERMISSIONS.USER_VIEW,
        },
        {
            titleKey: 'staff:hub.cardPerformanceTitle',
            descriptionKey: 'staff:hub.cardPerformanceDescription',
            href: '/staff/performance',
            icon: <BarChartOutlined style={{ fontSize: 28, color: '#722ed1' }} />,
            permission: PERMISSIONS.REPORT_VIEW,
        },
        {
            titleKey: 'staff:hub.cardShiftsTitle',
            descriptionKey: 'staff:hub.cardShiftsDescription',
            href: '/staff/shifts',
            icon: <ClockCircleOutlined style={{ fontSize: 28, color: '#13c2c2' }} />,
            permission: PERMISSIONS.SHIFT_VIEW,
        },
    ];

    const visibleCards = cards.filter((card) => isMenuItemAllowed(card.href, permissions));

    if (visibleCards.length === 0) {
        return (
            <AdminPageShell>
                <AdminPageHeader
                    title={t('staff:hub.pageTitle')}
                    breadcrumbs={[adminOverviewCrumb(t), { title: t('staff:hub.pageTitle') }]}
                />
                <Alert
                    type="warning"
                    showIcon
                    title={t('staff:hub.accessDeniedTitle')}
                    description={t('staff:hub.accessDeniedDescription')}
                />
            </AdminPageShell>
        );
    }

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('staff:hub.pageTitle')}
                breadcrumbs={[adminOverviewCrumb(t), { title: t('staff:hub.pageTitle') }]}
            />
            <Typography.Paragraph type="secondary">{t('staff:hub.intro')}</Typography.Paragraph>
            <Row gutter={[16, 16]}>
                {visibleCards.map((card) => (
                    <Col xs={24} sm={12} lg={8} key={card.href}>
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
