'use client';

import React from 'react';
import { Tag } from 'antd';
import {
    CalculatorOutlined,
    CoffeeOutlined,
    CrownOutlined,
    ShoppingOutlined,
    UserOutlined,
} from '@ant-design/icons';

import { useI18n } from '@/i18n';

const CANONICAL_ROLES = [
    'SuperAdmin',
    'Manager',
    'Cashier',
    'Waiter',
    'Kitchen',
    'ReportViewer',
    'Accountant',
] as const;

const ROLE_COLORS: Record<string, string> = {
    SuperAdmin: 'red',
    Manager: 'blue',
    Cashier: 'green',
    Accountant: 'orange',
    Waiter: 'cyan',
    Kitchen: 'default',
    ReportViewer: 'default',
};

const ROLE_ICONS: Record<string, React.ReactNode> = {
    SuperAdmin: <CrownOutlined />,
    Manager: <UserOutlined />,
    Cashier: <ShoppingOutlined />,
    Accountant: <CalculatorOutlined />,
    Waiter: <CoffeeOutlined />,
    Kitchen: <UserOutlined />,
    ReportViewer: <UserOutlined />,
};

export type UserRoleBadgeProps = {
    role: string;
    isOwner?: boolean;
    /** @deprecated Prefer role-based colors; kept for callers that still pass it. */
    platform?: boolean;
};

const BADGE_LABEL_KEYS: Partial<Record<(typeof CANONICAL_ROLES)[number], string>> = {
    SuperAdmin: 'users.roles.badgeLabels.SuperAdmin',
    Manager: 'users.roles.badgeLabels.Manager',
    Cashier: 'users.roles.badgeLabels.Cashier',
    Accountant: 'users.roles.badgeLabels.Accountant',
    Waiter: 'users.roles.badgeLabels.Waiter',
    Kitchen: 'users.roles.badgeLabels.Kitchen',
    ReportViewer: 'users.roles.badgeLabels.ReportViewer',
};

export function UserRoleBadge({ role, isOwner }: UserRoleBadgeProps) {
    const { t } = useI18n();
    const isCanonical = (CANONICAL_ROLES as readonly string[]).includes(role);
    const badgeKey = BADGE_LABEL_KEYS[role as (typeof CANONICAL_ROLES)[number]];
    const label =
        isCanonical && badgeKey
            ? t(badgeKey)
            : isCanonical
              ? t(`users.roles.displayNames.${role}` as 'users.roles.displayNames.SuperAdmin')
              : role;
    const color = ROLE_COLORS[role] ?? 'default';
    const icon = ROLE_ICONS[role] ?? <UserOutlined />;

    return (
        <>
            <Tag color={color} icon={icon}>{label}</Tag>
            {isOwner ? <Tag color="geekblue">{t('users.tabs.tenant.ownerBadge')}</Tag> : null}
        </>
    );
}
