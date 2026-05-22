'use client';

import React from 'react';
import { Tag } from 'antd';

import type { UnifiedAdminUserRow } from '@/features/users/types/unifiedAdminUserRow';
import { useI18n } from '@/i18n';

export type UserTypeBadgeProps = {
    row: UnifiedAdminUserRow;
};

/** Distinguishes platform operators vs mandant membership rows in the unified admin user list. */
export function UserTypeBadge({ row }: UserTypeBadgeProps) {
    const { t } = useI18n();

    if (row.kind === 'platform') {
        if (row.role === 'SuperAdmin') {
            return <Tag color="purple">{t('users.unified.typeBadgePlatformAdmin')}</Tag>;
        }
        return <Tag color="blue">{t('users.unified.typeBadgePlatform')}</Tag>;
    }

    return <Tag color="green">{t('users.unified.typeBadgeTenant')}</Tag>;
}
