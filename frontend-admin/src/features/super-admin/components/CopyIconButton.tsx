'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useCallback } from 'react';
import { Button } from 'antd';
import { CopyOutlined } from '@ant-design/icons';

import { useI18n } from '@/i18n';
import { copyTextToClipboard } from '@/lib/clipboard';

export type CopyIconButtonProps = {
    text: string;
    ariaLabel: string;
    onCopied?: () => void;
};

export function CopyIconButton({ text, ariaLabel, onCopied }: CopyIconButtonProps) {
  const { message } = useAntdApp();

    const { t } = useI18n();

    const copy = useCallback(async () => {
        const copied = await copyTextToClipboard(text);
        if (copied) {
            message.success(t('tenants.provisioning.copySuccess'));
            onCopied?.();
        } else {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    }, [text, t, onCopied]);

    return (
        <Button type="text" size="small" icon={<CopyOutlined />} onClick={() => void copy()} aria-label={ariaLabel} />
    );
}
