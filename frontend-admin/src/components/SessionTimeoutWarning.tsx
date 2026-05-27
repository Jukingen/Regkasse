'use client';

import { Button, Modal, Typography } from 'antd';
import { useI18n } from '@/i18n';
import { CountdownTimer } from '@/components/CountdownTimer';

type Props = {
    open: boolean;
    warningSeconds: number;
    onContinueSession: () => void;
    onCountdownComplete: () => void;
};

export function SessionTimeoutWarning({
    open,
    warningSeconds,
    onContinueSession,
    onCountdownComplete,
}: Props) {
    const { t } = useI18n();

    return (
        <Modal
            open={open}
            title={t('common.auth.sessionTimeout.warningTitle')}
            closable={false}
            maskClosable={false}
            footer={[
                <Button key="continue" type="primary" onClick={onContinueSession}>
                    {t('common.auth.sessionTimeout.stayLoggedIn')}
                </Button>,
            ]}
        >
            <Typography.Paragraph>
                {t('common.auth.sessionTimeout.warningBody', { seconds: String(warningSeconds) })}
            </Typography.Paragraph>
            <CountdownTimer
                active={open}
                seconds={warningSeconds}
                onComplete={onCountdownComplete}
            />
        </Modal>
    );
}
