'use client';

import React from 'react';
import { Alert, Button, Modal, Table } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import type { BulkImportJobStatusResponse } from '@/features/users/api/bulkImport';
import { downloadBulkImportResults } from '@/features/users/api/bulkImport';

type Props = {
    open: boolean;
    status: BulkImportJobStatusResponse | null;
    onClose: () => void;
};

/** Final import outcome with error table and results CSV download. */
export function BulkImportResultsModal({ open, status, onClose }: Props) {
    const result = status?.result;
    const errors = result?.errors ?? status?.errors ?? [];
    const successCount = result?.successCount ?? status?.successCount ?? 0;
    const failedCount = result?.failedCount ?? status?.failedCount ?? 0;
    const totalRows = result?.totalRows ?? status?.totalRows ?? 0;
    const downloadUrl = result?.downloadUrl;

    const alertType =
        status?.status === 'Cancelled'
            ? 'warning'
            : failedCount === 0
              ? 'success'
              : successCount > 0
                ? 'warning'
                : 'error';

    return (
        <Modal
            title="Import-Ergebnis"
            open={open}
            onCancel={onClose}
            width={720}
            footer={[
                downloadUrl ? (
                    <Button
                        key="download"
                        icon={<DownloadOutlined />}
                        onClick={() => void downloadBulkImportResults(downloadUrl)}
                    >
                        Ergebnisbericht (CSV)
                    </Button>
                ) : null,
                <Button key="close" type="primary" onClick={onClose}>
                    Schließen
                </Button>,
            ]}
        >
            {status ? (
                <>
                    <Alert
                        type={alertType}
                        showIcon
                        style={{ marginBottom: 16 }}
                        message={
                            status.status === 'Cancelled'
                                ? 'Import abgebrochen'
                                : `${successCount} erfolgreich, ${failedCount} fehlgeschlagen (von ${totalRows} Zeilen)`
                        }
                        description={status.message ?? undefined}
                    />
                    {errors.length > 0 ? (
                        <Table
                            size="small"
                            pagination={{ pageSize: 10 }}
                            rowKey={(r) => `${r.row}-${r.email ?? ''}`}
                            dataSource={errors}
                            columns={[
                                { title: 'Zeile', dataIndex: 'row', width: 72 },
                                { title: 'E-Mail', dataIndex: 'email', ellipsis: true },
                                { title: 'Fehler', dataIndex: 'error' },
                            ]}
                        />
                    ) : null}
                </>
            ) : null}
        </Modal>
    );
}
