'use client';

import { Alert } from 'antd';
import { formatDateTime } from '@/i18n/formatting';

export type StartbelegStatusProps = {
    /** True when a Startbeleg exists for the selected cash register. */
    exists: boolean;
    /** UTC ISO timestamp when the Startbeleg was created (optional). */
    createdAtUtc?: string | null;
    loading?: boolean;
};

/**
 * Compact Startbeleg presence status for RKSV Sonderbelege.
 */
export function StartbelegStatus({ exists, createdAtUtc, loading = false }: StartbelegStatusProps) {
    if (loading) {
        return <Alert type="info" showIcon title="Status wird geladen…" />;
    }

    if (exists) {
        const dateLabel = createdAtUtc?.trim() ? formatDateTime(createdAtUtc) : null;
        return (
            <Alert
                type="success"
                showIcon
                title={dateLabel ? `Bereits erstellt am ${dateLabel}` : 'Bereits erstellt'}
            />
        );
    }

    return <Alert type="warning" showIcon title="Noch nicht erstellt" />;
}
