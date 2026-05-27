'use client';

import React from 'react';
import { Table, Typography } from 'antd';
import type { BulkImportPreviewRow } from '@/features/users/api/bulkImport';

type Props = {
    rows: BulkImportPreviewRow[];
    totalRows: number;
    loading?: boolean;
};

/** First N rows of a bulk import file before starting the job. */
export function ImportPreviewTable({ rows, totalRows, loading }: Props) {
    return (
        <>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
                Vorschau ({rows.length} von {totalRows} Zeilen)
            </Typography.Text>
            <Table
                size="small"
                loading={loading}
                pagination={false}
                rowKey={(r) => String(r.row)}
                dataSource={rows}
                scroll={{ x: 640 }}
                columns={[
                    { title: 'Zeile', dataIndex: 'row', width: 64 },
                    { title: 'E-Mail', dataIndex: 'email', ellipsis: true },
                    { title: 'Benutzername', dataIndex: 'username', ellipsis: true },
                    { title: 'Vorname', dataIndex: 'firstName', width: 100 },
                    { title: 'Nachname', dataIndex: 'lastName', width: 100 },
                    { title: 'Rolle', dataIndex: 'role', width: 100 },
                    { title: 'Mandant', dataIndex: 'tenantSlug', width: 100 },
                ]}
            />
        </>
    );
}
