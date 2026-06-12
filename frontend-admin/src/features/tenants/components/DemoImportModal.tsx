'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useEffect, useRef, useState } from 'react';
import { Modal, Button, Alert, Space, Tabs } from 'antd';
import { ImportOutlined } from '@ant-design/icons';

import type { DemoProductImportResult } from '@/api/admin/products';
import { useQueryClient } from '@tanstack/react-query';
import { useDemoImportCatalog } from '@/features/products/api/productHooks';
import { DemoImportCatalogWizard } from '@/features/tenants/components/demo-import/DemoImportCatalogWizard';
import { DemoImportProgressPanel } from '@/features/tenants/components/demo-import/DemoImportProgressPanel';
import {
    runDemoImportWithProgress,
    type DemoImportProgress,
} from '@/features/tenants/api/demoImportJobs';
import { DemoTemplateImportTab } from '@/features/tenants/components/demo-import/DemoTemplateImportTab';
import {
    DEFAULT_DEMO_IMPORT_IMAGE_MODE,
    toImageModeRequest,
    type DemoImportImageMode,
} from '@/features/tenants/components/demo-import/demoImportImage';
import {
    DEFAULT_PRICE_ADJUSTMENT,
    toPriceAdjustmentRequest,
    type DemoImportPriceAdjustmentState,
} from '@/features/tenants/components/demo-import/priceAdjustment';
import { buildImportRequest } from '@/features/tenants/components/demo-import/utils';
import { CATEGORY_GROUPS, enrichGroupsWithCatalog } from '@/features/tenants/components/demo-import/categoryGroups';
import { DemoImportSummaryModal } from '@/features/tenants/components/demo-import/DemoImportSummaryModal';

export type DemoImportModalProps = {
    open: boolean;
    tenantId?: string;
    tenantName: string;
    tenantSlug?: string | null;
    onClose: () => void;
    onSuccess: () => void;
};

function extractImportErrorMessage(error: unknown): string {
    if (error && typeof error === 'object' && 'response' in error) {
        const data = (error as { response?: { data?: { message?: string; error?: string; errorMessage?: string } } })
            .response?.data;
        return data?.message ?? data?.error ?? data?.errorMessage ?? 'Fehler beim Importieren der Demo-Produkte';
    }
    return 'Fehler beim Importieren der Demo-Produkte';
}

