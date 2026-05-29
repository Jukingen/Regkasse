'use client';

import { useRouter } from 'next/navigation';
import { Button, Modal, Space, Typography, Divider, List, Tooltip } from 'antd';
import {
    CheckCircleOutlined,
    BarChartOutlined,
    FolderOutlined,
    EuroOutlined,
} from '@ant-design/icons';

import type { DemoProductImportResult } from '@/api/admin/products';
import { buildPosAppOpenUrl } from '@/lib/posAppUrl';
import {
    formatAverageImportedPrice,
    resolveCategoriesCreated,
    resolveImportedProductCount,
} from '@/features/tenants/components/demo-import/demoImportSummary';

const { Text, Title } = Typography;

const NEXT_STEPS: Array<{ text: string; href?: string }> = [
    { text: 'Preise überprüfen und anpassen', href: '/products' },
    { text: 'Produktbilder hinzufügen', href: '/products' },
    { text: 'TSE-konforme Monatsbeleg erstellen', href: '/rksv/sonderbelege?focus=monatsbeleg' },
    { text: 'Erste Bestellung aufgeben' },
];

export type DemoImportSummaryModalProps = {
    open: boolean;
    result: DemoProductImportResult;
    tenantSlug?: string | null;
    onClose: () => void;
    onDone: () => void;
};

export function DemoImportSummaryModal({
    open,
    result,
    tenantSlug,
    onClose,
    onDone,
}: DemoImportSummaryModalProps) {
    const router = useRouter();
    const importedCount = resolveImportedProductCount(result);
    const categoriesCreated = resolveCategoriesCreated(result);
    const averagePrice = formatAverageImportedPrice(result);
    const posUrl = buildPosAppOpenUrl(tenantSlug);

    const navigate = (href: string) => {
        onDone();
        router.push(href);
    };

    const openPos = () => {
        if (!posUrl) return;
        window.open(posUrl, '_blank', 'noopener,noreferrer');
        onDone();
    };

    return (
        <Modal
            open={open}
            title={null}
            closable
            onCancel={() => {
                onClose();
                onDone();
            }}
            width={520}
            footer={
                <Space wrap style={{ width: '100%', justifyContent: 'flex-end' }}>
                    <Button type="primary" onClick={() => navigate('/products')}>
                        Preise bearbeiten
                    </Button>
                    <Button onClick={() => navigate('/dashboard')}>Dashboard</Button>
                    <Tooltip
                        title={
                            posUrl
                                ? 'POS-App in neuem Tab öffnen'
                                : 'POS-URL nicht konfiguriert (NEXT_PUBLIC_POS_APP_URL). App auf dem Kassengerät öffnen.'
                        }
                    >
                        <Button disabled={!posUrl} onClick={openPos}>
                            POS öffnen
                        </Button>
                    </Tooltip>
                </Space>
            }
        >
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Space align="start">
                    <CheckCircleOutlined style={{ color: '#52c41a', fontSize: 22, marginTop: 2 }} />
                    <Title level={4} style={{ margin: 0 }}>
                        Import abgeschlossen!
                    </Title>
                </Space>

                <Space direction="vertical" size={4} style={{ width: '100%' }}>
                    <Text>
                        <BarChartOutlined style={{ marginRight: 8 }} />
                        {importedCount} Produkte importiert
                    </Text>
                    <Text>
                        <FolderOutlined style={{ marginRight: 8 }} />
                        {categoriesCreated} Kategorien erstellt
                    </Text>
                    {averagePrice ? (
                        <Text>
                            <EuroOutlined style={{ marginRight: 8 }} />
                            Durchschnittspreis: {averagePrice}
                        </Text>
                    ) : null}
                </Space>

                <Divider style={{ margin: '8px 0' }} />

                <div>
                    <Text strong>Nächste Schritte:</Text>
                    <List
                        size="small"
                        style={{ marginTop: 8 }}
                        dataSource={NEXT_STEPS}
                        renderItem={(item) => (
                            <List.Item style={{ padding: '4px 0', border: 'none' }}>
                                {item.href ? (
                                    <Button
                                        type="link"
                                        style={{ padding: 0, height: 'auto' }}
                                        onClick={() => navigate(item.href!)}
                                    >
                                        • {item.text}
                                    </Button>
                                ) : (
                                    <Text type="secondary">• {item.text}</Text>
                                )}
                            </List.Item>
                        )}
                    />
                </div>
            </Space>
        </Modal>
    );
}
