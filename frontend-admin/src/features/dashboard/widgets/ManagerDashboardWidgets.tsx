'use client';

import { ClockCircleOutlined, DollarOutlined, TeamOutlined } from '@ant-design/icons';
import { Card, Col, Row, Statistic, Typography } from 'antd';
import Link from 'next/link';
import React from 'react';

import { LicenseRenewalChecklistCard } from '@/components/LicenseRenewalChecklistCard';
import { LicenseSupportOptionsCard } from '@/components/LicenseSupportOptionsCard';
import { ActivitySummary } from '@/features/dashboard/components/ActivitySummary';
import { DashboardMonatsbelegSection } from '@/features/dashboard/components/DashboardMonatsbelegSection';
import { HospitalityQuickLinksCard } from '@/features/dashboard/components/HospitalityQuickLinksCard';
import { ManagerLicenseStatusCard } from '@/features/dashboard/components/ManagerLicenseStatusCard';
import { OfflineQueueDashboardCard } from '@/features/dashboard/components/OfflineQueueDashboardCard';
import { RksvReminderCard } from '@/features/dashboard/components/RksvReminderCard';
import { TagesabschlussReminder } from '@/features/dashboard/components/TagesabschlussReminder';
import { TseHealthCard } from '@/features/dashboard/components/TseHealthCard';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { ExportQuickActionsCard } from '@/features/exports/components/ExportQuickActionsCard';
import { useTodaySales } from '@/features/reports/hooks/useTodaySales';
import { useOpenShifts } from '@/features/shifts/hooks/useOpenShifts';
import { useActiveStaff } from '@/features/staff/hooks/useActiveStaff';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { useI18n } from '@/i18n/I18nProvider';
import { formatCurrency } from '@/i18n/formatting';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';

type ShellProps = Pick<WidgetShellProps, 'title' | 'dragHandleProps'>;

function formatRegisterLabel(
  fallback: string,
  registerNumber?: string | null,
  location?: string | null
): string {
  const number = registerNumber?.trim();
  const place = location?.trim();
  if (number && place) {
    return `${number} — ${place}`;
  }
  return number || place || fallback;
}

/**
 * Tagesabschluss + RKSV Sonderbeleg reminders (Handlungsbedarf).
 * Register context shared via persisted `useCashRegisterSelection` (welcome selector).
 */
export function ActionRequiredWidget({ title, dragHandleProps }: ShellProps) {
  const { t } = useI18n();
  const { isAuthorized: canSeeTagesabschluss } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.DAILY_CLOSING_VIEW,
  });
  const { isAuthorized: canSeeRksvReminder } = useAuthorizationGate({
    requiredPermission: AppPermissions.CashRegisterView,
  });
  const { selectedRegister, selectedRegisterId } = useCashRegisterSelection({
    autoSelect: true,
    persistSelection: true,
  });

  const hasAnySection = canSeeTagesabschluss || canSeeRksvReminder;

  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      {!hasAnySection ? (
        <Typography.Text type="secondary">{t('dashboard.widgets.actionRequired.noPermission')}</Typography.Text>
      ) : (
        <>
          {canSeeTagesabschluss ? (
            <TagesabschlussReminder cashRegisterId={selectedRegisterId} register={selectedRegister} />
          ) : null}
          {canSeeRksvReminder ? <RksvReminderCard /> : null}
        </>
      )}
    </WidgetShell>
  );
}

/** Mandant license health / countdown — drag-sortable catalog widget. */
export function ManagerLicenseStatusWidget({ title, dragHandleProps }: ShellProps) {
  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      <ManagerLicenseStatusCard />
    </WidgetShell>
  );
}

