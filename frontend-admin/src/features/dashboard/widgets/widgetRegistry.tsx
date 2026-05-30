'use client';

import React from 'react';
import { DASHBOARD_WIDGET_IDS } from '@/features/dashboard/types';
import { TodaySalesWidget } from '@/features/dashboard/widgets/TodaySalesWidget';
import { ActiveCashRegistersWidget } from '@/features/dashboard/widgets/ActiveCashRegistersWidget';
import { LowStockAlertsWidget } from '@/features/dashboard/widgets/LowStockAlertsWidget';
import { RecentUsersWidget } from '@/features/dashboard/widgets/RecentUsersWidget';
import { LicenseExpiryWidget } from '@/features/dashboard/widgets/LicenseExpiryWidget';
import { FinanzOnlineStatusWidget } from '@/features/dashboard/widgets/FinanzOnlineStatusWidget';
import { TopSellingProductsWidget } from '@/features/dashboard/widgets/TopSellingProductsWidget';
import { PaymentTrendWidget, parsePaymentTrendPeriod } from '@/features/dashboard/widgets/PaymentTrendWidget';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';

export type DashboardWidgetRenderProps = {
    title: string;
    dragHandleProps?: WidgetShellProps['dragHandleProps'];
    settings?: Record<string, unknown> | null;
    onSettingsChange?: (settings: Record<string, unknown>) => void;
};

export function renderDashboardWidget(
    widgetId: string,
    props: DashboardWidgetRenderProps,
): React.ReactNode {
    const common = {
        title: props.title,
        dragHandleProps: props.dragHandleProps,
    };

    switch (widgetId) {
        case DASHBOARD_WIDGET_IDS.todaySales:
            return <TodaySalesWidget {...common} />;
        case DASHBOARD_WIDGET_IDS.activeCashRegisters:
            return <ActiveCashRegistersWidget {...common} />;
        case DASHBOARD_WIDGET_IDS.lowStockAlerts:
            return <LowStockAlertsWidget {...common} />;
        case DASHBOARD_WIDGET_IDS.recentUsers:
            return <RecentUsersWidget {...common} />;
        case DASHBOARD_WIDGET_IDS.licenseExpiry:
            return <LicenseExpiryWidget {...common} />;
        case DASHBOARD_WIDGET_IDS.finanzOnlineStatus:
            return <FinanzOnlineStatusWidget {...common} />;
        case DASHBOARD_WIDGET_IDS.topSellingProducts: {
            const period =
                props.settings?.period === 'week' ? ('week' as const) : ('today' as const);
            return (
                <TopSellingProductsWidget
                    {...common}
                    period={period}
                    onPeriodChange={(p) => props.onSettingsChange?.({ period: p })}
                />
            );
        }
        case DASHBOARD_WIDGET_IDS.paymentTrends: {
            const period = parsePaymentTrendPeriod(props.settings?.period);
            return (
                <PaymentTrendWidget
                    {...common}
                    period={period}
                    onPeriodChange={(p) => props.onSettingsChange?.({ period: p })}
                />
            );
        }
        default:
            return null;
    }
}
