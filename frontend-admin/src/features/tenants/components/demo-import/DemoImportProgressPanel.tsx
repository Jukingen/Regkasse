'use client';

import { Divider, Progress, Typography } from 'antd';
import {
    CheckCircleOutlined,
    ClockCircleOutlined,
    LoadingOutlined,
} from '@ant-design/icons';

import type { DemoImportProgress } from '@/features/tenants/api/demoImportJobs';
import {
    CATEGORY_GROUPS,
    expandDemoCategoryReferences,
} from '@/features/tenants/components/demo-import/categoryGroups';

const { Text } = Typography;

function resolveCategoryLabel(categoryName: string): string {
    const refs = new Set(expandDemoCategoryReferences(categoryName));
    for (const group of CATEGORY_GROUPS) {
        if (refs.has(group.name)) return group.displayName;
        for (const sub of group.subcategories ?? []) {
            if (refs.has(sub.name)) return sub.displayName;
        }
    }
    return categoryName;
}

function categoryStatusLine(row: DemoImportProgress['categories'][number]): string {
    const label = resolveCategoryLabel(row.categoryName);
    if (row.state === 'Completed') {
        return `✅ ${label} (${row.processed}/${row.total}) abgeschlossen`;
    }
    if (row.state === 'Processing') {
        return `⏳ ${label} (${row.processed}/${row.total}) wird verarbeitet`;
    }
    return `⏳ ${label} (0/${row.total}) wartet`;
}

export type DemoImportProgressPanelProps = {
    progress: DemoImportProgress | null;
};

export function DemoImportProgressPanel({ progress }: DemoImportProgressPanelProps) {
    if (!progress) {
        return (
            <div style={{ padding: '8px 0' }}>
                <Text type="secondary">Import wird vorbereitet…</Text>
            </div>
        );
    }

    const currentIndex = progress.processedProducts;
    const total = progress.totalProducts;
    const currentLabel =
        progress.currentProductName && total > 0
            ? `Aktuell: ${progress.currentProductName} (${Math.min(currentIndex + 1, total)}/${total})`
            : null;

    return (
        <div style={{ padding: '4px 0' }}>
            <Text strong>Importiere Demo-Produkte…</Text>
            <Progress
                percent={progress.percent}
                status={progress.status === 'Failed' ? 'exception' : 'active'}
                style={{ marginTop: 12, marginBottom: 12 }}
            />
            {currentLabel ? (
                <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
                    {currentLabel}
                </Text>
            ) : null}
            <div style={{ marginBottom: 4 }}>
                <Text>
                    <CheckCircleOutlined style={{ color: '#52c41a', marginRight: 6 }} />
                    {progress.importedCount} Produkte importiert
                </Text>
            </div>
            <div style={{ marginBottom: 12 }}>
                <Text>
                    <ClockCircleOutlined style={{ marginRight: 6 }} />
                    {progress.skippedCount} Produkte übersprungen
                </Text>
            </div>
            <Divider style={{ margin: '12px 0' }} />
            {progress.categories.map((row) => (
                <div key={row.categoryName} style={{ fontSize: 13, marginBottom: 4, color: '#595959' }}>
                    {row.state === 'Processing' ? (
                        <LoadingOutlined style={{ marginRight: 6 }} />
                    ) : null}
                    {categoryStatusLine(row)}
                </div>
            ))}
            {progress.message && progress.status === 'Failed' ? (
                <Text type="danger" style={{ display: 'block', marginTop: 12 }}>
                    {progress.message}
                </Text>
            ) : null}
        </div>
    );
}