/** KPI strip (sales / shifts / staff / balance) for the selected cash register. */
export function ManagerKpiStripWidget({ title, dragHandleProps }: ShellProps) {
  const { t, formatLocale } = useI18n();
  const { selectedRegister, selectedRegisterId } = useCashRegisterSelection({
    autoSelect: true,
    persistSelection: true,
  });
  const registerId = selectedRegisterId ?? undefined;
  const { data: sales, isLoading: salesLoading } = useTodaySales(registerId);
  const { data: openShifts = [], isLoading: shiftsLoading } = useOpenShifts(registerId);
  const { data: activeStaff = [], isLoading: staffLoading } = useActiveStaff(registerId);

  const noRegisterLabel = t('dashboard.manager.noRegister');
  const registerLabel = selectedRegister
    ? formatRegisterLabel(
        noRegisterLabel,
        selectedRegister.registerNumber,
        selectedRegister.location
      )
    : noRegisterLabel;
  const balance = selectedRegister?.currentBalance ?? 0;
  const transactionCount = sales?.count ?? 0;

  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12}>
          <Card size="small">
            <Statistic
              title={t('dashboard.manager.todaySales')}
              value={sales?.total ?? 0}
              precision={2}
              prefix={<DollarOutlined />}
              suffix="€"
              loading={salesLoading}
              styles={{ content: { color: '#16a34a' } }}
            />
            <small style={{ color: '#64748b' }}>
              {t('dashboard.manager.transactionCount', {
                count: transactionCount,
              })}
            </small>
          </Card>
        </Col>
        <Col xs={24} sm={12}>
          <Card size="small">
            <Statistic
              title={t('dashboard.manager.openShifts')}
              value={openShifts.length}
              prefix={<ClockCircleOutlined />}
              loading={shiftsLoading}
              styles={{ content: { color: '#eab308' } }}
            />
            <small style={{ color: '#64748b' }}>
              {openShifts.length > 0
                ? t('dashboard.manager.shiftOpen')
                : t('dashboard.manager.allClosed')}
            </small>
            <div style={{ marginTop: 8 }}>
              <Link href="/staff/shifts">{t('dashboard.manager.viewStaffShifts')}</Link>
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={12}>
          <Card size="small">
            <Statistic
              title={t('dashboard.manager.activeStaff')}
              value={activeStaff.length}
              prefix={<TeamOutlined />}
              loading={staffLoading}
              styles={{ content: { color: '#1a56db' } }}
            />
            <small style={{ color: '#64748b' }}>{t('dashboard.manager.onDutyToday')}</small>
            <div style={{ marginTop: 8 }}>
              <Link href="/staff">{t('dashboard.manager.viewStaffHub')}</Link>
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={12}>
          <Card size="small">
            <Statistic
              title={t('dashboard.manager.cashBalance')}
              value={balance}
              formatter={(value) => formatCurrency(Number(value ?? 0), formatLocale)}
              prefix={<DollarOutlined />}
              styles={{ content: { color: '#1a56db' } }}
            />
            <small style={{ color: '#64748b' }}>{registerLabel}</small>
          </Card>
        </Col>
      </Row>
    </WidgetShell>
  );
}

/** Monatsbeleg + RKSV reminder section (lazy when scrolled into view). */
export function ManagerMonatsbelegWidget({ title, dragHandleProps }: ShellProps) {
  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      <DashboardMonatsbelegSection enabled />
    </WidgetShell>
  );
}

/** Recent tenant activity feed. */
export function ManagerActivityWidget({ title, dragHandleProps }: ShellProps) {
  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      <ActivitySummary limit={5} />
    </WidgetShell>
  );
}

export function ManagerTseHealthWidget({ title, dragHandleProps }: ShellProps) {
  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      <TseHealthCard />
    </WidgetShell>
  );
}

export function ManagerOfflineQueueWidget({ title, dragHandleProps }: ShellProps) {
  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      <OfflineQueueDashboardCard />
    </WidgetShell>
  );
}

export function ManagerLicenseChecklistWidget({ title, dragHandleProps }: ShellProps) {
  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      <LicenseRenewalChecklistCard />
    </WidgetShell>
  );
}

export function ManagerLicenseSupportWidget({ title, dragHandleProps }: ShellProps) {
  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      <LicenseSupportOptionsCard />
    </WidgetShell>
  );
}

export function ManagerHospitalityLinksWidget({ title, dragHandleProps }: ShellProps) {
  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      <HospitalityQuickLinksCard />
    </WidgetShell>
  );
}

export function ManagerExportQuickActionsWidget({ title, dragHandleProps }: ShellProps) {
  return (
    <WidgetShell title={title} dragHandleProps={dragHandleProps}>
      <ExportQuickActionsCard />
    </WidgetShell>
  );
}
