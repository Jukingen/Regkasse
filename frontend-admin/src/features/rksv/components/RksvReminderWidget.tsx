'use client';

import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Select, Space, Typography } from 'antd';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { CashRegister } from '@/api/generated/model';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n/I18nProvider';
import { MonatsbelegPastMonthsAlert } from '@/features/rksv/components/MonatsbelegPastMonthsAlert';
import { PastMonthsMonatsbelegModal } from '@/features/rksv/components/PastMonthsMonatsbelegModal';
import { useCreateMonatsbeleg } from '@/features/rksv/hooks/useCreateMonatsbeleg';
import { useMissingMonths } from '@/features/rksv/hooks/useMissingMonths';
import { usePastMissingMonatsbelege } from '@/features/rksv/hooks/usePastMissingMonatsbelege';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getViennaCalendarYearMonth } from '@/shared/utils/viennaCalendar';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';

function normalizeRegisterRows(data: unknown): CashRegister[] {
    if (Array.isArray(data)) return data as CashRegister[];
    if (data && typeof data === 'object' && 'registers' in data) {
        const registers = (data as { registers?: CashRegister[] }).registers;
        if (Array.isArray(registers)) return registers;
    }
    return [];
}

function registerLabel(register: CashRegister | undefined, registerId: string): string {
    if (!register) return registerId.slice(0, 8);
    const nr = formatRegisterDisplayLabel(register.registerNumber);
    const loc = register.location?.trim();
    if (loc && nr) return `${loc} (Nr. ${nr})`;
    if (nr) return `Nr. ${nr}`;
    return registerId.slice(0, 8);
}

