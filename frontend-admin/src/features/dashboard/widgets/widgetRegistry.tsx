'use client';

import { Skeleton } from 'antd';
import dynamic from 'next/dynamic';
import React from 'react';

import { DataRetentionWidget } from '@/features/dashboard/components/DataRetentionWidget';
import { MetricsWidget } from '@/features/dashboard/components/MetricsWidget';
import { OfflineStatusWidget } from '@/features/dashboard/components/OfflineStatusWidget';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_WIDGET_IDS } from '@/features/dashboard/types';
import { ActiveCashRegistersWidget } from '@/features/dashboard/widgets/ActiveCashRegistersWidget';
import { BackupStatusWidget } from '@/features/dashboard/widgets/BackupStatusWidget';
import { DailyClosingWidget } from '@/features/dashboard/widgets/DailyClosingWidget';
import { FiskalyStatusWidget } from '@/features/dashboard/widgets/FiskalyStatusWidget';
import { FinanzOnlineStatusWidget } from '@/features/dashboard/widgets/FinanzOnlineStatusWidget';
import { LicenseExpiryWidget } from '@/features/dashboard/widgets/LicenseExpiryWidget';
import { LowStockAlertsWidget } from '@/features/dashboard/widgets/LowStockAlertsWidget';
import { parsePaymentTrendPeriod } from '@/features/dashboard/widgets/paymentTrendPeriod';
import { RecentPermissionChangesWidget } from '@/features/dashboard/widgets/RecentPermissionChangesWidget';
import { RecentUsersWidget } from '@/features/dashboard/widgets/RecentUsersWidget';
import {
  ActionRequiredWidget,
  ManagerActivityWidget,
  ManagerExportQuickActionsWidget,
  ManagerHospitalityLinksWidget,
  ManagerKpiStripWidget,
  ManagerLicenseChecklistWidget,
  ManagerLicenseStatusWidget,
  ManagerLicenseSupportWidget,
  ManagerMonatsbelegWidget,
  ManagerOfflineQueueWidget,
  ManagerTseHealthWidget,
} from '@/features/dashboard/widgets/ManagerDashboardWidgets';
import { TopSellingProductsWidget } from '@/features/dashboard/widgets/TopSellingProductsWidget';

const TodaySalesWidget = dynamic(
  () => import('@/features/dashboard/widgets/TodaySalesWidget').then((m) => ({ default: m.TodaySalesWidget })),
  { ssr: false, loading: () => <Skeleton active paragraph={{ rows: 6 }} /> }
);

const PaymentTrendWidget = dynamic(
  () =>
    import('@/features/dashboard/widgets/PaymentTrendWidget').then((m) => ({
      default: m.PaymentTrendWidget,
    })),
  { ssr: false, loading: () => <Skeleton active paragraph={{ rows: 6 }} /> }
);

export type DashboardWidgetRenderProps = {
  title: string;
  dragHandleProps?: WidgetShellProps['dragHandleProps'];
  settings?: Record<string, unknown> | null;
  onSettingsChange?: (settings: Record<string, unknown>) => void;
};

export function renderDashboardWidget(
  widgetId: string,
  props: DashboardWidgetRenderProps
): React.ReactNode {
  const common = {
    title: props.title,
    dragHandleProps: props.dragHandleProps,
  };

  switch (widgetId) {
    case DASHBOARD_WIDGET_IDS.actionRequired:
      return <ActionRequiredWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.dailyClosing:
      return <DailyClosingWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.managerLicenseStatus:
      return <ManagerLicenseStatusWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.managerKpiStrip:
      return <ManagerKpiStripWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.managerMonatsbeleg:
      return <ManagerMonatsbelegWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.managerActivity:
      return <ManagerActivityWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.managerTseHealth:
      return <ManagerTseHealthWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.fiskalyStatus:
      return <FiskalyStatusWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.managerOfflineQueue:
      return <ManagerOfflineQueueWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.managerLicenseChecklist:
      return <ManagerLicenseChecklistWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.managerLicenseSupport:
      return <ManagerLicenseSupportWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.managerHospitalityLinks:
      return <ManagerHospitalityLinksWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.managerExportQuickActions:
      return <ManagerExportQuickActionsWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.todaySales:
      return <TodaySalesWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.activeCashRegisters:
      return <ActiveCashRegistersWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.lowStockAlerts:
      return <LowStockAlertsWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.recentUsers:
      return <RecentUsersWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.recentPermissionChanges:
      return <RecentPermissionChangesWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.licenseExpiry:
      return <LicenseExpiryWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.finanzOnlineStatus:
      return <FinanzOnlineStatusWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.offlineSystemStatus:
      return <OfflineStatusWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.backupStatus:
      return <BackupStatusWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.dataRetention:
      return <DataRetentionWidget {...common} />;
    case DASHBOARD_WIDGET_IDS.topSellingProducts: {
      const period = props.settings?.period === 'week' ? ('week' as const) : ('today' as const);
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
    case DASHBOARD_WIDGET_IDS.systemMetrics:
      return <MetricsWidget {...common} />;
    default:
      return null;
  }
}
