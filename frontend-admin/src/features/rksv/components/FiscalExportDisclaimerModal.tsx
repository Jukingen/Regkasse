'use client';

/**
 * Mandatory RKSV § 8 acknowledgment before fiscal/DEP-style export actions.
 * Shows server disclaimer text (GET /api/admin/fiscal-export/disclaimer); export calls must send X-Disclaimer-Acknowledged: true.
 */

import React, { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Checkbox, Modal, Space, Spin, Typography } from 'antd';
import { getApiAdminFiscalExportDisclaimer } from '@/api/generated/admin/admin';
import { useI18n } from '@/i18n/I18nProvider';

export type FiscalExportDisclaimerModalProps = {
    open: boolean;
    onCancel: () => void;
    /** Called when user confirms; skip24h reflects «don't show again for 24h». */
    onConfirm: (opts: { skip24h: boolean }) => void;
};

export function FiscalExportDisclaimerModal({ open, onCancel, onConfirm }: FiscalExportDisclaimerModalProps) {
    const { t, textLocale } = useI18n();
    const [understood, setUnderstood] = useState(false);
    const [skip24h, setSkip24h] = useState(false);

    const disclaimerQuery = useQuery({
        queryKey: ['admin', 'fiscal-export-disclaimer'],
        queryFn: () => getApiAdminFiscalExportDisclaimer(),
        enabled: open,
        staleTime: 300_000,
    });

    useEffect(() => {
        if (open) {
            setUnderstood(false);
            setSkip24h(false);
        }
    }, [open]);

    const serverNotice =
        disclaimerQuery.data === undefined
            ? ''
            : textLocale === 'en'
              ? (disclaimerQuery.data.en ?? '')
              : (disclaimerQuery.data.de ?? '');

    const fallbackBody = t('rksvHub.fiscalExportPage.disclaimerModalBody');
    const alertDescription =
        disclaimerQuery.isSuccess && serverNotice.trim().length > 0 ? serverNotice : fallbackBody;

    return (
        <Modal
            open={open}
            title={t('rksvHub.fiscalExportPage.disclaimerModalTitle')}
            onCancel={onCancel}
            footer={[
                <Button key="cancel" onClick={onCancel}>
                    {t('rksvHub.fiscalExportPage.disclaimerModalCancel')}
                </Button>,
                <Button
                    key="go"
                    type="primary"
                    danger
                    disabled={!understood || disclaimerQuery.isLoading}
                    onClick={() => onConfirm({ skip24h })}
                >
                    {t('rksvHub.fiscalExportPage.disclaimerModalProceed')}
                </Button>,
            ]}
            width={560}
            destroyOnClose
        >
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                {disclaimerQuery.isLoading ? (
                    <div style={{ display: 'flex', justifyContent: 'center', padding: 24 }}>
                        <Spin />
                    </div>
                ) : (
                    <Alert
                        type="warning"
                        showIcon
                        message={
                            <Typography.Paragraph style={{ marginBottom: 0, whiteSpace: 'pre-line' }}>
                                {alertDescription}
                            </Typography.Paragraph>
                        }
                    />
                )}
                <Checkbox
                    disabled={disclaimerQuery.isLoading}
                    checked={understood}
                    onChange={(e) => setUnderstood(e.target.checked)}
                >
                    {t('rksvHub.fiscalExportPage.disclaimerModalUnderstandCheckbox')}
                </Checkbox>
                <Checkbox
                    disabled={disclaimerQuery.isLoading}
                    checked={skip24h}
                    onChange={(e) => setSkip24h(e.target.checked)}
                >
                    {t('rksvHub.fiscalExportPage.disclaimerModalSkip24hCheckbox')}
                </Checkbox>
            </Space>
        </Modal>
    );
}
