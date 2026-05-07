'use client';

/**
 * Mandatory RKSV § 8 acknowledgment before fiscal/DEP-style export actions.
 */

import React, { useEffect, useState } from 'react';
import { Button, Checkbox, Modal, Space, Typography } from 'antd';
import { useI18n } from '@/i18n/I18nProvider';

export type FiscalExportDisclaimerModalProps = {
    open: boolean;
    onCancel: () => void;
    /** Called when user confirms; skip24h reflects «don't show again for 24h». */
    onConfirm: (opts: { skip24h: boolean }) => void;
};

export function FiscalExportDisclaimerModal({ open, onCancel, onConfirm }: FiscalExportDisclaimerModalProps) {
    const { t } = useI18n();
    const [understood, setUnderstood] = useState(false);
    const [skip24h, setSkip24h] = useState(false);

    useEffect(() => {
        if (open) {
            setUnderstood(false);
            setSkip24h(false);
        }
    }, [open]);

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
                    disabled={!understood}
                    onClick={() => onConfirm({ skip24h })}
                >
                    {t('rksvHub.fiscalExportPage.disclaimerModalProceed')}
                </Button>,
            ]}
            width={560}
            destroyOnClose
        >
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Typography.Paragraph style={{ marginBottom: 0, whiteSpace: 'pre-line' }}>
                    {t('rksvHub.fiscalExportPage.disclaimerModalBody')}
                </Typography.Paragraph>
                <Checkbox checked={understood} onChange={(e) => setUnderstood(e.target.checked)}>
                    {t('rksvHub.fiscalExportPage.disclaimerModalUnderstandCheckbox')}
                </Checkbox>
                <Checkbox checked={skip24h} onChange={(e) => setSkip24h(e.target.checked)}>
                    {t('rksvHub.fiscalExportPage.disclaimerModalSkip24hCheckbox')}
                </Checkbox>
            </Space>
        </Modal>
    );
}
