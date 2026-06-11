'use client';

import { useMemo, useState } from 'react';
import { Button, Modal, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { CashRegister } from '@/api/generated/model';
import { CreateMonatsbelegModal } from '@/features/rksv/components/CreateMonatsbelegModal';
import type { PastMissingMonatsbelegEntry } from '@/features/rksv/utils/monatsbelegMissingMonths';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';

export type PastMonthsMonatsbelegModalProps = {
    open: boolean;
    entries: PastMissingMonatsbelegEntry[];
    onClose: () => void;
    onCreated: () => void;
};

function normalizeRegisterRows(data: unknown): CashRegister[] {
    if (Array.isArray(data)) return data as CashRegister[];
    if (data && typeof data === 'object' && 'registers' in data) {
        const registers = (data as { registers?: CashRegister[] }).registers;
        if (Array.isArray(registers)) return registers;
    }
    return [];
}

function formatMonthYearDe(year: number, month: number): string {
    return new Intl.DateTimeFormat('de-DE', {
        month: 'long',
        year: 'numeric',
        timeZone: 'Europe/Vienna',
    }).format(new Date(Date.UTC(year, month - 1, 1)));
}

function registerLabel(register: CashRegister | undefined, registerId: string): string {
    if (!register) return registerId.slice(0, 8);
    const nr = formatRegisterDisplayLabel(register.registerNumber);
    const loc = register.location?.trim();
    if (loc && nr) return `${loc} (Nr. ${nr})`;
    if (nr) return `Nr. ${nr}`;
    return registerId.slice(0, 8);
}

type CreateTarget = {
    cashRegisterId: string;
    year: number;
    month: number;
};

export function PastMonthsMonatsbelegModal({
    open,
    entries,
    onClose,
    onCreated,
}: PastMonthsMonatsbelegModalProps) {
    const { data: registersRaw } = useGetApiCashRegister({ query: { enabled: open } });
    const registers = useMemo(() => normalizeRegisterRows(registersRaw), [registersRaw]);
    const registerById = useMemo(() => {
        const map = new Map<string, CashRegister>();
        for (const register of registers) {
            const id = register.id?.trim();
            if (id) map.set(id, register);
        }
        return map;
    }, [registers]);

    const [createTarget, setCreateTarget] = useState<CreateTarget | null>(null);

    const columns: ColumnsType<PastMissingMonatsbelegEntry> = useMemo(
        () => [
            {
                title: 'Kasse',
                key: 'register',
                render: (_, row) => registerLabel(registerById.get(row.cashRegisterId), row.cashRegisterId),
            },
            {
                title: 'Periode',
                key: 'period',
                render: (_, row) => formatMonthYearDe(row.year, row.month),
            },
            {
                title: 'Status',
                key: 'status',
                width: 140,
                render: (_, row) =>
                    row.isOverdue ? <Tag color="red">Überfällig</Tag> : <Tag color="orange">Fehlt</Tag>,
            },
            {
                title: 'Aktion',
                key: 'action',
                width: 140,
                render: (_, row) => (
                    <Button
                        size="small"
                        type="primary"
                        onClick={() =>
                            setCreateTarget({
                                cashRegisterId: row.cashRegisterId,
                                year: row.year,
                                month: row.month,
                            })
                        }
                    >
                        Erstellen
                    </Button>
                ),
            },
        ],
        [registerById],
    );

    return (
        <>
            <Modal
                title="Frühere Monatsbelege"
                open={open}
                onCancel={onClose}
                footer={[
                    <Button key="close" onClick={onClose}>
                        Schließen
                    </Button>,
                ]}
                width={760}
                destroyOnHidden
            >
                <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                        Fehlende Monatsbelege aus vergangenen Kalendermonaten können mit Bestätigung erstellt werden.
                        FinanzOnline akzeptiert Nachreichungen in der Regel, bei längeren Lücken kann eine Prüfung
                        erfolgen.
                    </Typography.Paragraph>
                    <Table<PastMissingMonatsbelegEntry>
                        rowKey={(row) => `${row.cashRegisterId}-${row.yearMonth}`}
                        dataSource={entries}
                        columns={columns}
                        pagination={entries.length > 8 ? { pageSize: 8 } : false}
                        size="small"
                        locale={{ emptyText: 'Keine fehlenden Monatsbelege aus früheren Monaten.' }}
                    />
                </Space>
            </Modal>

            {createTarget ? (
                <CreateMonatsbelegModal
                    open
                    year={createTarget.year}
                    month={createTarget.month}
                    cashRegisterId={createTarget.cashRegisterId}
                    reason={`Nachholung Monatsbeleg ${createTarget.year}-${String(createTarget.month).padStart(2, '0')}`}
                    onClose={() => setCreateTarget(null)}
                    onSuccess={() => {
                        setCreateTarget(null);
                        onCreated();
                    }}
                />
            ) : null}
        </>
    );
}