export function RksvReminderWidget() {
    const { message } = useAntdApp();
    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const canCreate = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);

    const { missingMonths, currentYearMonth, refetch, isLoading, isError } = useMissingMonths();
    const {
        otherMissingCount,
        pastMissingEntries,
        refetch: refetchPastMissing,
        isLoading: pastMissingLoading,
    } = usePastMissingMonatsbelege();
    const createMonatsbeleg = useCreateMonatsbeleg();
    const { data: registersRaw } = useGetApiCashRegister();

    const registers = useMemo(() => normalizeRegisterRows(registersRaw), [registersRaw]);
    const registerById = useMemo(() => {
        const map = new Map<string, CashRegister>();
        for (const register of registers) {
            const id = register.id?.trim();
            if (id) map.set(id, register);
        }
        return map;
    }, [registers]);

    const [selectedRegisterId, setSelectedRegisterId] = useState<string | undefined>(undefined);
    const [pastMonthsModalOpen, setPastMonthsModalOpen] = useState(false);

    useEffect(() => {
        if (missingMonths.length === 0) {
            setSelectedRegisterId(undefined);
            return;
        }
        if (selectedRegisterId && missingMonths.some((entry) => entry.cashRegisterId === selectedRegisterId)) {
            return;
        }
        setSelectedRegisterId(missingMonths[0]?.cashRegisterId);
    }, [missingMonths, selectedRegisterId]);

    const selectedMissing = useMemo(
        () => missingMonths.find((entry) => entry.cashRegisterId === selectedRegisterId),
        [missingMonths, selectedRegisterId],
    );

    const currentMonthMissing = Boolean(selectedMissing);
    const anyOverdue = missingMonths.some((entry) => entry.isOverdue);

    const handleCreateMissing = async () => {
        if (!canCreate) {
            message.warning(t('dashboard.rksvReminder.permission_denied'));
            return;
        }

        if (!selectedRegisterId || !selectedMissing) {
            message.info(t('dashboard.rksvReminder.monatsbeleg_already_exists'));
            return;
        }

        const { year, month } = getViennaCalendarYearMonth();
        if (selectedMissing.year !== year || selectedMissing.month !== month) {
            message.error(t('dashboard.rksvReminder.only_current_month'));
            return;
        }

        try {
            await createMonatsbeleg.mutateAsync({
                data: {
                    cashRegisterId: selectedRegisterId,
                    year,
                    month,
                    reason: 'Monatsbeleg für aktuellen Kalendermonat',
                },
            });
            message.success(t('dashboard.rksvReminder.monatsbeleg_created_success'));
            await refetch();
        } catch {
            message.error(t('dashboard.rksvReminder.monatsbeleg_create_failed'));
        }
    };

    const refreshAll = async () => {
        await Promise.all([refetch(), refetchPastMissing()]);
    };

    if (isLoading || pastMissingLoading) {
        return (
            <Card
                title={t('dashboard.rksvReminder.card_title')}
                variant="borderless"
                style={{ marginBottom: 24 }}
            >
                <Typography.Text type="secondary">{t('dashboard.rksvReminder.loading_monatsbeleg')}</Typography.Text>
            </Card>
        );
    }

    if (isError) {
        return (
            <Card
                title={t('dashboard.rksvReminder.card_title')}
                variant="borderless"
                style={{ marginBottom: 24 }}
            >
                <Alert type="error" title={t('dashboard.rksvReminder.monatsbeleg_load_failed')} />
            </Card>
        );
    }

    const allCurrentOk = missingMonths.length === 0;

    return (
        <Card title={t('dashboard.rksvReminder.card_title')} variant="borderless" style={{ marginBottom: 24 }}>
            <MonatsbelegPastMonthsAlert
                otherMissingCount={otherMissingCount}
                canCreate={canCreate}
                onManagePastMonths={() => setPastMonthsModalOpen(true)}
            />

            {missingMonths.length > 0 ? (
                <Space orientation="vertical" style={{ width: '100%' }} size="middle">
                    {missingMonths.length > 1 ? (
                        <Space wrap>
                            <Typography.Text strong>{t('dashboard.rksvReminder.register_short')}</Typography.Text>
                            <Select
                                style={{ minWidth: 280 }}
                                value={selectedRegisterId}
                                onChange={setSelectedRegisterId}
                                options={missingMonths.map((entry) => ({
                                    value: entry.cashRegisterId,
                                    label: registerLabel(registerById.get(entry.cashRegisterId), entry.cashRegisterId),
                                }))}
                            />
                        </Space>
                    ) : null}

                    {currentMonthMissing ? (
                        <Alert
                            type={selectedMissing?.isOverdue || anyOverdue ? 'error' : 'warning'}
                            title={t('dashboard.rksvReminder.action_required')}
                            description={t('dashboard.rksvReminder.monatsbeleg_missing_for_register', {
                                month: currentYearMonth,
                            })}
                            action={
                                canCreate ? (
                                    <Button
                                        size="small"
                                        type="primary"
                                        onClick={() => void handleCreateMissing()}
                                        loading={createMonatsbeleg.isPending}
                                    >
                                        {t('dashboard.rksvReminder.create_now')}
                                    </Button>
                                ) : undefined
                            }
                        />
                    ) : null}

                    {missingMonths.length > 1 ? (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {t('dashboard.rksvReminder.registers_need_monatsbeleg', {
                                count: missingMonths.length,
                                month: currentYearMonth,
                            })}
                        </Typography.Text>
                    ) : null}
                </Space>
            ) : allCurrentOk && otherMissingCount === 0 ? (
                <Alert
                    type="success"
                    title={t('dashboard.rksvReminder.all_current_widget_title')}
                    description={t('dashboard.rksvReminder.all_current_widget_description', {
                        month: currentYearMonth,
                    })}
                />
            ) : allCurrentOk ? (
                <Alert
                    type="info"
                    title={t('dashboard.rksvReminder.current_month_complete_title')}
                    description={t('dashboard.rksvReminder.current_month_complete_description', {
                        month: currentYearMonth,
                    })}
                />
            ) : null}

            <PastMonthsMonatsbelegModal
                open={pastMonthsModalOpen}
                entries={pastMissingEntries}
                onClose={() => setPastMonthsModalOpen(false)}
                onCreated={() => void refreshAll()}
            />
        </Card>
    );
}
