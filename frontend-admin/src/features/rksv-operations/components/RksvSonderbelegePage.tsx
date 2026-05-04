'use client';

/**
 * RKSV Sonderbelege: list recent special receipts, create Nullbeleg/Jahresbeleg/etc. with backend permissions,
 * and show cash register operational status (decommissioned vs active). German operator copy (AT).
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import {
    Alert,
    Button,
    Card,
    Checkbox,
    Input,
    InputNumber,
    Modal,
    Select,
    Space,
    Table,
    Tag,
    Typography,
    message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
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
import ReceiptReprintWizard from '@/features/operations-center/components/ReceiptReprintWizard';
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

export default function RksvSonderbelegePage() {
    const { hasPermission } = usePermissions();
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();

    const canNull = hasPermission(PERMISSIONS.RKSV_NULLBELEG_CREATE);
    const canStart = hasPermission(PERMISSIONS.RKSV_STARTBELEG_CREATE);
    const canMonat = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);
    const canJahr = hasPermission(PERMISSIONS.RKSV_JAHRESBELEG_CREATE);
    const canSchluss = hasPermission(PERMISSIONS.RKSV_SCHLUSSBELEG_CREATE);
    const canReprint = hasPermission(PERMISSIONS.RECEIPT_REPRINT);

    const { data: registersRaw, isLoading: registersLoading } = useGetApiCashRegister();
    const registers = useMemo(() => normalizeRegisterRows(registersRaw), [registersRaw]);

    const { year: vy, month: vm } = useMemo(() => getViennaCalendarYearMonth(), []);
    const defaultYear = useMemo(() => getViennaCalendarYear(), []);

    const [registerId, setRegisterId] = useState<string | undefined>(undefined);
    const [nbYear, setNbYear] = useState(vy);
    const [nbMonth, setNbMonth] = useState(vm);
    const [nbActsJahres, setNbActsJahres] = useState(vm === 12);
    const [mbYear, setMbYear] = useState(vy);
    const [mbMonth, setMbMonth] = useState(vm);
    const [jbYear, setJbYear] = useState(defaultYear);
    const [jbEarly, setJbEarly] = useState('');
    const [reasonShort, setReasonShort] = useState('');
    const [busy, setBusy] = useState<string | null>(null);

    const [reprintOpen, setReprintOpen] = useState(false);
    const [reprintPaymentId, setReprintPaymentId] = useState('');
    const [reprintHint, setReprintHint] = useState<string | undefined>();
    const [schlussModalOpen, setSchlussModalOpen] = useState(false);
    const [schlussConfirmText, setSchlussConfirmText] = useState('');

    useEffect(() => {
        const q = searchParams.get('registerId')?.trim();
        if (q) setRegisterId(q);
    }, [searchParams]);

    const { data: receiptScan, isLoading: scanLoading } = useQuery({
        queryKey: ['rksv-sonderbelege-recent-special', 300],
        queryFn: async () => {
            const res = await getApiReceiptsList({ page: 1, pageSize: 300, sort: 'issuedAt:desc' });
            const items = (res.items ?? []) as OrvalReceiptRow[];
            return items.filter((x) => Boolean(x.rksvSpecialReceiptKind?.trim()));
        },
    });

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
                    label: `${r.registerNumber ?? r.id} (${r.id})`,
                })),
        [registers],
    );

    const postJson = useCallback(async (path: string, body: object) => {
        return customInstance<{
            paymentId?: string;
            receiptNumber?: string;
            message?: string;
        }>({
            url: path,
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            data: body,
        });
    }, []);

    const onNullbeleg = useCallback(async () => {
        if (!registerId) {
            message.warning('Bitte eine Kasse wählen.');
            return;
        }
        setBusy('null');
        try {
            await postJson('/api/rksv/special-receipts/nullbeleg', {
                cashRegisterId: registerId,
                year: nbYear,
                month: nbMonth,
                reason: reasonShort.trim() || 'Admin Nullbeleg',
                actsAsJahresbeleg: nbActsJahres ? true : null,
            });
            message.success('Nullbeleg erstellt.');
            await invalidateLists();
        } catch (e: unknown) {
            const err = e as { response?: { data?: { message?: string } }; message?: string };
            message.error(String(err?.response?.data?.message ?? err?.message ?? 'Fehler'));
        } finally {
            setBusy(null);
        }
    }, [registerId, nbYear, nbMonth, nbActsJahres, reasonShort, postJson, invalidateLists]);

    const onStartbeleg = useCallback(async () => {
        if (!registerId) {
            message.warning('Bitte eine Kasse wählen.');
            return;
        }
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
        if (!registerId) {
            message.warning('Bitte eine Kasse wählen.');
            return;
        }
        setBusy('monat');
        try {
            await postJson('/api/rksv/special-receipts/monatsbeleg', {
                cashRegisterId: registerId,
                year: mbYear,
                month: mbMonth,
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
    }, [registerId, mbYear, mbMonth, reasonShort, postJson, invalidateLists]);

    const onJahresbeleg = useCallback(async () => {
        if (!registerId) {
            message.warning('Bitte eine Kasse wählen.');
            return;
        }
        setBusy('jahr');
        try {
            await postJson('/api/rksv/special-receipts/jahresbeleg', {
                cashRegisterId: registerId,
                year: jbYear,
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
    }, [registerId, jbYear, jbEarly, postJson, invalidateLists]);

    const onSchlussbeleg = useCallback(async () => {
        if (!registerId) {
            message.warning('Bitte eine Kasse wählen.');
            return;
        }
        setBusy('schluss');
        try {
            await postJson('/api/rksv/special-receipts/schlussbeleg', {
                cashRegisterId: registerId,
                reason: reasonShort.trim() || 'Admin Schlussbeleg',
            });
            message.success('Schlussbeleg erstellt — Kasse dauerhaft außer Betrieb.');
            await invalidateLists();
        } catch (e: unknown) {
            const err = e as { response?: { data?: { message?: string } }; message?: string };
            message.error(String(err?.response?.data?.message ?? err?.message ?? 'Fehler'));
        } finally {
            setBusy(null);
        }
    }, [registerId, reasonShort, postJson, invalidateLists]);

    const confirmJahresbeleg = useCallback(() => {
        Modal.confirm({
            title: 'Jahresbeleg erstellen',
            content: 'Dieser Vorgang kann nicht rückgängig gemacht werden.',
            okText: 'Erstellen',
            cancelText: 'Abbrechen',
            onOk: () => onJahresbeleg(),
        });
    }, [onJahresbeleg]);

    const openSchlussModal = useCallback(() => {
        if (!registerId) {
            message.warning('Bitte eine Kasse wählen.');
            return;
        }
        setSchlussConfirmText('');
        setSchlussModalOpen(true);
    }, [registerId]);

    const submitSchlussModal = useCallback(async () => {
        if (schlussConfirmText.trim() !== 'ENDGÜLTIG') {
            message.error('Bitte exakt «ENDGÜLTIG» eingeben.');
            return;
        }
        await onSchlussbeleg();
        setSchlussModalOpen(false);
        setSchlussConfirmText('');
    }, [schlussConfirmText, onSchlussbeleg]);

    const registerColumns: ColumnsType<CashRegister> = useMemo(
        () => [
            {
                title: 'Kasse (Nr.)',
                key: 'nr',
                render: (_: unknown, r) => formatRegisterDisplayLabel(r.registerNumber) || '—',
            },
            {
                title: 'Kassen-ID (UUID)',
                dataIndex: 'id',
                key: 'id',
                ellipsis: true,
                render: (id: string) => (
                    <Typography.Text code copyable style={{ fontSize: 11 }}>
                        {id}
                    </Typography.Text>
                ),
            },
            {
                title: 'Betriebsstatus',
                key: 'st',
                render: (_: unknown, r) => registerBetriebsstatusDe(rawRegisterStatus(r)),
            },
            {
                title: 'RKSV Hinweis',
                key: 'hint',
                render: (_: unknown, r) =>
                    rawRegisterStatus(r) === 5 ? (
                        <Typography.Text type="secondary">Kasse endgültig abgeschlossen.</Typography.Text>
                    ) : (
                        <Typography.Text type="secondary">
                            Startbeleg / Monatsbeleg: Hinweise beim Schichtstart auf der Kasse (POS).
                        </Typography.Text>
                    ),
            },
        ],
        [],
    );

    const specialColumns: ColumnsType<OrvalReceiptRow> = useMemo(
        () => [
            {
                title: 'Belegnummer',
                dataIndex: 'receiptNumber',
                key: 'receiptNumber',
                render: (t: string, row) => (
                    <Link href={`/receipts/${row.receiptId}`}>{t || '—'}</Link>
                ),
            },
            {
                title: 'Datum',
                dataIndex: 'issuedAt',
                key: 'issuedAt',
                render: (d: string) => (d ? dayjs(d).format('DD.MM.YYYY HH:mm') : '—'),
            },
            {
                title: 'Kasse (FK)',
                dataIndex: 'cashRegisterId',
                key: 'cashRegisterId',
                ellipsis: true,
                render: (id: string | null | undefined) =>
                    id ? (
                        <Typography.Text code style={{ fontSize: 11 }}>
                            {id}
                        </Typography.Text>
                    ) : (
                        '—'
                    ),
            },
            {
                title: 'Sonderbeleg',
                dataIndex: 'rksvSpecialReceiptKind',
                key: 'kind',
                render: (k: string | null | undefined) => (
                    <Typography.Text>{rksvSpecialReceiptKindLabelDe(k)}</Typography.Text>
                ),
            },
            {
                title: 'FinanzOnline (BMF)',
                key: 'fon',
                render: (_: unknown, row) => {
                    if (!isRksvFinanzOnlineTrackedSpecialReceiptKind(row.rksvSpecialReceiptKind)) {
                        return <Typography.Text type="secondary">—</Typography.Text>;
                    }
                    const st = row.rksvFinanzOnlineSubmissionStatus;
                    if (!st?.trim()) {
                        return <Typography.Text type="secondary">—</Typography.Text>;
                    }
                    return (
                        <Tag color={rksvFinanzOnlineSubmissionStatusTagColor(st)}>
                            {rksvFinanzOnlineSubmissionStatusLabelDe(st)}
                        </Tag>
                    );
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
                    <Space>
                        <Link href={`/receipts/${row.receiptId}`}>
                            <Button size="small">Anzeigen</Button>
                        </Link>
                        {canReprint && row.paymentId ? (
                            <Button
                                size="small"
                                onClick={() => {
                                    setReprintPaymentId(String(row.paymentId));
                                    setReprintHint(row.receiptNumber ?? undefined);
                                    setReprintOpen(true);
                                }}
                            >
                                Nachdruck
                            </Button>
                        ) : null}
                    </Space>
                ),
            },
        ],
        [canReprint],
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

            <Alert
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
                message="Übersicht Sonderbelege"
                description={
                    <Typography.Paragraph style={{ marginBottom: 0 }}>
                        Zuletzt erfasste RKSV-Sonderbelege (Auszug aus den letzten 300 Belegen). Vollständige Suche über
                        die <Link href="/receipts">Belegliste</Link>. Ob ein Start- oder Monatsbeleg gerade fehlt,
                        meldet die Kasse beim Schichtstart (POS).
                    </Typography.Paragraph>
                }
            />

            <Card title="Kassen" style={{ marginBottom: 16 }} loading={registersLoading}>
                <Table<CashRegister>
                    rowKey={(r) => String(r.id ?? '')}
                    dataSource={registers}
                    columns={registerColumns}
                    pagination={false}
                    size="small"
                />
            </Card>

            <Card title="Letzte Sonderbelege" style={{ marginBottom: 16 }} loading={scanLoading}>
                <Table<OrvalReceiptRow>
                    rowKey={(r) => r.receiptId ?? ''}
                    dataSource={receiptScan ?? []}
                    columns={specialColumns}
                    pagination={false}
                    size="small"
                    locale={{ emptyText: 'Keine Sonderbelege in den letzten 300 Belegen.' }}
                />
            </Card>

            <Card title="Manuell erstellen" style={{ marginBottom: 16 }}>
                <Space direction="vertical" style={{ width: '100%' }} size="middle">
                    <div>
                        <Typography.Text type="secondary">Kasse</Typography.Text>
                        <Select
                            showSearch
                            allowClear
                            placeholder="Kasse wählen"
                            style={{ width: '100%', marginTop: 4 }}
                            loading={registersLoading}
                            optionFilterProp="label"
                            value={registerId}
                            onChange={(v) => setRegisterId(v)}
                            options={registerOptions}
                        />
                    </div>
                    <div>
                        <Typography.Text type="secondary">Optional: Kurzgrund (Notiz)</Typography.Text>
                        <Input
                            value={reasonShort}
                            onChange={(e) => setReasonShort(e.target.value)}
                            maxLength={450}
                            style={{ marginTop: 4 }}
                        />
                    </div>

                    <Space wrap>
                        {canNull ? (
                            <Space direction="vertical" size="small" style={{ minWidth: 260 }}>
                                <Typography.Text strong>Nullbeleg</Typography.Text>
                                <Space>
                                    <InputNumber min={2000} max={2100} value={nbYear} onChange={(v) => setNbYear(Number(v) || vy)} />
                                    <InputNumber min={1} max={12} value={nbMonth} onChange={(v) => setNbMonth(Number(v) || vm)} />
                                </Space>
                                <Checkbox checked={nbActsJahres} onChange={(e) => setNbActsJahres(e.target.checked)}>
                                    Als Jahresbezug (Dezember-Nullbeleg-Flag)
                                </Checkbox>
                                <Button onClick={() => void onNullbeleg()} disabled={!registerId || busy !== null} loading={busy === 'null'}>
                                    Nullbeleg erstellen
                                </Button>
                            </Space>
                        ) : null}

                        {canStart ? (
                            <Button onClick={() => void onStartbeleg()} disabled={!registerId || busy !== null} loading={busy === 'start'}>
                                Startbeleg erstellen
                            </Button>
                        ) : null}

                        {canMonat ? (
                            <Space direction="vertical" size="small" style={{ minWidth: 220 }}>
                                <Typography.Text strong>Monatsbeleg</Typography.Text>
                                <Space>
                                    <InputNumber min={2000} max={2100} value={mbYear} onChange={(v) => setMbYear(Number(v) || vy)} />
                                    <InputNumber min={1} max={12} value={mbMonth} onChange={(v) => setMbMonth(Number(v) || vm)} />
                                </Space>
                                <Button onClick={() => void onMonatsbeleg()} disabled={!registerId || busy !== null} loading={busy === 'monat'}>
                                    Monatsbeleg erstellen
                                </Button>
                            </Space>
                        ) : null}

                        {canJahr ? (
                            <Space direction="vertical" size="small" style={{ minWidth: 240 }}>
                                <Typography.Text strong>Jahresbeleg</Typography.Text>
                                <InputNumber min={2000} max={2100} value={jbYear} onChange={(v) => setJbYear(Number(v) || defaultYear)} />
                                <Input
                                    placeholder="Optional: Hinweis vorzeitige Erstellung"
                                    value={jbEarly}
                                    onChange={(e) => setJbEarly(e.target.value)}
                                    maxLength={450}
                                />
                                <Button onClick={confirmJahresbeleg} disabled={!registerId || busy !== null} loading={busy === 'jahr'}>
                                    Jahresbeleg erstellen
                                </Button>
                            </Space>
                        ) : null}

                        {canSchluss ? (
                            <Button danger onClick={openSchlussModal} disabled={!registerId || busy !== null} loading={busy === 'schluss'}>
                                Endbeleg erstellen
                            </Button>
                        ) : null}
                    </Space>
                </Space>
            </Card>

            <Modal
                title="Endbeleg erstellen (Schlussbeleg)"
                open={schlussModalOpen}
                onCancel={() => {
                    setSchlussModalOpen(false);
                    setSchlussConfirmText('');
                }}
                okText="Schlussbeleg erstellen"
                okButtonProps={{ danger: true, loading: busy === 'schluss' }}
                onOk={() => void submitSchlussModal()}
            >
                <Typography.Paragraph>
                    Die Kasse wird dauerhaft außer Betrieb gesetzt. Bitte «ENDGÜLTIG» eintippen, um fortzufahren.
                </Typography.Paragraph>
                <Input
                    placeholder="ENDGÜLTIG"
                    value={schlussConfirmText}
                    onChange={(e) => setSchlussConfirmText(e.target.value)}
                    autoComplete="off"
                />
            </Modal>

            <ReceiptReprintWizard
                open={reprintOpen}
                onClose={() => {
                    setReprintOpen(false);
                    setReprintPaymentId('');
                    setReprintHint(undefined);
                }}
                paymentId={reprintPaymentId}
                receiptNumberHint={reprintHint}
            />
        </>
    );
}
