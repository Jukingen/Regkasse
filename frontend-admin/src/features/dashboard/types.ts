/** Widget ids — must match backend `DashboardWidgetCatalog`. */
export const DASHBOARD_WIDGET_IDS = {
  actionRequired: 'action-required',
  dailyClosing: 'daily-closing',
  managerLicenseStatus: 'manager-license-status',
  managerKpiStrip: 'manager-kpi-strip',
  managerMonatsbeleg: 'manager-monatsbeleg',
  managerActivity: 'manager-activity',
  managerTseHealth: 'manager-tse-health',
  managerOfflineQueue: 'manager-offline-queue',
  managerLicenseChecklist: 'manager-license-checklist',
  managerLicenseSupport: 'manager-license-support',
  managerHospitalityLinks: 'manager-hospitality-links',
  managerExportQuickActions: 'manager-export-quick-actions',
  todaySales: 'today-sales',
  activeCashRegisters: 'active-cash-registers',
  lowStockAlerts: 'low-stock-alerts',
  recentUsers: 'recent-users',
  recentPermissionChanges: 'recent-permission-changes',
  licenseExpiry: 'license-expiry',
  finanzOnlineStatus: 'finanzonline-status',
  offlineSystemStatus: 'offline-system-status',
  backupStatus: 'backup-status',
  dataRetention: 'data-retention',
  topSellingProducts: 'top-selling-products',
  paymentTrends: 'payment-trends',
  systemMetrics: 'system-metrics',
} as const;

export type DashboardWidgetId = (typeof DASHBOARD_WIDGET_IDS)[keyof typeof DASHBOARD_WIDGET_IDS];

export type DashboardWidgetCatalogItem = {
  widgetId: string;
  title: string;
  description: string;
  requiredPermission: string;
  defaultOrder: number;
  defaultVisible: boolean;
  supportsAutoRefresh: boolean;
};

export type DashboardWidgetPreference = {
  widgetId: string;
  order: number;
  isVisible: boolean;
  settings?: Record<string, unknown> | null;
};

export type DashboardPreferencesResponse = {
  widgets: DashboardWidgetPreference[];
  updatedAtUtc?: string | null;
};

export type SaveDashboardPreferencesRequest = {
  widgets: DashboardWidgetPreference[];
};

export const DASHBOARD_AUTO_REFRESH_MS = 30_000;
export const DASHBOARD_DAILY_CLOSING_REFRESH_MS = 5 * 60_000;
