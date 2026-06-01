/**
 * Enterprise audit diff viewer modal.
 * Shows structured changes (e.g. "Rolle: Kellner → Demo").
 * Invariant 4: Only whitelisted fields rendered; sensitive data must never be shown.
 * Invariant 5: Gracefully handle incomplete historical records (null entry, missing/invalid changes JSON;
 * show "Keine Feldänderungen" and never throw).
 */
import React from 'react';
import { Modal, Table, Typography, Button } from 'antd';
import type { DiffRow } from '../utils/auditDiffUtils';
import { getDiffRowsFromEntry, EMPTY_PLACEHOLDER } from '../utils/auditDiffUtils';
import { usersCopy } from '../constants/copy';

const { Text } = Typography;

export type AuditEntryForDiff = {
    id?: string;
    changes?: string | null;
    oldValues?: string | null;
    newValues?: string | null;
    description?: string | null;
};

type Props = {
    open: boolean;
    onClose: () => void;
    entry: AuditEntryForDiff | null;
    getLabel: (key: string) => string;
    formatOptions?: { emptyPlaceholder?: string; labelActive?: string; labelInactive?: string };
};

export function AuditDiffViewerModal({ open, onClose, entry, getLabel, formatOptions }: Props) {
    const diffRows: DiffRow[] | null = entry
        ? getDiffRowsFromEntry(entry, getLabel, {
              emptyPlaceholder: formatOptions?.emptyPlaceholder ?? EMPTY_PLACEHOLDER,
              labelActive: formatOptions?.labelActive ?? usersCopy.statusActive,
              labelInactive: formatOptions?.labelInactive ?? usersCopy.statusInactive,
          })
        : null;

    const hasRows = Array.isArray(diffRows) && diffRows.length > 0;

    return (
        <Modal
            title={usersCopy.fieldChangesTitle}
            open={open}
            onCancel={onClose}
            footer={[
                <Button key="close" onClick={onClose}>
                    {usersCopy.close}
                </Button>,
            ]}
            destroyOnHidden
            width={560}
        >
            {hasRows ? (
                <Table
                    size="small"
                    dataSource={diffRows}
                    rowKey="field"
                    pagination={false}
                    columns={[
                        {
                            title: usersCopy.fieldLabel,
                            dataIndex: 'label',
                            key: 'label',
                            width: 160,
                            render: (val: string) => (val != null && String(val).trim() ? String(val).trim() : EMPTY_PLACEHOLDER),
                        },
                        {
                            title: usersCopy.oldValue,
                            dataIndex: 'oldVal',
                            key: 'oldVal',
                            render: (val: string) => (
                                <Text style={{ wordBreak: 'break-word', maxWidth: 200 }}>
                                    {val != null && String(val).trim() ? String(val).trim() : EMPTY_PLACEHOLDER}
                                </Text>
                            ),
                        },
                        {
                            title: '→',
                            key: 'arrow',
                            width: 32,
                            align: 'center',
                            render: () => <Text type="secondary">→</Text>,
                        },
                        {
                            title: usersCopy.newValue,
                            dataIndex: 'newVal',
                            key: 'newVal',
                            render: (val: string) => (
                                <Text style={{ wordBreak: 'break-word', maxWidth: 200 }}>
                                    {val != null && String(val).trim() ? String(val).trim() : EMPTY_PLACEHOLDER}
                                </Text>
                            ),
                        },
                    ]}
                />
            ) : (
                <Text type="secondary">{usersCopy.noFieldChanges}</Text>
            )}
        </Modal>
    );
}
