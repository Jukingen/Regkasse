'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Bu ana bileşen RKSV Sonderbelege işlemlerini daha anlaşılır kart düzeninde sunar.
 */

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { Modal, Alert, Button, Card, Col, DatePicker, Input, Row, Select, Space, Table, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { InfoCircleOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import { getApiReceiptsList } from '@/api/generated/receipts/receipts';
import type { ReceiptListItemDto } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_GROUP_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { formatEUR } from '@/shared/utils/currency';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';
import { ReprintButton } from '@/features/payments/components/ReprintButton';
import { StoredReportPdfButton } from '@/features/reports/components/StoredReportPdfButton';
import { reportPdfTypeFromSpecialReceiptKind } from '@/features/reports/api/reportPdfApi';
import { rksvSpecialReceiptKindLabelDe } from '@/features/rksv-operations/rksvSpecialReceiptDisplay';
import {
    isRksvFinanzOnlineTrackedSpecialReceiptKind,
    rksvFinanzOnlineSubmissionStatusLabelDe,
    rksvFinanzOnlineSubmissionStatusTagColor,
} from '@/features/receipts/utils/rksvFinanzOnlineSubmissionUi';

import { formatDateTime } from '@/i18n/formatting';
import { CreateMonatsbelegModal } from '@/features/rksv/components/CreateMonatsbelegModal';
import { LateMonatsbelegCreationCard } from '@/features/rksv/components/LateMonatsbelegCreationCard';
import { monatsbelegQueryKeys, useCashRegisterMonatsbeleg } from '@/features/rksv/hooks/useMonatsbeleg';
import {
    getMonthDifference,
    getViennaCalendarYear,
    getViennaCalendarYearMonth,
} from '@/shared/utils/viennaCalendar';
import { receiptIsLateCreated } from '@/features/rksv/types/receiptLateCreation';
import type { ReceiptLateCreationFields } from '@/features/rksv/types/receiptLateCreation';

type OrvalReceiptRow = ReceiptListItemDto & ReceiptLateCreationFields;

type MissingMonatsbelegTableRow = {
    key: string;
    year: number;
    month: number;
    isMissing: boolean;
    isOverdue: boolean;
    monthDiff: number;
};

function monatsbelegDelaySeverity(monthDiff: number): { label: string; color: string } {
    if (monthDiff <= 0) return { label: 'Aktuell', color: 'green' };
    if (monthDiff <= 1) return { label: 'Info', color: 'blue' };
    if (monthDiff <= 6) return { label: 'Warnung', color: 'orange' };
    return { label: 'Achtung', color: 'red' };
}

function formatMonthYearDe(year: number, month: number): string {
    return new Intl.DateTimeFormat('de-DE', {
        month: 'long',
        year: 'numeric',
        timeZone: 'Europe/Vienna',
    }).format(new Date(Date.UTC(year, month - 1, 1)));
}

function rawRegisterStatus(reg: { status?: number | null }): number | undefined {
    return typeof reg.status === 'number' ? reg.status : undefined;
}

function registerBetriebsstatusDe(status: number | undefined): string {
    switch (status) {
        case 1:
            return 'Geschlossen';
        case 2:
            return 'Geöffnet';
        case 3:
            return 'Wartung';
        case 4:
            return 'Deaktiviert';
        case 5:
            return 'Fiskalisierung abgeschlossen';
        default:
            return status != null ? `Status ${status}` : '—';
    }
}

function normalizeSpecialKind(kind: string | null | undefined): string {
    return String(kind ?? '').trim().toLowerCase();
}

function isKind(row: OrvalReceiptRow, kind: string): boolean {
    return normalizeSpecialKind(row.rksvSpecialReceiptKind) === kind;
}

function specialReceiptPurposeDe(kind: string): string {
    switch (kind) {
        case 'startbeleg':
            return 'Erster RKSV-Beleg zur Aktivierung einer Kasse.';
        case 'monatsbeleg':
            return 'Monatlicher Kontrollbeleg zur RKSV-Nachweisführung.';
        case 'jahresbeleg':
            return 'Jährlicher Kontrollbeleg für den Jahresabschluss.';
        case 'nullbeleg':
            return 'Nullumsatz-Beleg für Kontrolle, Test oder Sonderfälle.';
        case 'schlussbeleg':
            return 'Endgültige Stilllegung der Kasse (kein weiterer Verkauf).';
        default:
            return 'RKSV-Sonderbeleg.';
    }
}

function specialReceiptBadge(kind: string): { text: string; color: string } {
    switch (kind) {
        case 'startbeleg':
            return { text: 'Start', color: 'blue' };
        case 'monatsbeleg':
            return { text: 'Monats', color: 'green' };
        case 'jahresbeleg':
            return { text: 'Jahres', color: 'gold' };
        case 'nullbeleg':
            return { text: 'Null', color: 'purple' };
        case 'schlussbeleg':
            return { text: 'Schluss', color: 'red' };
        default:
            return { text: 'Sonderbeleg', color: 'default' };
    }
}

function monthShortNameDe(month1to12: number): string {
    return new Intl.DateTimeFormat('de-DE', {
        month: 'short',
        timeZone: 'Europe/Vienna',
    }).format(new Date(Date.UTC(2026, month1to12 - 1, 1)));
}

function titleWithTooltip(title: string, tooltipText: string): React.ReactNode {
    return (
        <Space size={6}>
            <span>{title}</span>
            <Tooltip title={tooltipText}>
                <InfoCircleOutlined style={{ color: '#8c8c8c' }} />
            </Tooltip>
        </Space>
    );
}

export default function RksvSonderbelegePage() {
  const { message, modal } = useAntdApp();

    const { hasPermission } = usePermissions();
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();

    const canNull = hasPermission(PERMISSIONS.RKSV_NULLBELEG_CREATE);
    const canStart = hasPermission(PERMISSIONS.RKSV_STARTBELEG_CREATE);
    const canMonat = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);
    const canJahr = hasPermission(PERMISSIONS.RKSV_JAHRESBELEG_CREATE);
    const canSchluss = hasPermission(PERMISSIONS.RKSV_SCHLUSSBELEG_CREATE);
    // Demo tools are SuperAdmin-only (backend catalog) AND development-only. Manager never sees them.
    const canTestHelper = hasPermission(PERMISSIONS.RKSV_TEST_HELPER);
    const canTseSimulation = hasPermission(PERMISSIONS.RKSV_TSE_SIMULATION);
    const isDevelopment = process.env.NODE_ENV === 'development';

    // Canonical admin register source (IgnoreQueryFilters + explicit effective-tenant scoping):
    // robust for Manager, unlike the legacy shared GET /api/CashRegister which also relied on
    // the ambient EF global query filter and could yield an empty list.
    const { registers, isLoading: registersLoading } = useAdminCashRegisterList({
        allowTenantScopedDefault: true,
        excludeDecommissioned: false,
    });

    const { year: viennaYear, month: viennaMonth } = useMemo(() => getViennaCalendarYearMonth(), []);
    const defaultYear = useMemo(() => getViennaCalendarYear(), []);

    const [registerId, setRegisterId] = useState<string | undefined>(undefined);
    const [monatPeriod, setMonatPeriod] = useState(() => dayjs(`${viennaYear}-${String(viennaMonth).padStart(2, '0')}-01`));
    const [jahrPeriod, setJahrPeriod] = useState(() => dayjs(`${defaultYear}-01-01`));
    const [nullPeriod, setNullPeriod] = useState(() => dayjs(`${viennaYear}-${String(viennaMonth).padStart(2, '0')}-01`));
    const [jbEarly, setJbEarly] = useState('');
    const [reasonShort, setReasonShort] = useState('');
    const [busy, setBusy] = useState<string | null>(null);

    const [schlussModalOpen, setSchlussModalOpen] = useState(false);
    const [schlussConfirmText, setSchlussConfirmText] = useState('');
    const [monatsbelegModalOpen, setMonatsbelegModalOpen] = useState(false);
    const [selectedMonatsbelegYear, setSelectedMonatsbelegYear] = useState(viennaYear);
    const [selectedMonatsbelegMonth, setSelectedMonatsbelegMonth] = useState(viennaMonth);

    const didAutoSelectRef = useRef(false);

    useEffect(() => {
        const q = searchParams.get('registerId')?.trim();
        if (q) setRegisterId(q);
    }, [searchParams]);

    // Auto-select once: if exactly one register is available (and none was preselected
    // via query param or user action), pick it so Manager sees the register immediately.
    useEffect(() => {
        if (didAutoSelectRef.current || registersLoading) return;
        if (registerId) {
            didAutoSelectRef.current = true;
            return;
        }
        if (searchParams.get('registerId')?.trim()) return;
        if (registers.length === 1 && registers[0]?.id) {
            setRegisterId(String(registers[0].id));
            didAutoSelectRef.current = true;
        }
    }, [registers, registersLoading, registerId, searchParams]);

    useEffect(() => {
        const focus = searchParams.get('focus')?.trim();
        if (
            focus !== 'startbeleg'
            && focus !== 'schlussbeleg'
            && focus !== 'monatsbeleg'
            && focus !== 'test-helper'
        ) {
            return;
        }
        const id =
            focus === 'startbeleg'
                ? 'rksv-focus-startbeleg'
                : focus === 'schlussbeleg'
                  ? 'rksv-focus-schlussbeleg'
                  : focus === 'test-helper'
                    ? 'rksv-focus-test-helper'
                    : 'rksv-missing-monatsbelege';
        requestAnimationFrame(() => {
            document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
    }, [searchParams]);

    const monatsbelegStatusQuery = useCashRegisterMonatsbeleg(registerId ?? '', {
        enabled: Boolean(registerId?.trim()),
    });

    const SONDERBELEG_RECEIPT_SCAN_PAGE_SIZE = 100;

    const { data: receiptScan, isLoading: scanLoading } = useQuery({
        queryKey: ['rksv-sonderbelege-recent-special', SONDERBELEG_RECEIPT_SCAN_PAGE_SIZE],
        queryFn: async () => {
            const res = await getApiReceiptsList({
                page: 1,
                pageSize: SONDERBELEG_RECEIPT_SCAN_PAGE_SIZE,
                sort: 'issuedAt:desc',
            });
            const items = (res.items ?? []) as OrvalReceiptRow[];
            return items.filter((x) => Boolean(x.rksvSpecialReceiptKind?.trim()));
        },
    });

    const selectedRegister = useMemo(
        () => registers.find((r) => String(r.id ?? '') === String(registerId ?? '')),
        [registers, registerId],
    );
    const selectedRegisterStatus = selectedRegister ? rawRegisterStatus(selectedRegister) : undefined;
    const selectedRegisterIsDecommissioned = selectedRegisterStatus === 5;
    const selectedRegisterHasOpenSession = selectedRegisterStatus === 2;
    const canCreateSchlussbelegNow = selectedRegisterStatus === 1;

    const registerScopedReceipts = useMemo(
        () => (receiptScan ?? []).filter((row) => String(row.cashRegisterId ?? '') === String(registerId ?? '')),
        [receiptScan, registerId],
    );

    const monatYear = monatPeriod.year();
    const monatMonth = monatPeriod.month() + 1;
    const monatMonthDiff = getMonthDifference(monatYear, monatMonth);
    const monatIsFutureMonth = monatMonthDiff < 0;
    const jahrYear = jahrPeriod.year();

    const hasStartbelegForRegister = useMemo(
        () => registerScopedReceipts.some((row) => isKind(row, 'startbeleg')),
        [registerScopedReceipts],
    );

    const hasNullbelegForRegister = useMemo(
        () => registerScopedReceipts.some((row) => isKind(row, 'nullbeleg')),
        [registerScopedReceipts],
    );

    const hasMonatsbelegForPeriod = useMemo(
        () =>
            registerScopedReceipts.some(
                (row) =>
                    isKind(row, 'monatsbeleg') &&
                    Number(row.rksvSpecialReceiptYear ?? 0) === monatYear &&
                    Number(row.rksvSpecialReceiptMonth ?? 0) === monatMonth,
            ),
        [registerScopedReceipts, monatYear, monatMonth],
    );

    const hasJahresbelegForYear = useMemo(
        () =>
            registerScopedReceipts.some(
                (row) => isKind(row, 'jahresbeleg') && Number(row.rksvSpecialReceiptYear ?? 0) === jahrYear,
            ),
        [registerScopedReceipts, jahrYear],
    );

    const hasSchlussbelegForRegister = useMemo(
        () => registerScopedReceipts.some((row) => isKind(row, 'schlussbeleg')),
        [registerScopedReceipts],
    );

    const recentSpecialReceipts = useMemo(
        () => (registerId ? registerScopedReceipts : receiptScan ?? []).slice(0, 10),
        [registerId, registerScopedReceipts, receiptScan],
    );

    const monthlyTimelineRows = useMemo(
        () =>
            Array.from({ length: 12 }, (_, idx) => {
                const month = idx + 1;
                const hasMonatsbeleg = registerScopedReceipts.some(
                    (row) =>
                        isKind(row, 'monatsbeleg') &&
                        Number(row.rksvSpecialReceiptYear ?? 0) === monatYear &&
                        Number(row.rksvSpecialReceiptMonth ?? 0) === month,
                );
                const nowMonth = viennaYear === monatYear ? viennaMonth : 12;
                const isPastOrCurrent = month <= nowMonth;
                const status = hasMonatsbeleg
                    ? 'done'
                    : isPastOrCurrent
                      ? 'missing'
                      : 'pending';
                return { month, status };
            }),
        [registerScopedReceipts, monatYear, viennaYear, viennaMonth],
    );

    const invalidateLists = useCallback(async () => {
        await queryClient.invalidateQueries({ queryKey: ['rksv-sonderbelege-recent-special'] });
        await queryClient.invalidateQueries({ queryKey: ['/api/Receipts/list'] });
    }, [queryClient]);

    const refetchMonatsbelegData = useCallback(async () => {
        await invalidateLists();
        await queryClient.invalidateQueries({ queryKey: monatsbelegQueryKeys.statusOverview });
        if (registerId?.trim()) {
            await queryClient.invalidateQueries({
                queryKey: monatsbelegQueryKeys.registerStatus(registerId.trim()),
            });
            await monatsbelegStatusQuery.refetch();
        }
    }, [invalidateLists, monatsbelegStatusQuery, queryClient, registerId]);

    const missingMonatsbelegRows = useMemo((): MissingMonatsbelegTableRow[] => {
        const apiMissing = monatsbelegStatusQuery.data?.missingMonths ?? [];
        if (apiMissing.length > 0) {
            return apiMissing
                .map((entry) => ({
                    key: `${entry.year}-${String(entry.month).padStart(2, '0')}`,
                    year: entry.year,
                    month: entry.month,
                    isMissing: true,
                    isOverdue: entry.isOverdue,
                    monthDiff: getMonthDifference(entry.year, entry.month),
                }))
                .sort((a, b) => {
                    const anchorA = a.year * 12 + (a.month - 1);
                    const anchorB = b.year * 12 + (b.month - 1);
                    return anchorA - anchorB;
                });
        }

        return monthlyTimelineRows
            .filter((row) => row.status === 'missing')
            .map((row) => ({
                key: `${monatYear}-${String(row.month).padStart(2, '0')}`,
                year: monatYear,
                month: row.month,
                isMissing: true,
                isOverdue: getMonthDifference(monatYear, row.month) > 0,
                monthDiff: getMonthDifference(monatYear, row.month),
            }));
    }, [monatsbelegStatusQuery.data?.missingMonths, monatYear, monthlyTimelineRows]);

    const openMissingMonatsbelegModal = useCallback(
        (year: number, month: number) => {
            if (!registerId) {
                message.warning('Bitte eine Kasse wählen.');
                return;
            }
            if (!canMonat) {
                message.warning('Sie haben keine Berechtigung für diese Aktion.');
                return;
            }
            if (getMonthDifference(year, month) < 0) {
                message.error('Monatsbeleg kann nicht für einen zukünftigen Kalendermonat erstellt werden.');
                return;
            }
            setSelectedMonatsbelegYear(year);
            setSelectedMonatsbelegMonth(month);
            setMonatsbelegModalOpen(true);
        },
        [registerId, canMonat, message],
    );

    const registerOptions = useMemo(
        () =>
            registers
                .filter((r) => r.id)
                .map((r) => ({
                    value: r.id as string,
                    label: `${formatRegisterDisplayLabel(r.registerNumber) || r.id} (${r.id})`,
                })),
        [registers],
    );

    const postJson = useCallback(async (path: string, body: object) => {
        return customInstance<{ paymentId?: string; receiptNumber?: string; message?: string }>({
            url: path,
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            data: body,
        });
    }, []);

    const onNullbeleg = useCallback(async () => {
        if (!registerId) return message.warning('Bitte eine Kasse wählen.');
        setBusy('null');
        try {
            await postJson('/api/rksv/special-receipts/nullbeleg', {
                cashRegisterId: registerId,
                year: viennaYear,
                month: viennaMonth,
                reason: reasonShort.trim() || 'Nullbeleg für Prüfzwecke',
                actsAsJahresbeleg: null,
            });
            message.success('Nullbeleg erstellt.');
            await invalidateLists();
        } catch (e: unknown) {
            const err = e as { response?: { data?: { message?: string } }; message?: string };
            message.error(String(err?.response?.data?.message ?? err?.message ?? 'Fehler'));
        } finally {
            setBusy(null);
        }
    }, [registerId, viennaYear, viennaMonth, reasonShort, postJson, invalidateLists]);

    const onStartbeleg = useCallback(async () => {
        if (!registerId) return message.warning('Bitte eine Kasse wählen.');
        setBusy('start');
        try {
            await postJson('/api/rksv/special-receipts/startbeleg', {
                cashRegisterId: registerId,
                reason: reasonShort.trim() || 'Admin Startbeleg',
            });
            message.success('Startbeleg erstellt.');
            await invalidateLists();
        } catch (e: unknown) {
            const err = e as { response?: { data?: { message?: string } }; message?: string };
            message.error(String(err?.response?.data?.message ?? err?.message ?? 'Fehler'));
        } finally {
            setBusy(null);
        }
    }, [registerId, reasonShort, postJson, invalidateLists]);

    const openMonatsbelegModal = useCallback(() => {
        openMissingMonatsbelegModal(monatYear, monatMonth);
    }, [monatYear, monatMonth, openMissingMonatsbelegModal]);

    const onJahresbeleg = useCallback(async () => {
        if (!registerId) return message.warning('Bitte eine Kasse wählen.');
        setBusy('jahr');
        try {
            await postJson('/api/rksv/special-receipts/jahresbeleg', {
                cashRegisterId: registerId,
                year: jahrYear,
                reason: 'Admin Jahresbeleg',
                earlyReason: jbEarly.trim() || null,
            });
            message.success('Jahresbeleg erstellt.');
            await invalidateLists();
        } catch (e: unknown) {
            const err = e as { response?: { data?: { message?: string } }; message?: string };
            message.error(String(err?.response?.data?.message ?? err?.message ?? 'Fehler'));
        } finally {
            setBusy(null);
        }
    }, [registerId, jahrYear, jbEarly, postJson, invalidateLists]);

    const onSchlussbeleg = useCallback(async () => {
        if (!registerId) return message.warning('Bitte eine Kasse wählen.');
        if (!canCreateSchlussbelegNow) {
            message.error('Endbeleg ist nur möglich, wenn keine offene Sitzung besteht und der Status "Geschlossen" ist.');
            return;
        }
        setBusy('schluss');
        try {
            await postJson('/api/rksv/special-receipts/schlussbeleg', {
                cashRegisterId: registerId,
                reason: reasonShort.trim() || 'Admin Schlussbeleg',
            });
            message.success('Schlussbeleg erstellt — Status wechselt auf "Decommissioned". Keine neuen Zahlungen mehr erlaubt.');
            await invalidateLists();
        } catch (e: unknown) {
            const err = e as { response?: { data?: { message?: string } }; message?: string };
            message.error(String(err?.response?.data?.message ?? err?.message ?? 'Fehler'));
        } finally {
            setBusy(null);
        }
    }, [registerId, canCreateSchlussbelegNow, reasonShort, postJson, invalidateLists]);

    const confirmJahresbeleg = useCallback(() => {
        modal.confirm({
            title: 'Jahresbeleg erstellen',
            content: 'Dieser Vorgang kann nicht rückgängig gemacht werden.',
            okText: 'Erstellen',
            cancelText: 'Abbrechen',
            onOk: () => onJahresbeleg(),
        });
    }, [onJahresbeleg]);

    const submitSchlussModal = useCallback(async () => {
        if (!canCreateSchlussbelegNow) {
            message.error('Endbeleg ist nur bei geschlossener Kasse ohne offene Sitzung möglich.');
            return;
        }
        if (schlussConfirmText.trim().toUpperCase() !== 'ENDBELEG') {
            message.error('Bitte exakt «ENDBELEG» eingeben.');
            return;
        }
        await onSchlussbeleg();
        setSchlussModalOpen(false);
        setSchlussConfirmText('');
    }, [canCreateSchlussbelegNow, schlussConfirmText, onSchlussbeleg]);

    const openSchlussbelegDialog = useCallback(() => {
        if (!registerId) {
            message.warning('Bitte eine Kasse wählen.');
            return;
        }
        if (!canCreateSchlussbelegNow) {
            message.error('Endbeleg ist nur möglich, wenn keine offene Sitzung besteht und die Kasse geschlossen ist.');
            return;
        }
        setSchlussConfirmText('');
        setSchlussModalOpen(true);
    }, [registerId, canCreateSchlussbelegNow]);

    const onBulkCreateMissingMonatsbelege = useCallback(async () => {
        if (!registerId) {
            message.warning('Bitte eine Kasse wählen.');
            return;
        }

        const hasCurrentMonthMonatsbeleg = registerScopedReceipts.some(
            (row) =>
                isKind(row, 'monatsbeleg') &&
                Number(row.rksvSpecialReceiptYear ?? 0) === viennaYear &&
                Number(row.rksvSpecialReceiptMonth ?? 0) === viennaMonth,
        );

        if (hasCurrentMonthMonatsbeleg) {
            message.info('Monatsbeleg für aktuellen Monat bereits vorhanden');
            return;
        }

        setBusy('demo-bulk');
        try {
            await postJson('/api/rksv/special-receipts/monatsbeleg', {
                cashRegisterId: registerId,
                year: viennaYear,
                month: viennaMonth,
                reason: 'Demo Helper: Monatsbeleg aktueller Monat',
            });
            await invalidateLists();
            message.success(`Monatsbeleg für ${formatMonthYearDe(viennaYear, viennaMonth)} erstellt.`);
        } catch (e: unknown) {
            const err = e as { response?: { data?: { message?: string } }; message?: string };
            message.error(String(err?.response?.data?.message ?? err?.message ?? 'Fehler'));
        } finally {
            setBusy(null);
        }
    }, [registerId, registerScopedReceipts, viennaYear, viennaMonth, postJson, invalidateLists, message]);

    const onCreateDemoNullbelegForCurrentMonth = useCallback(async () => {
        if (!registerId) {
            message.warning('Bitte eine Kasse wählen.');
            return;
        }

        setBusy('demo-null');
        try {
            await postJson('/api/rksv/special-receipts/nullbeleg', {
                cashRegisterId: registerId,
                year: viennaYear,
                month: viennaMonth,
                reason: 'Demo Helper: Test-Nullbeleg',
                actsAsJahresbeleg: viennaMonth === 12 ? true : null,
            });
            message.success('Test-Nullbeleg für aktuellen Monat erstellt.');
            await invalidateLists();
        } catch (e: unknown) {
            const err = e as { response?: { data?: { message?: string } }; message?: string };
            message.error(String(err?.response?.data?.message ?? err?.message ?? 'Fehler'));
        } finally {
            setBusy(null);
        }
    }, [registerId, viennaYear, viennaMonth, postJson, invalidateLists]);

    const onResetTseSimulation = useCallback(async () => {
        setBusy('demo-tse-reset');
        try {
            setMonatPeriod(dayjs(`${viennaYear}-${String(viennaMonth).padStart(2, '0')}-01`));
            setJahrPeriod(dayjs(`${defaultYear}-01-01`));
            setJbEarly('');
            setReasonShort('');
            await queryClient.invalidateQueries({ queryKey: ['/api/tse/health'] });
            message.success('TSE-Simulation zurückgesetzt (Demo-Helfer).');
        } finally {
            setBusy(null);
        }
    }, [viennaYear, viennaMonth, defaultYear, queryClient]);

    const specialColumns: ColumnsType<OrvalReceiptRow> = useMemo(
        () => [
            {
                title: 'Belegnummer',
                dataIndex: 'receiptNumber',
                key: 'receiptNumber',
                render: (t: string, row) => <Link href={`/receipts/${row.receiptId}`}>{t || '—'}</Link>,
            },
            {
                title: 'Typ',
                dataIndex: 'rksvSpecialReceiptKind',
                key: 'kind',
                render: (k: string | null | undefined) => <Typography.Text>{rksvSpecialReceiptKindLabelDe(k)}</Typography.Text>,
            },
            {
                title: 'Periode',
                key: 'period',
                render: (_: unknown, row) => {
                    const y = Number(row.rksvSpecialReceiptYear ?? 0);
                    const m = Number(row.rksvSpecialReceiptMonth ?? 0);
                    if (y > 0 && m > 0) return `${formatMonthYearDe(y, m)} (${y}-${String(m).padStart(2, '0')})`;
                    if (y > 0) return String(y);
                    return '—';
                },
            },
            {
                title: 'Status',
                key: 'status',
                render: (_: unknown, row) => {
                    const kind = normalizeSpecialKind(row.rksvSpecialReceiptKind);
                    if (kind === 'schlussbeleg') return <Tag color="red">Stillgelegt</Tag>;
                    if (kind === 'startbeleg') return <Tag color="blue">Initial erstellt</Tag>;
                    if (receiptIsLateCreated(row)) return <Tag color="orange">Verspätet erstellt</Tag>;
                    return <Tag color="green">Erstellt</Tag>;
                },
            },
            {
                title: 'Datum',
                dataIndex: 'issuedAt',
                key: 'issuedAt',
                render: (d: string) => (d ? formatDateTime(d, '') : '—'),
            },
            {
                title: 'FinanzOnline',
                key: 'fon',
                render: (_: unknown, row) => {
                    if (!isRksvFinanzOnlineTrackedSpecialReceiptKind(row.rksvSpecialReceiptKind)) {
                        return <Typography.Text type="secondary">—</Typography.Text>;
                    }
                    const st = row.rksvFinanzOnlineSubmissionStatus;
                    if (!st?.trim()) return <Typography.Text type="secondary">—</Typography.Text>;
                    return <Tag color={rksvFinanzOnlineSubmissionStatusTagColor(st)}>{rksvFinanzOnlineSubmissionStatusLabelDe(st)}</Tag>;
                },
            },
            {
                title: 'Betrag',
                dataIndex: 'grandTotal',
                key: 'grandTotal',
                align: 'right',
                render: (v: number | undefined) => formatEUR(v ?? 0),
            },
            {
                title: 'PDF',
                key: 'pdf',
                width: 90,
                align: 'center',
                render: (_: unknown, row) =>
                    row.paymentId ? (
                        <StoredReportPdfButton
                            reportType={reportPdfTypeFromSpecialReceiptKind(row.rksvSpecialReceiptKind)}
                            targetId={row.paymentId}
                            fileNameBase={row.receiptNumber}
                            size="small"
                        />
                    ) : (
                        '—'
                    ),
            },
            {
                title: 'Aktionen',
                key: 'actions',
                render: (_: unknown, row) => (
                    <Space wrap>
                        <Link href={`/receipts/${row.receiptId}`}>
                            <Button size="small">Anzeigen</Button>
                        </Link>
                        {row.paymentId ? (
                            <ReprintButton
                                paymentId={row.paymentId}
                                receiptNumber={row.receiptNumber}
                                size="small"
                            />
                        ) : null}
                    </Space>
                ),
            },
        ],
        [],
    );

    const actionDisabledBase = !registerId || busy !== null || selectedRegisterIsDecommissioned;

    const missingMonatsbelegColumns: ColumnsType<MissingMonatsbelegTableRow> = useMemo(
        () => [
            {
                title: 'Periode',
                key: 'period',
                render: (_, record) => formatMonthYearDe(record.year, record.month),
            },
            {
                title: 'Rückstand',
                key: 'delay',
                width: 160,
                render: (_, record) => {
                    const severity = monatsbelegDelaySeverity(record.monthDiff);
                    return (
                        <Space size={6}>
                            <Tag color={severity.color}>{severity.label}</Tag>
                            {record.monthDiff > 0 ? (
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    {record.monthDiff} {record.monthDiff === 1 ? 'Monat' : 'Monate'}
                                </Typography.Text>
                            ) : (
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    aktueller Monat
                                </Typography.Text>
                            )}
                        </Space>
                    );
                },
            },
            {
                title: 'Status',
                key: 'status',
                width: 120,
                render: (_, record) =>
                    record.isOverdue ? <Tag color="red">Überfällig</Tag> : <Tag color="orange">Fehlt</Tag>,
            },
            {
                title: 'Aktion',
                key: 'action',
                width: 160,
                render: (_, record) => (
                    <Space>
                        {record.isMissing ? (
                            <Button
                                type="primary"
                                size="small"
                                disabled={actionDisabledBase || !canMonat}
                                onClick={() => openMissingMonatsbelegModal(record.year, record.month)}
                            >
                                {record.monthDiff > 0 ? 'Nachträglich erstellen' : 'Jetzt erstellen'}
                            </Button>
                        ) : null}
                    </Space>
                ),
            },
        ],
        [actionDisabledBase, canMonat, openMissingMonatsbelegModal],
    );

    return (
        <>
            <AdminPageHeader
                title="RKSV Sonderbelege"
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: ADMIN_NAV_GROUP_LABELS.rksv, href: '/rksv' },
                    { title: 'RKSV Sonderbelege' },
                ]}
            />

            <Card style={{ marginBottom: 16 }}>
                <Space orientation="vertical" style={{ width: '100%' }} size="middle">
                    <div>
                        <Typography.Text strong>Kasse auswählen</Typography.Text>
                        <Select
                            showSearch
                            allowClear
                            placeholder="Kasse wählen"
                            style={{ width: '100%', marginTop: 8 }}
                            loading={registersLoading}
                            optionFilterProp="label"
                            value={registerId}
                            onChange={(v) => setRegisterId(v)}
                            options={registerOptions}
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary">Optionaler Grund / Notiz (für Sonderbelege)</Typography.Text>
                        <Input value={reasonShort} onChange={(e) => setReasonShort(e.target.value)} maxLength={450} style={{ marginTop: 8 }} />
                    </div>
                    {selectedRegister ? (
                        <Alert
                            type={selectedRegisterIsDecommissioned ? 'warning' : 'info'}
                            showIcon
                            title={`Betriebsstatus: ${registerBetriebsstatusDe(selectedRegisterStatus)}`}
                            description={
                                selectedRegisterIsDecommissioned
                                    ? 'Diese Kasse wurde bereits stillgelegt. Es können keine neuen Sonderbelege erstellt werden.'
                                    : `Kasse: ${formatRegisterDisplayLabel(selectedRegister.registerNumber) || selectedRegister.id}`
                            }
                        />
                    ) : (
                        <Alert type="info" showIcon title="Bitte zuerst eine Kasse auswählen." />
                    )}
                </Space>
            </Card>

            <LateMonatsbelegCreationCard
                cashRegisterId={registerId}
                canCreate={canMonat}
                disabled={actionDisabledBase}
                onSuccess={() => {
                    void refetchMonatsbelegData();
                }}
            />

            {isDevelopment && canTestHelper ? (
                <Card id="rksv-focus-test-helper" title="Test Helper (Demo-Modus)" style={{ marginBottom: 16 }}>
                    <Space orientation="vertical" style={{ width: '100%' }} size="middle">
                        <Alert
                            type="warning"
                            showIcon
                            title="Demo-Modus: Beachten Sie, dass diese Belege nur zu Testzwecken dienen und nicht für den Produktivbetrieb verwendet werden dürfen."
                        />
                        <Space wrap>
                            <Button
                                onClick={() => void onBulkCreateMissingMonatsbelege()}
                                loading={busy === 'demo-bulk'}
                                disabled={!registerId || busy !== null}
                            >
                                Monatsbeleg für aktuellen Monat erstellen
                            </Button>
                            <Button
                                onClick={() => void onCreateDemoNullbelegForCurrentMonth()}
                                loading={busy === 'demo-null'}
                                disabled={!registerId || busy !== null}
                            >
                                Test-Nullbeleg für aktuellen Monat erstellen
                            </Button>
                            {canTseSimulation ? (
                                <Button
                                    danger
                                    onClick={() => void onResetTseSimulation()}
                                    loading={busy === 'demo-tse-reset'}
                                    disabled={busy !== null}
                                >
                                    TSE-Simulation zurücksetzen
                                </Button>
                            ) : null}
                        </Space>
                    </Space>
                </Card>
            ) : null}

            <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
                <Col xs={24} md={12}>
                    <Card
                        title={titleWithTooltip(
                            'Startbeleg',
                            'Der Startbeleg muss unmittelbar nach der ersten Inbetriebnahme der Kasse erstellt werden, bevor ein regulärer Zahlungsvorgang durchgeführt werden kann. Nur ein Startbeleg pro Kasse möglich.',
                        )}
                        id="rksv-focus-startbeleg"
                    >
                        <Space orientation="vertical" style={{ width: '100%' }}>
                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                Erster Beleg nach Kassenaktivierung. Nur einmal pro Kasse möglich.
                            </Typography.Paragraph>
                            <Typography.Text type="secondary">
                                Hinweis: Vor dem ersten regulären Verkauf muss der Startbeleg vorhanden sein.
                            </Typography.Text>
                            <Button
                                type="primary"
                                onClick={() => void onStartbeleg()}
                                disabled={actionDisabledBase || hasStartbelegForRegister || !canStart}
                                loading={busy === 'start'}
                                block
                            >
                                Startbeleg erstellen
                            </Button>
                            {hasStartbelegForRegister ? <Alert type="success" showIcon title="Für diese Kasse ist bereits ein Startbeleg vorhanden." /> : null}
                        </Space>
                    </Card>
                </Col>

                <Col xs={24} md={12}>
                    <Card
                        title={titleWithTooltip(
                            'Monatsbeleg',
                            'Für jeden Kalendermonat muss ein Monatsbeleg erstellt werden, spätestens bis zum Ende des Folgemonats. Der Monatsbeleg dient der monatlichen Kontrolle der Registrierkasse.',
                        )}
                    >
                        <Space orientation="vertical" style={{ width: '100%' }}>
                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                Monatlicher Kontrollbeleg. Pflicht für jeden Kalendermonat.
                            </Typography.Paragraph>
                            <Typography.Text type="secondary">
                                Empfohlen: Monatsbeleg direkt nach Monatsende erstellen.
                            </Typography.Text>
                            <DatePicker
                                picker="month"
                                value={monatPeriod}
                                onChange={(v) => v && setMonatPeriod(v)}
                                disabledDate={(current) => {
                                    if (!current) return false;
                                    const y = current.year();
                                    const m = current.month() + 1;
                                    return getMonthDifference(y, m) < 0;
                                }}
                                style={{ width: '100%' }}
                            />
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                Aktueller und vergangene Kalendermonate (Europe/Vienna) sind möglich. Vergangene Monate
                                erfordern eine Bestätigung.
                            </Typography.Text>
                            <Button
                                type="primary"
                                onClick={openMonatsbelegModal}
                                disabled={
                                    actionDisabledBase ||
                                    hasMonatsbelegForPeriod ||
                                    !canMonat ||
                                    monatIsFutureMonth
                                }
                                block
                            >
                                {`Monatsbeleg für ${formatMonthYearDe(monatYear, monatMonth)} erstellen`}
                            </Button>
                            {hasMonatsbelegForPeriod ? <Alert type="success" showIcon title="Monatsbeleg für den gewählten Zeitraum ist bereits vorhanden." /> : null}
                        </Space>
                    </Card>
                </Col>

                <Col xs={24} md={12}>
                    <Card
                        title={titleWithTooltip(
                            'Jahresbeleg',
                            'Ein Jahresbeleg ist für jedes Kalenderjahr zu erstellen, spätestens bis zum 31. Jänner des Folgejahres. Der Monatsbeleg Dezember kann als Jahresbeleg verwendet werden.',
                        )}
                    >
                        <Space orientation="vertical" style={{ width: '100%' }}>
                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                Jährlicher Kontrollbeleg. Dezember Monatsbeleg kann als Jahresbeleg dienen.
                            </Typography.Paragraph>
                            <Typography.Text type="secondary">
                                Frist beachten: Erstellung bis spätestens 31. Jänner des Folgejahres.
                            </Typography.Text>
                            <DatePicker picker="year" value={jahrPeriod} onChange={(v) => v && setJahrPeriod(v)} style={{ width: '100%' }} />
                            <Input
                                placeholder="Optional: Grund bei vorzeitiger Erstellung"
                                value={jbEarly}
                                onChange={(e) => setJbEarly(e.target.value)}
                                maxLength={450}
                            />
                            <Button
                                type="primary"
                                onClick={confirmJahresbeleg}
                                disabled={actionDisabledBase || hasJahresbelegForYear || !canJahr}
                                loading={busy === 'jahr'}
                                block
                            >
                                {`Jahresbeleg für ${jahrYear} erstellen`}
                            </Button>
                            {hasJahresbelegForYear ? <Alert type="success" showIcon title="Jahresbeleg für das gewählte Jahr ist bereits vorhanden." /> : null}
                        </Space>
                    </Card>
                </Col>

                <Col xs={24} md={12}>
                    <Card
                        title={titleWithTooltip(
                            'Nullbeleg',
                            'Der Nullbeleg ist ein Beleg mit Null-Betrag. Er kann zu Kontrollzwecken oder als Ersatz für den Monatsbeleg in bestimmten Ausnahmefällen (z.B. bei Umsatzsteuerbefreiung) verwendet werden.',
                        )}
                    >
                        <Space orientation="vertical" style={{ width: '100%' }}>
                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                Der Nullbeleg wird nur bei einer Kassennachschau auf amtliche Aufforderung benötigt.
                            </Typography.Paragraph>
                            <Typography.Text type="secondary">
                                Keine Planung oder Erinnerung erforderlich. Nur für Prüfzwecke.
                            </Typography.Text>
                            <Button
                                type="primary"
                                onClick={() => void onNullbeleg()}
                                disabled={actionDisabledBase || !canNull}
                                loading={busy === 'null'}
                                block
                            >
                                Nullbeleg für Prüfzwecke erstellen
                            </Button>
                            {hasNullbelegForRegister ? (
                                <Alert
                                    type="info"
                                    showIcon
                                    title="Für diese Kasse existiert bereits mindestens ein Nullbeleg."
                                />
                            ) : null}
                        </Space>
                    </Card>
                </Col>

                <Col xs={24}>
                    <Card
                        id="rksv-focus-schlussbeleg"
                        title={titleWithTooltip(
                            'Schlussbeleg / Endbeleg',
                            'Der Schlussbeleg wird bei endgültiger Stilllegung der Kasse erstellt. Nach Erstellung kann die Kasse keine weiteren Zahlungen mehr annehmen. Dies kann nicht rückgängig gemacht werden.',
                        )}
                        styles={{ body: { border: '1px solid #ffccc7', borderRadius: 8, background: '#fff1f0' } }}
                    >
                        <Space orientation="vertical" style={{ width: '100%' }}>
                            <Typography.Paragraph strong style={{ color: '#a8071a', marginBottom: 0 }}>
                                Endgültige Stilllegung der Kasse. Nach Erstellung kann die Kasse keine Zahlungen mehr annehmen.
                            </Typography.Paragraph>
                            <Alert
                                type="warning"
                                showIcon
                                title='Endbeleg wird NUR bei endgültiger Außerbetriebnahme verwendet (keine Saisonpause!).'
                            />
                            <Alert
                                type="error"
                                showIcon
                                title="Achtung: Dieser Vorgang ist dauerhaft und kann nicht rückgängig gemacht werden."
                            />
                            <Typography.Text type="secondary">
                                Nur verwenden, wenn die Kasse endgültig außer Betrieb genommen wird.
                            </Typography.Text>
                            <Typography.Text type="secondary">
                                {'Nach Erstellung wird der Status auf "Decommissioned" gesetzt. Neue Zahlungen sind danach nicht mehr erlaubt.'}
                            </Typography.Text>
                            {!canCreateSchlussbelegNow ? (
                                <Alert
                                    type="warning"
                                    showIcon
                                    title={
                                        selectedRegisterHasOpenSession
                                            ? 'Nicht verfügbar: Es besteht eine offene Sitzung. Bitte Sitzung schließen.'
                                            : 'Nicht verfügbar: Endbeleg nur bei Kassenstatus „Geschlossen".'
                                    }
                                />
                            ) : null}
                            <Button
                                danger
                                type="primary"
                                onClick={openSchlussbelegDialog}
                                disabled={actionDisabledBase || hasSchlussbelegForRegister || !canSchluss || !canCreateSchlussbelegNow}
                                loading={busy === 'schluss'}
                                block
                            >
                                Kasse stilllegen (Endbeleg)
                            </Button>
                            {hasSchlussbelegForRegister ? <Alert type="warning" showIcon title="Für diese Kasse existiert bereits ein Schlussbeleg." /> : null}
                        </Space>
                    </Card>
                </Col>
            </Row>

            <Card title="Zuletzt erstellte Sonderbelege (mit Zweck)" style={{ marginBottom: 16 }} loading={scanLoading}>
                {recentSpecialReceipts.length === 0 ? (
                    <Alert type="info" showIcon title="Noch keine Sonderbelege vorhanden." />
                ) : (
                    <Row gutter={[12, 12]}>
                        {recentSpecialReceipts.map((row) => {
                            const kind = normalizeSpecialKind(row.rksvSpecialReceiptKind);
                            const badge = specialReceiptBadge(kind);
                            const y = Number(row.rksvSpecialReceiptYear ?? 0);
                            const m = Number(row.rksvSpecialReceiptMonth ?? 0);
                            const periodText =
                                kind === 'monatsbeleg' && y > 0 && m > 0
                                    ? `Abgedeckter Monat: ${formatMonthYearDe(y, m)}`
                                    : kind === 'jahresbeleg' && y > 0
                                      ? `Abgedecktes Jahr: ${y}`
                                      : kind === 'nullbeleg' && y > 0 && m > 0
                                        ? `Bezogen auf: ${formatMonthYearDe(y, m)}`
                                        : 'Periode: —';
                            return (
                                <Col xs={24} md={12} lg={8} key={row.receiptId ?? `${row.receiptNumber}-${row.issuedAt}`}>
                                    <Card size="small">
                                        <Space orientation="vertical" size={6} style={{ width: '100%' }}>
                                            <Space>
                                                <Tag color={badge.color}>{badge.text}</Tag>
                                                {receiptIsLateCreated(row) ? (
                                                    <Tag color="orange">Verspätet erstellt</Tag>
                                                ) : (
                                                    <Tag color="green">Erfolgreich erstellt</Tag>
                                                )}
                                            </Space>
                                            <Typography.Text strong>{row.receiptNumber || 'Ohne Belegnummer'}</Typography.Text>
                                            <Typography.Text type="secondary">
                                                Erstellt am: {row.issuedAt ? formatDateTime(row.issuedAt, '') : '—'}
                                            </Typography.Text>
                                            <Typography.Text>{periodText}</Typography.Text>
                                            <Typography.Text type="secondary">{specialReceiptPurposeDe(kind)}</Typography.Text>
                                            <Space wrap>
                                                {row.receiptId ? (
                                                    <Link href={`/receipts/${row.receiptId}`}>
                                                        <Button size="small">Details öffnen</Button>
                                                    </Link>
                                                ) : null}
                                                {row.paymentId ? (
                                                    <>
                                                        <StoredReportPdfButton
                                                            reportType={reportPdfTypeFromSpecialReceiptKind(row.rksvSpecialReceiptKind)}
                                                            targetId={row.paymentId}
                                                            fileNameBase={row.receiptNumber}
                                                            size="small"
                                                        />
                                                        <ReprintButton
                                                            paymentId={row.paymentId}
                                                            receiptNumber={row.receiptNumber}
                                                            size="small"
                                                        />
                                                    </>
                                                ) : null}
                                            </Space>
                                        </Space>
                                    </Card>
                                </Col>
                            );
                        })}
                    </Row>
                )}
            </Card>

            <Card
                id="rksv-missing-monatsbelege"
                title="Fehlende Monatsbelege"
                style={{ marginBottom: 16 }}
                loading={Boolean(registerId) && monatsbelegStatusQuery.isLoading}
            >
                {!registerId ? (
                    <Alert type="info" showIcon title="Bitte zuerst eine Kasse auswählen." />
                ) : (
                    <Space orientation="vertical" style={{ width: '100%' }} size="middle">
                        <Alert
                            type="info"
                            showIcon
                            title="Hinweise zu vergangenen Monatsbelegen"
                            description={
                                <ul style={{ marginBottom: 0, paddingLeft: 20 }}>
                                    <li>Aktueller Monat: direkt erstellbar</li>
                                    <li>1 Monat zurück: Info-Hinweis</li>
                                    <li>2–6 Monate zurück: Warnung, Bestätigung erforderlich</li>
                                    <li>Über 6 Monate: erhöhtes Risiko, Audit-Log</li>
                                </ul>
                            }
                        />
                        <Table<MissingMonatsbelegTableRow>
                            rowKey="key"
                            size="small"
                            pagination={missingMonatsbelegRows.length > 12 ? { pageSize: 12 } : false}
                            columns={missingMonatsbelegColumns}
                            dataSource={missingMonatsbelegRows}
                            locale={{ emptyText: 'Keine fehlenden Monatsbelege für diese Kasse.' }}
                        />
                    </Space>
                )}
            </Card>

            <Card
                title={`Monatsbeleg Timeline ${monatYear}`}
                style={{ marginBottom: 16 }}
                extra={
                    <DatePicker
                        picker="year"
                        value={dayjs(`${monatYear}-01-01`)}
                        onChange={(v) => v && setMonatPeriod((prev) => prev.year(v.year()))}
                    />
                }
            >
                {!registerId ? (
                    <Alert type="info" showIcon title="Für die Timeline zuerst eine Kasse auswählen." />
                ) : (
                    <Row gutter={[12, 12]}>
                        {monthlyTimelineRows.map((item) => {
                            const isDone = item.status === 'done';
                            const isMissing = item.status === 'missing';
                            const color = isDone ? '#f6ffed' : isMissing ? '#fff2f0' : '#fafafa';
                            const borderColor = isDone ? '#b7eb8f' : isMissing ? '#ffccc7' : '#d9d9d9';
                            const icon = isDone ? '✓' : isMissing ? '!' : '•';
                            const label = isDone ? 'Abgeschlossen' : isMissing ? 'Fehlt' : 'Ausstehend';
                            return (
                                <Col xs={12} sm={8} md={6} lg={4} xl={3} key={`timeline-${item.month}`}>
                                    <Card
                                        size="small"
                                        styles={{
                                            body: {
                                                background: color,
                                                border: `1px solid ${borderColor}`,
                                                borderRadius: 8,
                                            },
                                        }}
                                    >
                                        <Space orientation="vertical" size={4} style={{ width: '100%' }}>
                                            <Typography.Text strong>{monthShortNameDe(item.month)}</Typography.Text>
                                            <Typography.Text>{icon}</Typography.Text>
                                            <Typography.Text type="secondary">{label}</Typography.Text>
                                        </Space>
                                    </Card>
                                </Col>
                            );
                        })}
                    </Row>
                )}
            </Card>

            <Card title="Bestehende Sonderbelege" loading={scanLoading}>
                <Table<OrvalReceiptRow>
                    rowKey={(r) => r.receiptId ?? ''}
                    dataSource={registerId ? registerScopedReceipts : receiptScan ?? []}
                    columns={specialColumns}
                    pagination={{ pageSize: 12 }}
                    size="small"
                    locale={{
                        emptyText: registerId
                            ? 'Für die ausgewählte Kasse wurden keine Sonderbelege gefunden.'
                            : 'Keine Sonderbelege in den letzten 300 Belegen.',
                    }}
                />
            </Card>

            {registerId ? (
                <CreateMonatsbelegModal
                    open={monatsbelegModalOpen}
                    cashRegisterId={registerId}
                    year={selectedMonatsbelegYear}
                    month={selectedMonatsbelegMonth}
                    reason={reasonShort.trim() || 'Admin Monatsbeleg'}
                    onClose={() => setMonatsbelegModalOpen(false)}
                    onSuccess={() => {
                        void refetchMonatsbelegData();
                        setMonatsbelegModalOpen(false);
                    }}
                />
            ) : null}

            <Modal
                title="Kasse stilllegen (Endbeleg)"
                open={schlussModalOpen}
                onCancel={() => {
                    setSchlussModalOpen(false);
                    setSchlussConfirmText('');
                }}
                okText="Endbeleg endgültig erstellen"
                okButtonProps={{ danger: true, loading: busy === 'schluss' }}
                onOk={() => void submitSchlussModal()}
            >
                <Typography.Paragraph strong>
                    Diese Aktion deaktiviert die Kasse dauerhaft. Nach dem Endbeleg sind keine neuen Zahlungen oder Kassiervorgänge mehr möglich.
                </Typography.Paragraph>
                <Typography.Paragraph type="warning">
                    Nicht für Feiertage, Betriebsferien oder saisonale Pausen verwenden.
                </Typography.Paragraph>
                <Alert
                    type="error"
                    showIcon
                    style={{ marginBottom: 12 }}
                    title="Starke Bestätigung erforderlich"
                    description='Gib zur Bestätigung exakt «ENDBELEG» ein. Status wird auf "Decommissioned" gesetzt.'
                />
                <Input
                    placeholder="ENDBELEG"
                    value={schlussConfirmText}
                    onChange={(e) => setSchlussConfirmText(e.target.value)}
                    autoComplete="off"
                />
            </Modal>

        </>
    );
}
