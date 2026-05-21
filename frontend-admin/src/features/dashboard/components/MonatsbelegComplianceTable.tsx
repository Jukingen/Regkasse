'use client';

import React, { useCallback, useMemo } from 'react';
import Link from 'next/link';
import { Alert, Button, Card, Progress, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { RegisterMonatsbelegRow } from '@/features/dashboard/hooks/useAdminMonatsbelegOverview';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import type { MissingMonth } from '@/features/dashboard/api/monatsbelegStatus';
import { customInstance } from '@/lib/axios';
import { useQueryClient } from '@tanstack/react-query';
import { isDemoAutoSuggestMonatsbelegEnabled } from '@/shared/config/demoMonatsbelegFeature';

const germanMonthYearFormatter = new Intl.DateTimeFormat('de-DE', {
    month: 'long',
    year: 'numeric',
});

function parseYearMonth(value: string | null | undefined): { year: number; month: number } | null {
    if (!value) return null;
    const match = /^(\d{4})-(\d{2})$/.exec(value.trim());
    if (!match) return null;
    const year = Number(match[1]);
    const month = Number(match[2]);
    if (!Number.isFinite(year) || !Number.isFinite(month) || month < 1 || month > 12) return null;
    return { year, month };
}

function formatMonthYear(year: number, month: number): string {
    const date = new Date(year, month - 1, 1);
    return germanMonthYearFormatter.format(date);
}

function formatLastCompletedLabel(value: string | null | undefined): string {
    const parsed = parseYearMonth(value);
    if (!parsed) return 'Kein Monatsbeleg vorhanden';
    return formatMonthYear(parsed.year, parsed.month);
}

function formatMissingMonthsLabel(missingMonths: MissingMonth[] | undefined): string {
    if (!missingMonths || missingMonths.length === 0) {
        return 'Keine fehlenden Monate';
    }

    return missingMonths
        .map((missingMonth) => {
            const monthLabel = formatMonthYear(missingMonth.year, missingMonth.month);
            return missingMonth.isOverdue ? `${monthLabel} (überfällig)` : monthLabel;
        })
        .join(', ');
}

function getYearlyProgress(missingMonths: MissingMonth[] | undefined): { completed: number; total: number; percent: number } {
    const now = new Date();
    const currentYear = now.getFullYear();
    const lastRequiredMonthInYear = Math.max(0, Math.min(12, now.getMonth()));
    const total = lastRequiredMonthInYear;
    if (total === 0) {
        return { completed: 0, total: 0, percent: 100 };
    }

    const missingCount = (missingMonths ?? []).filter(
        (m) => m.year === currentYear && m.month >= 1 && m.month <= lastRequiredMonthInYear,
    ).length;
    const completed = Math.max(0, total - missingCount);
    const percent = Math.round((completed / total) * 100);

    return { completed, total, percent };
}

function getNextMissingMonthLabel(nextRequiredMonth: string | null | undefined): string {
    const parsed = parseYearMonth(nextRequiredMonth);
    if (!parsed) return 'nächsten Monat';
    return formatMonthYear(parsed.year, parsed.month);
}

export type MonatsbelegComplianceTableProps = {
    rows: RegisterMonatsbelegRow[];
    loading: boolean;
};

export function MonatsbelegComplianceTable({ rows, loading }: MonatsbelegComplianceTableProps) {
    const { hasPermission } = usePermissions();
    const queryClient = useQueryClient();
    const canMonatsbeleg = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);
    const demoAutoSuggestEnabled = isDemoAutoSuggestMonatsbelegEnabled();

    const createMissingMonatsbelegeForTest = useCallback(async (record: RegisterMonatsbelegRow) => {
        const missingMonths = record.status?.missingMonths ?? [];
        if (missingMonths.length === 0) {
            message.info('Keine fehlenden Monate für diese Kasse.');
            return;
        }

        const failedMonths: string[] = [];
        for (const missingMonth of missingMonths) {
            try {
                await customInstance({
                    url: '/api/rksv/special-receipts/monatsbeleg',
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    data: {
                        cashRegisterId: record.registerId,
                        year: missingMonth.year,
                        month: missingMonth.month,
                        reason: 'Demo Sammeltest Monatsbeleg',
                    },
                });
            } catch {
                failedMonths.push(formatMonthYear(missingMonth.year, missingMonth.month));
            }
        }

        await queryClient.invalidateQueries({ queryKey: ['rksv', 'monatsbeleg-status', record.registerId] });

        if (failedMonths.length === 0) {
            message.success('Alle fehlenden Monate wurden als Test erstellt.');
            return;
        }

        message.warning(`Einige Monate konnten nicht erstellt werden: ${failedMonths.join(', ')}`);
    }, [queryClient]);

    const columns: ColumnsType<RegisterMonatsbelegRow> = useMemo(
        () => [
            {
                title: 'Kasse',
                key: 'register',
                render: (_, record) => (
                    <Space direction="vertical" size={0}>
                        <Typography.Text strong>
                            {record.register.location?.trim() || '—'}
                        </Typography.Text>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            Nr. {record.register.registerNumber}
                        </Typography.Text>
                    </Space>
                ),
            },
            {
                title: 'Letzter Monatsbeleg',
                key: 'last',
                width: 220,
                render: (_, record) => {
                    if (record.statusLoading) return <Typography.Text type="secondary">…</Typography.Text>;
                    if (record.statusError) return <Typography.Text type="warning">—</Typography.Text>;
                    return <Typography.Text>{formatLastCompletedLabel(record.status?.lastCompletedMonth)}</Typography.Text>;
                },
            },
            {
                title: 'Fehlende Monate',
                key: 'missing-months',
                width: 360,
                render: (_, record) => {
                    if (record.statusLoading) return <Tag color="default">Laden…</Tag>;
                    if (record.statusError) return <Tag color="default">Status fehlgeschlagen</Tag>;
                    return (
                        <Typography.Text>
                            {formatMissingMonthsLabel(record.status?.missingMonths ?? undefined)}
                        </Typography.Text>
                    );
                },
            },
            {
                title: 'Fortschritt',
                key: 'progress',
                width: 260,
                render: (_, record) => {
                    if (record.statusLoading) return <Typography.Text type="secondary">…</Typography.Text>;
                    if (record.statusError) return <Typography.Text type="warning">—</Typography.Text>;

                    const progress = getYearlyProgress(record.status?.missingMonths ?? undefined);
                    return (
                        <Space direction="vertical" size={4} style={{ width: '100%' }}>
                            <Progress percent={progress.percent} size="small" />
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                {progress.completed} von {progress.total} Monaten im aktuellen Jahr abgeschlossen
                            </Typography.Text>
                        </Space>
                    );
                },
            },
            {
                title: 'Aktion',
                key: 'action',
                width: 320,
                render: (_, record) => {
                    if (!canMonatsbeleg) {
                        return <Typography.Text type="secondary">Keine Berechtigung</Typography.Text>;
                    }

                    const nextMissingMonthLabel = getNextMissingMonthLabel(record.status?.nextRequiredMonth);
                    return (
                        <Space direction="vertical" size={8}>
                            <Link href={`/rksv/sonderbelege?registerId=${encodeURIComponent(record.registerId)}`}>
                                <Button type="primary" size="small">
                                    Monatsbeleg für {nextMissingMonthLabel} erstellen
                                </Button>
                            </Link>
                            {demoAutoSuggestEnabled ? (
                                <Button
                                    size="small"
                                    onClick={() => void createMissingMonatsbelegeForTest(record)}
                                    disabled={!record.status?.missingMonths?.length}
                                >
                                    Alle fehlenden Monate als Test erstellen
                                </Button>
                            ) : null}
                        </Space>
                    );
                },
            },
        ],
        [canMonatsbeleg, createMissingMonatsbelegeForTest, demoAutoSuggestEnabled],
    );

    return (
        <Card title="Monatsbeleg (RKSV)" bordered={false} style={{ marginBottom: 24 }}>
            {demoAutoSuggestEnabled ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 12 }}
                    message="Demo-Modus: Sie können Monatsbelege zu Testzwecken jederzeit erstellen. Im Produktivbetrieb sind sie gesetzlich vorgeschrieben."
                />
            ) : null}
            <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                Übersicht aller Kassen: Monatsbeleg-Frist nach Kalendermonat (Europe/Vienna). Aktualisierung alle 5 Minuten
                und bei Fenster-Fokus.
            </Typography.Paragraph>
            <Table<RegisterMonatsbelegRow>
                rowKey={(r) => r.registerId}
                loading={loading}
                pagination={false}
                columns={columns}
                dataSource={rows}
                locale={{ emptyText: 'Keine Kassen gefunden.' }}
            />
        </Card>
    );
}
