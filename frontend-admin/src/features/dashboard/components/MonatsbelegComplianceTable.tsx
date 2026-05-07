'use client';

import React, { useMemo } from 'react';
import Link from 'next/link';
import { Button, Card, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import type { RegisterMonatsbelegRow } from '@/features/dashboard/hooks/useAdminMonatsbelegOverview';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

function formatLastMonatsbeleg(iso: string | null | undefined): string {
    if (!iso?.trim()) return 'Keiner';
    const d = dayjs(iso);
    return d.isValid() ? d.format('DD.MM.YYYY') : '—';
}

function statusLabel(level: string | undefined): { text: string; color: string } {
    const l = level?.toLowerCase() ?? '';
    if (l === 'red') return { text: '🔴 ÜBERFÄLLIG', color: 'red' };
    if (l === 'yellow') return { text: '⚠️ Bald fällig', color: 'orange' };
    return { text: '✅ Aktuell', color: 'green' };
}

export type MonatsbelegComplianceTableProps = {
    rows: RegisterMonatsbelegRow[];
    loading: boolean;
};

export function MonatsbelegComplianceTable({ rows, loading }: MonatsbelegComplianceTableProps) {
    const { hasPermission } = usePermissions();
    const canMonatsbeleg = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);

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
                width: 160,
                render: (_, record) => {
                    if (record.statusLoading) return <Typography.Text type="secondary">…</Typography.Text>;
                    if (record.statusError) return <Typography.Text type="warning">—</Typography.Text>;
                    return formatLastMonatsbeleg(record.status?.lastMonatsbelegDate ?? null);
                },
            },
            {
                title: 'Status',
                key: 'status',
                width: 200,
                render: (_, record) => {
                    if (record.statusLoading) {
                        return <Tag color="default">Laden…</Tag>;
                    }
                    if (record.statusError) {
                        return <Tag color="default">Status fehlgeschlagen</Tag>;
                    }
                    const { text, color } = statusLabel(record.status?.warningLevel);
                    return <Tag color={color}>{text}</Tag>;
                },
            },
            {
                title: 'Aktion',
                key: 'action',
                width: 200,
                render: (_, record) =>
                    canMonatsbeleg ? (
                        <Link href={`/rksv/sonderbelege?registerId=${encodeURIComponent(record.registerId)}`}>
                            <Button type="primary" size="small">
                                Monatsbeleg erstellen
                            </Button>
                        </Link>
                    ) : (
                        <Typography.Text type="secondary">Keine Berechtigung</Typography.Text>
                    ),
            },
        ],
        [canMonatsbeleg],
    );

    return (
        <Card title="Monatsbeleg (RKSV)" bordered={false} style={{ marginBottom: 24 }}>
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
