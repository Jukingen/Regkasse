'use client';

import React from 'react';
import { Card, Skeleton } from 'antd';
import { useLazyWhenVisible } from '@/components/ui/LazyWhenVisible';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { AppPermissions } from '@/shared/auth/permissions';
import { MonatsbelegWidget } from '@/features/dashboard/components/MonatsbelegWidget';
import { RksvReminderWidget } from '@/features/rksv/components/RksvReminderWidget';

type DashboardMonatsbelegSectionProps = {
    enabled: boolean;
};

/** Defers Monatsbeleg widget fetch until the section scrolls into view. Requires cash_register.view. */
export function DashboardMonatsbelegSection({ enabled }: DashboardMonatsbelegSectionProps) {
    const { isAuthorized: canSeeRksvReminder } = useAuthorizationGate({
        requiredPermission: AppPermissions.CashRegisterView,
    });
    const sectionEnabled = enabled && canSeeRksvReminder;
    const { ref, visible } = useLazyWhenVisible(sectionEnabled);

    if (!sectionEnabled) return null;

    return (
        <div ref={ref} style={{ marginBottom: 24 }}>
            {!visible ? (
                <Card>
                    <Skeleton active paragraph={{ rows: 4 }} />
                </Card>
            ) : (
                <>
                    <RksvReminderWidget />
                    <MonatsbelegWidget enabled={sectionEnabled && visible} />
                </>
            )}
        </div>
    );
}
