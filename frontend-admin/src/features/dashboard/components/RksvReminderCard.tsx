'use client';

import { Alert, Button, Card, Empty, Skeleton, Space, Tag } from 'antd';
import {
    CheckCircleOutlined,
    ReloadOutlined,
    WarningOutlined,
} from '@ant-design/icons';
import { useRksvReminderOverview } from '@/features/rksv-operations/hooks/useRksvReminderOverview';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';

export function RksvReminderCard() {
    const { data, isLoading, error, refetch, isError, isFetching } = useRksvReminderOverview();
    const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);

    if (isLoading) {
        return (
            <Card
                title="RKSV Sonderbelege (Erinnerungen)"
                extra={<Button icon={<ReloadOutlined />} onClick={() => void refetch()} />}
                variant="borderless"
                style={{ marginBottom: 24 }}
            >
                <Space orientation="vertical" style={{ width: '100%' }}>
                    <Skeleton active paragraph={{ rows: 3 }} />
                </Space>
            </Card>
        );
    }

    const hasLoadError = isError || Boolean(error);
    if (hasLoadError) {
        return (
            <Card
                title="RKSV Sonderbelege (Erinnerungen)"
                extra={
                    <Button icon={<ReloadOutlined />} loading={isFetching} onClick={() => void refetch()}>
                        Erneut laden
                    </Button>
                }
                variant="borderless"
                style={{ marginBottom: 24 }}
            >
                <Alert
                    type="error"
                    title="Fehler beim Laden der RKSV-Daten"
                    description="Die RKSV-Erinnerungen konnten nicht geladen werden. Bitte versuchen Sie es später erneut."
                    showIcon
                />
            </Card>
        );
    }

    if (!data || data.totalRegisters === 0) {
        return (
            <Card
                title="RKSV Sonderbelege (Erinnerungen)"
                extra={<Button icon={<ReloadOutlined />} loading={isFetching} onClick={() => void refetch()} />}
                variant="borderless"
                style={{ marginBottom: 24 }}
            >
                <Empty
                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                    description="Keine Kassen vorhanden oder keine RKSV-Daten verfügbar"
                />
            </Card>
        );
    }

    const hasIssues =
        data.missingStartbeleg > 0 ||
        data.missingMonatsbeleg > 0 ||
        data.overdueMonatsbeleg > 0 ||
        data.missingJahresbeleg > 0;

    return (
        <Card
            title={
                <Space>
                    <span>RKSV Sonderbelege (Erinnerungen)</span>
                    {hasIssues ? (
                        <Tag color="red" icon={<WarningOutlined />}>
                            Aktion erforderlich
                        </Tag>
                    ) : (
                        <Tag color="green" icon={<CheckCircleOutlined />}>
                            Alles in Ordnung
                        </Tag>
                    )}
                </Space>
            }
            extra={
                <Space>
                    <Button size="small" icon={<ReloadOutlined />} loading={isFetching} onClick={() => void refetch()} />
                    {canOpenSonderbelege ? (
                        <Button size="small" type="link" href={RKSV_SONDERBELEGE_PATH}>
                            Verwalten
                        </Button>
                    ) : null}
                </Space>
            }
            variant="borderless"
            style={{ marginBottom: 24 }}
        >
            <Space orientation="vertical" style={{ width: '100%' }} size="middle">
                {data.missingStartbeleg > 0 ? (
                    <Alert
                        type="warning"
                        showIcon
                        title="Startbeleg fehlt"
                        description={`${data.missingStartbeleg} von ${data.totalRegisters} Kassen haben noch keinen Startbeleg.`}
                    />
                ) : null}

                {data.overdueMonatsbeleg > 0 ? (
                    <Alert
                        type="error"
                        showIcon
                        title="Monatsbeleg überfällig"
                        description={`${data.overdueMonatsbeleg} Kassen haben den Monatsbeleg nicht rechtzeitig erstellt.`}
                    />
                ) : null}

                {data.missingMonatsbeleg > 0 && data.overdueMonatsbeleg === 0 ? (
                    <Alert
                        type="info"
                        showIcon
                        title="Monatsbeleg ausstehend"
                        description={`${data.missingMonatsbeleg} Kassen benötigen einen Monatsbeleg.`}
                    />
                ) : null}

                {data.missingJahresbeleg > 0 ? (
                    <Alert
                        type="warning"
                        showIcon
                        title="Jahresbeleg ausstehend"
                        description={`${data.missingJahresbeleg} Kassen benötigen einen Jahresbeleg.`}
                    />
                ) : null}

                {!hasIssues ? (
                    <Alert
                        type="success"
                        showIcon
                        title="Alle RKSV-Sonderbelege sind aktuell"
                        description="Alle Kassen haben die erforderlichen Startbelege, Monatsbelege und Jahresbelege."
                    />
                ) : null}

                <div style={{ fontSize: 12, color: '#8c8c8c', textAlign: 'right' }}>
                    Letzte Aktualisierung: {data.lastUpdated ? new Date(data.lastUpdated).toLocaleString('de-DE') : 'unbekannt'}
                </div>
            </Space>
        </Card>
    );
}
