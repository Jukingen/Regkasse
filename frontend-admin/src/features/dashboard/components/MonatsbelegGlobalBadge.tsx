'use client';

import React from 'react';
import Link from 'next/link';
import { Badge, Tooltip } from 'antd';
import { WarningOutlined } from '@ant-design/icons';
import { useAdminMonatsbelegOverview } from '@/features/dashboard/hooks/useAdminMonatsbelegOverview';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

/**
 * Header badge: count of cash registers whose Monatsbeleg status is `red` (überfällig).
 */
export function MonatsbelegGlobalBadge() {
    const { hasPermission } = usePermissions();
    /** Same inventory GET as dashboard table (requires cashregister.view on API). */
    const canSee = hasPermission(PERMISSIONS.CASHREGISTER_VIEW);

    const { redCount, registersLoading } = useAdminMonatsbelegOverview(canSee);

    if (!canSee || redCount <= 0) {
        return null;
    }

    return (
        <Tooltip title="Monatsbeleg überfällig — mindestens eine Kasse erfordert sofortige Bearbeitung (RKSV).">
            <Link href="/dashboard" aria-label={`Monatsbeleg Warnungen: ${redCount}`}>
                <Badge count={redCount} size="small" offset={[-2, 2]} style={{ backgroundColor: '#cf1322' }}>
                    <WarningOutlined style={{ fontSize: 20, color: registersLoading ? '#bfbfbf' : '#cf1322' }} />
                </Badge>
            </Link>
        </Tooltip>
    );
}
