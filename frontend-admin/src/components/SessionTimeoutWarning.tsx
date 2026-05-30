'use client';

import { useEffect, useMemo, useState } from 'react';
import { Modal, Button, Space, Progress, Typography, Alert, theme } from 'antd';
import { WarningOutlined, LoginOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n';

const { Text, Title } = Typography;

export type SessionTimeoutWarningProps = {
    open: boolean;
    secondsRemaining: number;
    /** Full warning window length (progress circle baseline). */
    warningTotalSeconds: number;
    onContinue: () => void;
    onLogout: () => void;
};

function formatRemainingTime(
    seconds: number,
    t: (key: string, options?: Record<string, string | number>) => string,
): string {
    const safe = Math.max(0, Math.floor(seconds));
    const mins = Math.floor(safe / 60);
    const secs = safe % 60;
    if (mins === 0) {
        return t('common.auth.sessionTimeout.timeSecondsOnly', { seconds: secs });
    }
    return t('common.auth.sessionTimeout.timeMinutesAndSeconds', { minutes: mins, seconds: secs });
}

export function SessionTimeoutWarning({
    open,
    secondsRemaining,
    warningTotalSeconds,
    onContinue,
    onLogout,
}: SessionTimeoutWarningProps) {
    const { t } = useI18n();
    const { token } = theme.useToken();

    const displaySeconds = Math.max(0, secondsRemaining);
    const totalSeconds = Math.max(1, warningTotalSeconds);

    const [progress, setProgress] = useState(100);

    useEffect(() => {
        if (open && displaySeconds > 0) {
            const percent = (displaySeconds / totalSeconds) * 100;
            setProgress(Math.max(0, Math.min(100, percent)));
        } else if (!open) {
            setProgress(100);
        }
    }, [open, displaySeconds, totalSeconds]);

    const formattedTime = useMemo(
        () => formatRemainingTime(displaySeconds, t),
        [displaySeconds, t],
    );

    const circleLabel = `${Math.floor(displaySeconds / 60)}:${(displaySeconds % 60).toString().padStart(2, '0')}`;

    return (
        <Modal
            title={
                <Space>
                    <WarningOutlined style={{ color: token.colorWarning }} />
                    <span>{t('common.auth.sessionTimeout.warningTitle')}</span>
                </Space>
            }
            open={open}
            closable={false}
            maskClosable={false}
            keyboard={false}
            footer={[
                <Button key="logout" danger icon={<LoginOutlined />} onClick={onLogout}>
                    {t('common.auth.sessionTimeout.logout')}
                </Button>,
                <Button key="continue" type="primary" onClick={onContinue}>
                    {t('common.auth.sessionTimeout.stayLoggedIn')}
                </Button>,
            ]}
            width={450}
        >
            <div style={{ textAlign: 'center', padding: '16px 0' }}>
                <Title level={4} style={{ marginBottom: 16 }}>
                    {t('common.auth.sessionTimeout.expiresIn', { time: formattedTime })}
                </Title>

                <Progress
                    type="circle"
                    percent={progress}
                    format={() => circleLabel}
                    strokeColor={{
                        '0%': token.colorPrimary,
                        '100%': token.colorSuccess,
                    }}
                    size={120}
                />

                <Alert
                    message={t('common.auth.sessionTimeout.alertMessage')}
                    description={t('common.auth.sessionTimeout.alertDescription')}
                    type="warning"
                    showIcon
                    style={{ marginTop: 16, textAlign: 'left' }}
                />

                <Text type="secondary" style={{ display: 'block', marginTop: 16, fontSize: 12 }}>
                    {t('common.auth.sessionTimeout.securityNote')}
                </Text>
            </div>
        </Modal>
    );
}
