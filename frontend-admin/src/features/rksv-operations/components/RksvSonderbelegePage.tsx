'use client';

/**
 * Bu ana bileşen RKSV Sonderbelege işlemlerini daha anlaşılır kart düzeninde sunar.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import {
    Alert,
    Button,
    Card,
    Col,
    DatePicker,
    Input,
    Modal,
    Row,
    Select,
    Space,
    Table,
    Tag,
    Tooltip,
    Typography,
    message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { InfoCircleOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import { getApiReceiptsList } from '@/api/generated/receipts/receipts';
import type { CashRegister, ReceiptListItemDto as OrvalReceiptRow } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_GROUP_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { formatEUR } from '@/shared/utils/currency';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';
import { ReprintButton } from '@/features/payments/components/ReprintButton';
import { rksvSpecialReceiptKindLabelDe } from '@/features/rksv-operations/rksvSpecialReceiptDisplay';
import {
    isRksvFinanzOnlineTrackedSpecialReceiptKind,
    rksvFinanzOnlineSubmissionStatusLabelDe,
    rksvFinanzOnlineSubmissionStatusTagColor,
} from '@/features/receipts/utils/rksvFinanzOnlineSubmissionUi';

function getViennaCalendarYear(now: Date = new Date()): number {
    const fmt = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Vienna', year: 'numeric' });
    const y = fmt.formatToParts(now).find((p) => p.type === 'year')?.value;
    return y ? Number(y) : now.getUTCFullYear();
}

function getViennaCalendarYearMonth(now: Date = new Date()): { year: number; month: number } {
    const fmt = new Intl.DateTimeFormat('en-CA', {
        timeZone: 'Europe/Vienna',
        year: 'numeric',
        month: '2-digit',
    });
    const parts = fmt.formatToParts(now);
    const year = Number(parts.find((p) => p.type === 'year')?.value) || now.getUTCFullYear();
    const month = Number(parts.find((p) => p.type === 'month')?.value) || 1;
    return { year, month };
}

function formatMonthYearDe(year: number, month: number): string {
    return new Intl.DateTimeFormat('de-DE', {
        month: 'long',
        year: 'numeric',
        timeZone: 'Europe/Vienna',
    }).format(new Date(Date.UTC(year, month - 1, 1)));
}

function normalizeRegisterRows(data: unknown): CashRegister[] {
    if (Array.isArray(data)) return data as CashRegister[];
    if (data && typeof data === 'object' && 'registers' in data) {
        const r = (data as { registers?: CashRegister[] }).registers;
        if (Array.isArray(r)) return r;
    }
    return [];
}

function rawRegisterStatus(reg: CashRegister): number | undefined {
    const r = reg as unknown as { status?: number };
    return typeof r.status === 'number' ? r.status : undefined;
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
    const { hasPermission } = usePermissions();
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();

    const canNull = hasPermission(PERMISSIONS.RKSV_NULLBELEG_CREATE);
    const canStart = hasPermission(PERMISSIONS.RKSV_STARTBELEG_CREATE);
    const canMonat = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);
    const canJahr = hasPermission(PERMISSIONS.RKSV_JAHRESBELEG_CREATE);
    const canSchluss = hasPermission(PERMISSIONS.RKSV_SCHLUSSBELEG_CREATE);
    const isDevelopment = process.env.NODE_ENV === 'development';

    const { data: registersRaw, isLoading: registersLoading } = useGetApiCashRegister();
    const registers = useMemo(() => normalizeRegisterRows(registersRaw), [registersRaw]);

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

    useEffect(() => {
        const q = searchParams.get('registerId')?.trim();
        if (q) setRegisterId(q);
    }, [searchParams]);

    useEffect(() => {
        const focus = searchParams.get('focus')?.trim();
        if (focus !== 'startbeleg' && focus !== 'schlussbeleg') return;
        const id = focus === 'startbeleg' ? 'rksv-focus-startbeleg' : 'rksv-focus-schlussbeleg';
        requestAnimationFrame(() => {
            document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
    }, [searchParams]);

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
    }, [registerId, viennaYear, reasonShort, postJson, invalidateLists]);

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

    const onMonatsbeleg = useCallback(async () => {
        if (!registerId) return message.warning('Bitte eine Kasse wählen.');
        setBusy('monat');
        try {
            await postJson('/api/rksv/special-receipts/monatsbeleg', {
                cashRegisterId: registerId,
                year: monatYear,
                month: monatMonth,
                reason: reasonShort.trim() || 'Admin Monatsbeleg',
            });
            message.success('Monatsbeleg erstellt.');
            await invalidateLists();
        } catch (e: unknown) {
            const err = e as { response?: { data?: { message?: string } }; message?: string };
            message.error(String(err?.response?.data?.message ?? err?.message ?? 'Fehler'));
        } finally {
            setBusy(null);
        }
    }, [registerId, monatYear, monatMonth, reasonShort, postJson, invalidateLists]);

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
        Modal.confirm({
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

        const missingMonths = Array.from({ length: viennaMonth }, (_, i) => i + 1).filter((month) => {
            return !registerScopedReceipts.some(
                (row) =>
                    isKind(row, 'monatsbeleg') &&
                    Number(row.rksvSpecialReceiptYear ?? 0) === viennaYear &&
                    Number(row.rksvSpecialReceiptMonth ?? 0) === month,
            );
        });

        if (missingMonths.length === 0) {
            message.info('Für das aktuelle Jahr fehlen keine Monatsbelege.');
            return;
        }

        setBusy('demo-bulk');
        let success = 0;
        let failed = 0;
        for (const month of missingMonths) {
            try {
                await postJson('/api/rksv/special-receipts/monatsbeleg', {
                    cashRegisterId: registerId,
                    year: viennaYear,
                    month,
                    reason: 'Demo Helper: Bulk Monatsbelege',
                });
                success += 1;
            } catch {
                failed += 1;
            }
        }

        await invalidateLists();
        if (failed === 0) {
            message.success(`${success} fehlende Monatsbelege wurden erstellt.`);
        } else {
            message.warning(`${success} Monatsbelege erstellt, ${failed} fehlgeschlagen.`);
        }
        setBusy(null);
    }, [registerId, viennaMonth, registerScopedReceipts, viennaYear, postJson, invalidateLists]);

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
                    return <Tag color="green">Erstellt</Tag>;
                },
            },
            {
                title: 'Datum',
                dataIndex: 'issuedAt',
                key: 'issuedAt',
                render: (d: string) => (d ? dayjs(d).format('DD.MM.YYYY HH:mm') : '—'),
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
                <Space direction="vertical" style={{ width: '100%' }} size="middle">
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
                            message={`Betriebsstatus: ${registerBetriebsstatusDe(selectedRegisterStatus)}`}
                            description={
                                selectedRegisterIsDecommissioned
                                    ? 'Diese Kasse wurde bereits stillgelegt. Es können keine neuen Sonderbelege erstellt werden.'
                                    : `Kasse: ${formatRegisterDisplayLabel(selectedRegister.registerNumber) || selectedRegister.id}`
                            }
                        />
                    ) : (
                        <Alert type="info" showIcon message="Bitte zuerst eine Kasse auswählen." />
                    )}
                </Space>
            </Card>

            {isDevelopment ? (
                <Card title="Test Helper (Demo-Modus)" style={{ marginBottom: 16 }}>
                    <Space direction="vertical" style={{ width: '100%' }} size="middle">
                        <Alert
                            type="warning"
                            showIcon
                            message="Demo-Modus: Beachten Sie, dass diese Belege nur zu Testzwecken dienen und nicht für den Produktivbetrieb verwendet werden dürfen."
                        />
                        <Space wrap>
                            <Button
                                onClick={() => void onBulkCreateMissingMonatsbelege()}
                                loading={busy === 'demo-bulk'}
                                disabled={!registerId || busy !== null}
                            >
                                Alle fehlenden Monatsbelege für das aktuelle Jahr erstellen
                            </Button>
                            <Button
                                onClick={() => void onCreateDemoNullbelegForCurrentMonth()}
                                loading={busy === 'demo-null'}
                                disabled={!registerId || busy !== null}
                            >
                                Test-Nullbeleg für aktuellen Monat erstellen
                            </Button>
                            <Button
                                danger
                                onClick={() => void onResetTseSimulation()}
                                loading={busy === 'demo-tse-reset'}
                                disabled={busy !== null}
                            >
                                TSE-Simulation zurücksetzen
                            </Button>
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
                        <Space direction="vertical" style={{ width: '100%' }}>
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
                            {hasStartbelegForRegister ? <Alert type="success" showIcon message="Für diese Kasse ist bereits ein Startbeleg vorhanden." /> : null}
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
                        <Space direction="vertical" style={{ width: '100%' }}>
                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                Monatlicher Kontrollbeleg. Pflicht für jeden Kalendermonat.
                            </Typography.Paragraph>
                            <Typography.Text type="secondary">
                                Empfohlen: Monatsbeleg direkt nach Monatsende erstellen.
                            </Typography.Text>
                            <DatePicker picker="month" value={monatPeriod} onChange={(v) => v && setMonatPeriod(v)} style={{ width: '100%' }} />
                            <Button
                                type="primary"
                                onClick={() => void onMonatsbeleg()}
                                disabled={actionDisabledBase || hasMonatsbelegForPeriod || !canMonat}
                                loading={busy === 'monat'}
                                block
                            >
                                {`Monatsbeleg für ${formatMonthYearDe(monatYear, monatMonth)} erstellen`}
                            </Button>
                            {hasMonatsbelegForPeriod ? <Alert type="success" showIcon message="Monatsbeleg für den gewählten Zeitraum ist bereits vorhanden." /> : null}
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
                        <Space direction="vertical" style={{ width: '100%' }}>
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
                            {hasJahresbelegForYear ? <Alert type="success" showIcon message="Jahresbeleg für das gewählte Jahr ist bereits vorhanden." /> : null}
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
                        <Space direction="vertical" style={{ width: '100%' }}>
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
                                    message="Für diese Kasse existiert bereits mindestens ein Nullbeleg."
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
                        <Space direction="vertical" style={{ width: '100%' }}>
                            <Typography.Paragraph strong style={{ color: '#a8071a', marginBottom: 0 }}>
                                Endgültige Stilllegung der Kasse. Nach Erstellung kann die Kasse keine Zahlungen mehr annehmen.
                            </Typography.Paragraph>
                            <Alert
                                type="warning"
                                showIcon
                                message='Endbeleg wird NUR bei endgültiger Außerbetriebnahme verwendet (keine Saisonpause!).'
                            />
                            <Alert
                                type="error"
                                showIcon
                                message="Achtung: Dieser Vorgang ist dauerhaft und kann nicht rückgängig gemacht werden."
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
                                    message={
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
                            {hasSchlussbelegForRegister ? <Alert type="warning" showIcon message="Für diese Kasse existiert bereits ein Schlussbeleg." /> : null}
                        </Space>
                    </Card>
                </Col>
            </Row>

            <Card title="Zuletzt erstellte Sonderbelege (mit Zweck)" style={{ marginBottom: 16 }} loading={scanLoading}>
                {recentSpecialReceipts.length === 0 ? (
                    <Alert type="info" showIcon message="Noch keine Sonderbelege vorhanden." />
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
                                        <Space direction="vertical" size={6} style={{ width: '100%' }}>
                                            <Space>
                                                <Tag color={badge.color}>{badge.text}</Tag>
                                                <Tag color="green">Erfolgreich erstellt</Tag>
                                            </Space>
                                            <Typography.Text strong>{row.receiptNumber || 'Ohne Belegnummer'}</Typography.Text>
                                            <Typography.Text type="secondary">
                                                Erstellt am: {row.issuedAt ? dayjs(row.issuedAt).format('DD.MM.YYYY HH:mm') : '—'}
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
                                                    <ReprintButton
                                                        paymentId={row.paymentId}
                                                        receiptNumber={row.receiptNumber}
                                                        size="small"
                                                    />
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
                    <Alert type="info" showIcon message="Für die Timeline zuerst eine Kasse auswählen." />
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
                                        <Space direction="vertical" size={4} style={{ width: '100%' }}>
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
                    message="Starke Bestätigung erforderlich"
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
