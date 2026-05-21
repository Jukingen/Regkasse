'use client';

import React from 'react';
import { Tag } from 'antd';

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

export type UserRoleBadgeProps = {
    role: string;
    isOwner?: boolean;
    /** SuperAdmin platform badge styling */
    platform?: boolean;
};

export function UserRoleBadge({ role, isOwner, platform }: UserRoleBadgeProps) {
    const { t } = useI18n();
    const isCanonical = (CANONICAL_ROLES as readonly string[]).includes(role);
    const label = isCanonical ? t(`users.roles.displayNames.${role}`) : role;
    const color = platform || role === 'SuperAdmin' ? 'purple' : 'gold';

    return (
        <>
            <Tag color={color}>{label}</Tag>
            {isOwner ? <Tag color="geekblue">{t('users.tabs.tenant.ownerBadge')}</Tag> : null}
        </>
    );
}
