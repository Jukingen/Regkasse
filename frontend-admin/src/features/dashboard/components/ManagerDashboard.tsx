'use client';

import Link from 'next/link';
import { Alert, Button, Card, Col, Row, Space, Statistic } from 'antd';
import { ClockCircleOutlined, DollarOutlined, TeamOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';

import { CashRegisterSelector } from '@/components/CashRegisterSelector';
import { ActivitySummary } from '@/features/dashboard/components/ActivitySummary';
import { useTodaySales } from '@/features/reports/hooks/useTodaySales';
import { usePendingMonatsbeleg } from '@/features/rksv/hooks/usePendingMonatsbeleg';
import { useActiveStaff } from '@/features/staff/hooks/useActiveStaff';
import { useOpenShifts } from '@/features/shifts/hooks/useOpenShifts';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { useI18n } from '@/i18n/I18nProvider';
import { formatCurrency } from '@/i18n/formatting';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';

function resolveUserDisplayName(
    firstName?: string | null,
    lastName?: string | null,
    userName?: string | null,
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
    location?: string | null,
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
    const {
        selectedRegister,
        selectedRegisterId,
        setSelectedRegisterId,
        hasMultipleRegisters,
    } = useCashRegisterSelection({
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
              selectedRegister.location,
          )
        : noRegisterLabel;
    const todayLabel = dayjs().format('DD.MM.YYYY');
    const balance = selectedRegister?.currentBalance ?? 0;
    const transactionCount = sales?.count ?? 0;

    return (
        <div style={{ padding: 24 }}>
            <Card style={{ marginBottom: 16, background: '#f8fafc' }} variant="borderless">
                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                    <h2 style={{ margin: 0 }}>
                        {t('dashboard.manager.welcome', { name: userName })}
                    </h2>
                    <p style={{ color: '#64748b', margin: 0 }}>
                        {registerLabel} — {todayLabel}
                    </p>
                    {hasMultipleRegisters ? (
                        <CashRegisterSelector
                            value={selectedRegisterId}
                            onChange={setSelectedRegisterId}
                            required
                            autoSelect
                            showFormItem={false}
                            style={{ maxWidth: 360 }}
                        />
                    ) : null}
                </Space>
            </Card>

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
                            valueStyle={{ color: '#16a34a' }}
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
                            valueStyle={{ color: '#eab308' }}
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
                            valueStyle={{ color: '#1a56db' }}
                        />
                        <small style={{ color: '#64748b' }}>
                            {t('dashboard.manager.onDutyToday')}
                        </small>
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
                            valueStyle={{ color: '#1a56db' }}
                        />
                        <small style={{ color: '#64748b' }}>{registerLabel}</small>
                    </Card>
                </Col>
            </Row>

            <div style={{ marginTop: 16 }}>
                <ActivitySummary limit={5} />
            </div>
        </div>
    );
}
