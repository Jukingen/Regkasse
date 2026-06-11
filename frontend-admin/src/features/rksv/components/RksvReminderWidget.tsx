'use client';

import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Select, Space, Typography } from 'antd';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { CashRegister } from '@/api/generated/model';
import { useAntdApp } from '@/hooks/useAntdApp';
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
            message.warning('Sie haben keine Berechtigung für diese Aktion.');
            return;
        }

        if (!selectedRegisterId || !selectedMissing) {
            message.info('Monatsbeleg für aktuellen Monat bereits vorhanden');
            return;
        }

        const { year, month } = getViennaCalendarYearMonth();
        if (selectedMissing.year !== year || selectedMissing.month !== month) {
            message.error('Nur der aktuelle Kalendermonat (Europe/Vienna) kann erstellt werden.');
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
            message.success('Monatsbeleg für aktuellen Monat erstellt');
            await refetch();
        } catch {
            message.error('Fehler beim Erstellen des Monatsbelegs');
        }
    };

    const refreshAll = async () => {
        await Promise.all([refetch(), refetchPastMissing()]);
    };

    if (isLoading || pastMissingLoading) {
        return (
            <Card title="RKSV Sonderbelege (Erinnerungen)" variant="borderless" style={{ marginBottom: 24 }}>
                <Typography.Text type="secondary">Lade Monatsbeleg-Status…</Typography.Text>
            </Card>
        );
    }

    if (isError) {
        return (
            <Card title="RKSV Sonderbelege (Erinnerungen)" variant="borderless" style={{ marginBottom: 24 }}>
                <Alert type="error" title="Monatsbeleg-Status konnte nicht geladen werden." />
            </Card>
        );
    }

    const allCurrentOk = missingMonths.length === 0;

    return (
        <Card title="RKSV Sonderbelege (Erinnerungen)" variant="borderless" style={{ marginBottom: 24 }}>
            <MonatsbelegPastMonthsAlert
                otherMissingCount={otherMissingCount}
                canCreate={canCreate}
                onManagePastMonths={() => setPastMonthsModalOpen(true)}
            />

            {missingMonths.length > 0 ? (
                <Space orientation="vertical" style={{ width: '100%' }} size="middle">
                    {missingMonths.length > 1 ? (
                        <Space wrap>
                            <Typography.Text strong>Kasse</Typography.Text>
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
                            title="Aktion erforderlich"
                            description={`Monatsbeleg für ${currentYearMonth} fehlt für die ausgewählte Kasse.`}
                            action={
                                canCreate ? (
                                    <Button
                                        size="small"
                                        type="primary"
                                        onClick={() => void handleCreateMissing()}
                                        loading={createMonatsbeleg.isPending}
                                    >
                                        Jetzt erstellen
                                    </Button>
                                ) : undefined
                            }
                        />
                    ) : null}

                    {missingMonths.length > 1 ? (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {missingMonths.length} Kassen benötigen den Monatsbeleg für {currentYearMonth}.
                        </Typography.Text>
                    ) : null}
                </Space>
            ) : allCurrentOk && otherMissingCount === 0 ? (
                <Alert
                    type="success"
                    title="Alles aktuell"
                    description={`Der Monatsbeleg für ${currentYearMonth} ist für alle Kassen vorhanden.`}
                />
            ) : allCurrentOk ? (
                <Alert
                    type="info"
                    title="Aktueller Monat abgeschlossen"
                    description={`Der Monatsbeleg für ${currentYearMonth} ist für alle Kassen vorhanden. Bitte fehlende Monate aus früheren Perioden nachholen.`}
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
