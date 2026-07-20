'use client';

import { Typography } from 'antd';
import { CheckCircleOutlined, ExclamationCircleOutlined, LoadingOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n';

export type AutoSaveStatusProps = {
    saving?: boolean;
    saved?: boolean;
    error?: boolean;
    className?: string;
};

/**
 * Compact auto-save feedback for form headers / footers.
 */
export function AutoSaveStatusIndicator({
    saving = false,
    saved = false,
    error = false,
    className,
}: AutoSaveStatusProps) {
    const { t } = useI18n();

    if (!saving && !saved && !error) return null;

    const color = error ? 'danger' : saving ? 'secondary' : undefined;

    const icon = saving ? (
        <LoadingOutlined spin aria-hidden />
    ) : error ? (
        <ExclamationCircleOutlined aria-hidden style={{ color: 'var(--ant-color-error)' }} />
    ) : (
        <CheckCircleOutlined aria-hidden style={{ color: 'var(--ant-color-success)' }} />
    );

    const label = saving
        ? t('common.autoSave.saving')
        : error
          ? t('common.autoSave.error')
          : t('common.autoSave.saved');

    return (
        <Typography.Text
            type={color}
            className={className}
            style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 13 }}
            aria-live="polite"
        >
            {icon}
            {label}
        </Typography.Text>
    );
}
