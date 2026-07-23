'use client';

import { ClockCircleOutlined, DollarOutlined, TeamOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, Row, Space, Statistic } from 'antd';
import dayjs from 'dayjs';
import Link from 'next/link';

import { CashRegisterSelector } from '@/components/CashRegisterSelector';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { ActivitySummary } from '@/features/dashboard/components/ActivitySummary';
import { Dashboard } from '@/features/dashboard/components/Dashboard';
import { DashboardMonatsbelegSection } from '@/features/dashboard/components/DashboardMonatsbelegSection';
import { HospitalityQuickLinksCard } from '@/features/dashboard/components/HospitalityQuickLinksCard';
import { OfflineQueueDashboardCard } from '@/features/dashboard/components/OfflineQueueDashboardCard';
import { RksvReminderCard } from '@/features/dashboard/components/RksvReminderCard';
import { TagesabschlussReminder } from '@/features/dashboard/components/TagesabschlussReminder';
import { TseHealthCard } from '@/features/dashboard/components/TseHealthCard';
import { ExportQuickActionsCard } from '@/features/exports/components/ExportQuickActionsCard';
import { useTodaySales } from '@/features/reports/hooks/useTodaySales';
import { usePendingMonatsbeleg } from '@/features/rksv/hooks/usePendingMonatsbeleg';
import { useOpenShifts } from '@/features/shifts/hooks/useOpenShifts';
import { useActiveStaff } from '@/features/staff/hooks/useActiveStaff';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n/I18nProvider';
import { formatCurrency } from '@/i18n/formatting';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';

function resolveUserDisplayName(
  firstName?: string | null,
  lastName?: string | null,
  userName?: string | null
): string {
  const fullName = [firstName, lastName].filter(Boolean).join(' ').trim();
  if (fullName) {
    return fullName;
  }
  return userName?.trim() || 'Manager';
}

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

export function ManagerDashboard() {
  const { t, formatLocale } = useI18n();
  const { user } = useAuth();
  const { hasPermission } = usePermissions();
  const { isAuthorized: canSeeRksvReminder } = useAuthorizationGate({
    requiredPermission: AppPermissions.CashRegisterView,
  });
  const { isAuthorized: canSeeTagesabschlussReminder } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.DAILY_CLOSING_VIEW,
  });

  const offlineQueueCardEnabled = hasPermission(PERMISSIONS.PAYMENT_VIEW);
  const tseHealthCardEnabled = hasPermission(AppPermissions.CashRegisterView);
  const { selectedRegister, selectedRegisterId, setSelectedRegisterId, hasMultipleRegisters } =
    useCashRegisterSelection({
      autoSelect: true,
      persistSelection: true,
    });

  const registerId = selectedRegisterId ?? undefined;
  const { data: sales, isLoading: salesLoading } = useTodaySales(registerId);
  const { data: pendingMonatsbeleg = [] } = usePendingMonatsbeleg();
  const { data: openShifts = [], isLoading: shiftsLoading } = useOpenShifts(registerId);
  const { data: activeStaff = [], isLoading: staffLoading } = useActiveStaff(registerId);

  const userName = resolveUserDisplayName(user?.firstName, user?.lastName, user?.userName);
  const noRegisterLabel = t('dashboard.manager.noRegister');
  const registerLabel = selectedRegister
    ? formatRegisterLabel(
        noRegisterLabel,
        selectedRegister.registerNumber,
        selectedRegister.location
      )
    : noRegisterLabel;
  const todayLabel = dayjs().format('DD.MM.YYYY');
  const balance = selectedRegister?.currentBalance ?? 0;
  const transactionCount = sales?.count ?? 0;

  const operationalHeader = (
    <>
      {offlineQueueCardEnabled ? <OfflineQueueDashboardCard /> : null}
      {tseHealthCardEnabled ? <TseHealthCard /> : null}
      {canSeeRksvReminder ? <RksvReminderCard /> : null}
      {canSeeRksvReminder ? <DashboardMonatsbelegSection enabled={canSeeRksvReminder} /> : null}
      <HospitalityQuickLinksCard />
      <ExportQuickActionsCard />
    </>
  );

  return (
    <div style={{ padding: 24 }}>
      <Card style={{ marginBottom: 16, background: '#f8fafc' }} variant="borderless">
        <Space orientation="vertical" size={8} style={{ width: '100%' }}>
          <h2 style={{ margin: 0 }}>{t('dashboard.manager.welcome', { name: userName })}</h2>
          <p style={{ color: '#64748b', margin: 0 }}>
            {registerLabel} — {todayLabel}
          </p>
          <CashRegisterSelector
            value={selectedRegisterId}
            onChange={setSelectedRegisterId}
            required
            autoSelect
            showFormItem={false}
            style={{ maxWidth: hasMultipleRegisters ? 360 : '100%' }}
          />
        </Space>
      </Card>

      {canSeeTagesabschlussReminder ? (
        <TagesabschlussReminder cashRegisterId={selectedRegisterId} register={selectedRegister} />
      ) : null}

      {pendingMonatsbeleg.length > 0 ? (
        <Alert
          title={t('dashboard.manager.pendingMonatsbeleg', {
            count: pendingMonatsbeleg.length,
          })}
          description={t('dashboard.manager.pendingMonatsbelegDescription')}
          type="warning"
          showIcon
          action={
            <Link href={RKSV_SONDERBELEGE_PATH}>
              <Button size="small" type="primary">
                {t('dashboard.manager.createNow')}
              </Button>
            </Link>
          }
          style={{ marginBottom: 16 }}
        />
      ) : null}

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
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

        <Col xs={24} sm={12} lg={6}>
          <Card>
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

        <Col xs={24} sm={12} lg={6}>
          <Card>
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

        <Col xs={24} sm={12} lg={6}>
          <Card>
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

      <div style={{ marginTop: 16 }}>
        <ActivitySummary limit={5} />
      </div>

      <div style={{ marginTop: 24 }}>
        <Dashboard headerSlot={operationalHeader} />
      </div>
    </div>
  );
}
