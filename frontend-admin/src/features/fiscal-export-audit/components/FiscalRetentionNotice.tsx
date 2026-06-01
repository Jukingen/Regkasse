'use client';

import { Alert } from 'antd';
import { useI18n } from '@/i18n';

/**
 * Operator-facing compliance note: fiscal audit data retention under Austrian practice.
 * UI copy is German-first via i18n (de default catalog).
 */
export function FiscalRetentionNotice() {
    const { t } = useI18n();
    return (
        <Alert
            type="info"
            showIcon
            title={t('fiscalExportAudit.retention.title')}
            description={t('fiscalExportAudit.retention.body')}
            style={{ marginBottom: 16 }}
        />
    );
}
