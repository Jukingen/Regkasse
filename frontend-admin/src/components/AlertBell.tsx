'use client';

import { useMemo, useState } from 'react';
import {
    Badge,
    Button,
    Empty,
    List,
    Popover,
    Space,
    Tag,
    Typography,
} from 'antd';
import {
    AlertOutlined,
    CheckCircleOutlined,
    CloseCircleOutlined,
    WarningOutlined,
} from '@ant-design/icons';

import styles from '@/components/alertBell.module.css';
import {
    useMarkAlertAsRead,
    useSuspiciousAlerts,
    useSuspiciousAlertsAccess,
} from '@/features/alerts/hooks/useAlerts';
import type { SuspiciousAlertSeverity } from '@/features/alerts/types';
import { useI18n } from '@/i18n/I18nProvider';
import { getAdminHeaderPopupContainer } from '@/shared/layout/adminHeaderDropdown';

const { Text } = Typography;

function getSeverityIcon(severity: SuspiciousAlertSeverity) {
    switch (severity) {
        case 'Critical':
            return <CloseCircleOutlined className={styles.severityCritical} aria-hidden />;
        case 'High':
            return <CloseCircleOutlined className={styles.severityHigh} aria-hidden />;
        case 'Medium':
            return <WarningOutlined className={styles.severityMedium} aria-hidden />;
        default:
            return <CheckCircleOutlined className={styles.severityLow} aria-hidden />;
    }
}

function getSeverityTagColor(severity: SuspiciousAlertSeverity): string {
    switch (severity) {
        case 'Critical':
            return 'magenta';
        case 'High':
            return 'red';
        case 'Medium':
            return 'orange';
        default:
            return 'gold';
    }
}

/** Header bell for suspicious payment / security alerts. */
export function AlertBell() {
    const { t, formatLocale } = useI18n();
    const canSee = useSuspiciousAlertsAccess();
    const [open, setOpen] = useState(false);

    const { data: alerts, refetch, isFetching } = useSuspiciousAlerts({
        unreadOnly: true,
        enabled: canSee,
    });
    const markAsRead = useMarkAlertAsRead();

    const unreadCount = useMemo(
        () => alerts?.filter((alert) => !alert.isRead).length ?? 0,
        [alerts],
    );

    if (!canSee) {
        return null;
    }

    const handleMarkAsRead = async (alertId: string) => {
        await markAsRead.mutateAsync(alertId);
        void refetch();
    };

    const formatTimestamp = (iso: string) => {
        try {
            return new Date(iso).toLocaleString(formatLocale);
        } catch {
            return iso;
        }
    };

    const content = (
        <div className={styles.panel}>
            <div className={styles.header}>
                <Text strong>{t('suspiciousAlerts.title')}</Text>
                <Button type="link" size="small" loading={isFetching} onClick={() => void refetch()}>
                    {t('suspiciousAlerts.refresh')}
                </Button>
            </div>

            {!alerts?.length ? (
                <Empty description={t('suspiciousAlerts.empty')} className="p-4" />
            ) : (
                <List
                    dataSource={alerts}
                    renderItem={(alert) => (
                        <List.Item
                            className={!alert.isRead ? styles.unreadItem : styles.readItem}
                            onClick={() => void handleMarkAsRead(alert.id)}
                            actions={[
                                <Button key="read" type="link" size="small">
                                    {t('suspiciousAlerts.markRead')}
                                </Button>,
                            ]}
                        >
                            <List.Item.Meta
                                avatar={getSeverityIcon(alert.severity)}
                                title={
                                    <Space wrap size={[4, 4]}>
                                        <span>{alert.message}</span>
                                        <Tag color={getSeverityTagColor(alert.severity)}>
                                            {t(`suspiciousAlerts.severity.${alert.severity}`)}
                                        </Tag>
                                    </Space>
                                }
                                description={
                                    <div>
                                        <div className={styles.metaTime}>
                                            {formatTimestamp(alert.detectedAtUtc || alert.createdAtUtc)}
                                        </div>
                                        {alert.suggestedAction ? (
                                            <div className={styles.metaAction}>{alert.suggestedAction}</div>
                                        ) : null}
                                    </div>
                                }
                            />
                        </List.Item>
                    )}
                />
            )}
        </div>
    );

    return (
        <Popover
            content={content}
            trigger="click"
            open={open}
            onOpenChange={setOpen}
            placement="bottomRight"
            classNames={{ root: "admin-header-dropdown" }}
            getPopupContainer={getAdminHeaderPopupContainer}
        >
            <Badge count={unreadCount} offset={[-2, 2]} size="small">
                <Button
                    type="text"
                    aria-label={t('suspiciousAlerts.bellAria')}
                    icon={<AlertOutlined style={{ fontSize: 18 }} />}
                />
            </Badge>
        </Popover>
    );
}