export function DemoImportModal({ open, tenantId, tenantName, tenantSlug, onClose, onSuccess }: DemoImportModalProps) {
  const { message } = useAntdApp();

    const queryClient = useQueryClient();
    const [activeTab, setActiveTab] = useState<'wizard' | 'template'>('wizard');
    const [wizardKey, setWizardKey] = useState(0);
    const [overwrite, setOverwrite] = useState(false);
    const [priceAdjustment, setPriceAdjustment] =
        useState<DemoImportPriceAdjustmentState>(DEFAULT_PRICE_ADJUSTMENT);
    const [imageMode, setImageMode] = useState<DemoImportImageMode>(DEFAULT_DEMO_IMPORT_IMAGE_MODE);
    const [importing, setImporting] = useState(false);
    const [importProgress, setImportProgress] = useState<DemoImportProgress | null>(null);
    const [summaryResult, setSummaryResult] = useState<DemoProductImportResult | null>(null);
    const importAbortRef = useRef<AbortController | null>(null);

    const catalogQuery = useDemoImportCatalog(open);

    useEffect(() => {
        if (!open) return;
        setActiveTab('wizard');
        setWizardKey((k) => k + 1);
        setOverwrite(false);
        setPriceAdjustment(DEFAULT_PRICE_ADJUSTMENT);
        setImageMode(DEFAULT_DEMO_IMPORT_IMAGE_MODE);
        setImporting(false);
        setImportProgress(null);
        setSummaryResult(null);
        importAbortRef.current?.abort();
        importAbortRef.current = null;
    }, [open]);

    const showImportSuccess = (result: DemoProductImportResult) => {
        onClose();
        setSummaryResult(result);
    };

    const finishSummary = () => {
        setSummaryResult(null);
        onSuccess();
    };

    const handleWizardImport = async (draft: {
        selectedGroupNames: Set<string>;
        selectedProductIds: Set<string>;
        productOverrides: import('@/api/admin/products').DemoImportProductOverride[];
    }) => {
        if (!catalogQuery.data || draft.selectedProductIds.size === 0) {
            message.warning('Bitte wählen Sie mindestens ein Produkt aus.');
            return;
        }

        const categoryGroups = enrichGroupsWithCatalog(
            CATEGORY_GROUPS,
            new Map(catalogQuery.data.categories.map((category) => [category.name, category.productCount])),
        );

        const request = buildImportRequest(
            catalogQuery.data,
            draft.selectedProductIds,
            overwrite,
            {
                selectedGroupNames: draft.selectedGroupNames,
                categoryGroups,
            },
        );
        const apiRequest = {
            ...request,
            ...toPriceAdjustmentRequest(priceAdjustment),
            ...toImageModeRequest(imageMode),
            productOverrides: draft.productOverrides,
        };

        const controller = new AbortController();
        importAbortRef.current = controller;
        setImporting(true);
        setImportProgress(null);

        try {
            const finalProgress = await runDemoImportWithProgress(
                apiRequest,
                {
                    onProgress: setImportProgress,
                    signal: controller.signal,
                },
                tenantId,
            );

            const result = finalProgress.result;
            if (!result?.success) {
                message.error(result?.errorMessage ?? finalProgress.message ?? 'Import fehlgeschlagen');
                return;
            }

            if (tenantId) {
                queryClient.invalidateQueries({ queryKey: ['products', tenantId] });
                queryClient.invalidateQueries({ queryKey: ['categories', tenantId] });
            } else {
                queryClient.invalidateQueries({ queryKey: ['products'] });
                queryClient.invalidateQueries({ queryKey: ['categories'] });
            }

            showImportSuccess(result);
        } catch (error) {
            if (error instanceof DOMException && error.name === 'AbortError') {
                message.info('Import abgebrochen');
            } else {
                message.error(extractImportErrorMessage(error));
            }
        } finally {
            setImporting(false);
            importAbortRef.current = null;
        }
    };

    return (
        <>
        <Modal
            title="Import läuft"
            open={importing}
            closable={false}
            mask={{ closable: false }}
            width={520}
            footer={
                <Button
                    danger
                    onClick={() => importAbortRef.current?.abort()}
                >
                    Abbrechen
                </Button>
            }
        >
            <DemoImportProgressPanel progress={importProgress} />
        </Modal>
        <Modal
            title={
                <Space>
                    <ImportOutlined />
                    <span>Demo Produkte importieren</span>
                </Space>
            }
            open={open && !importing}
            onCancel={onClose}
            width={820}
            footer={
                activeTab === 'template' ? (
                    <Button onClick={onClose}>Schließen</Button>
                ) : null
            }
        >
            <Alert
                title={`Produkte für "${tenantName}"`}
                description="Geführter Import-Schritt für Schritt oder eigene CSV/Excel-Vorlage."
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
            />

            <Tabs
                activeKey={activeTab}
                onChange={(key) => setActiveTab(key as 'wizard' | 'template')}
                items={[
                    {
                        key: 'wizard',
                        label: 'Import-Assistent',
                        children: (
                            <DemoImportCatalogWizard
                                key={wizardKey}
                                catalog={catalogQuery.data}
                                isLoading={catalogQuery.isLoading}
                                isError={catalogQuery.isError}
                                tenantId={tenantId}
                                priceAdjustment={priceAdjustment}
                                onPriceAdjustmentChange={setPriceAdjustment}
                                imageMode={imageMode}
                                onImageModeChange={setImageMode}
                                overwrite={overwrite}
                                onOverwriteChange={setOverwrite}
                                onRequestImport={handleWizardImport}
                                importLoading={importing}
                            />
                        ),
                    },
                    {
                        key: 'template',
                        label: 'Eigene Vorlage',
                        children: (
                            <DemoTemplateImportTab
                                tenantId={tenantId}
                                overwrite={overwrite}
                                priceAdjustment={priceAdjustment}
                                onPriceAdjustmentChange={setPriceAdjustment}
                                imageMode={imageMode}
                                onImportSuccess={showImportSuccess}
                            />
                        ),
                    },
                ]}
            />
        </Modal>
        <DemoImportSummaryModal
            open={summaryResult != null}
            result={summaryResult ?? { success: true, created: 0, updated: 0, skipped: 0 }}
            tenantSlug={tenantSlug}
            onClose={() => setSummaryResult(null)}
            onDone={finishSummary}
        />
        </>
    );
}
