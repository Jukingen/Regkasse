'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import {
    DEV_PURGE_CATALOG_CONFIRM_PHRASE,
    DEV_PURGE_CATALOG_FISCAL_OVERRIDE_PHRASE,
    useDevPurgeAdminCatalog,
} from '@/api/admin/products';
import { useI18n } from '@/i18n';
import { extractRawApiErrorMessage } from '@/shared/errors/extractRawApiErrorMessage';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';
import { DeleteOutlined } from '@ant-design/icons';
import { Alert, Button, Checkbox, Modal, Space, Typography } from 'antd';
import { useCallback, useState } from 'react';

type DevCatalogPurgeButtonProps = {
    tenantSlug: string;
    tenantId?: string;
    productCount: number;
    categoryCount?: number;
    loading?: boolean;
    onSuccess?: () => void;
};

function isFiscalBlockError(error: unknown): boolean {
    const raw = extractRawApiErrorMessage(error).toLowerCase();
    return (
        raw.includes('signed fiscal') ||
        raw.includes('fiscal payment') ||
        raw.includes('rksv/tse') ||
        raw.includes('dev-purge-catalog-with-fiscal')
    );
}

export function DevCatalogPurgeButton({
    tenantSlug,
    tenantId,
    productCount,
    categoryCount = 0,
    loading,
    onSuccess,
}: DevCatalogPurgeButtonProps) {
    const { message } = useAntdApp();
    const { t } = useI18n();
    const purgeMutation = useDevPurgeAdminCatalog();

    const [open, setOpen] = useState(false);
    const [includeCategories, setIncludeCategories] = useState(true);
    const [acknowledged, setAcknowledged] = useState(false);

    const resetModal = useCallback(() => {
        setOpen(false);
        setIncludeCategories(true);
        setAcknowledged(false);
    }, []);

    const runPurge = useCallback(async () => {
        const baseRequest = {
            tenantSlug,
            tenantId,
            includeCategories,
        };
        try {
            return await purgeMutation.mutateAsync({
                ...baseRequest,
                confirmPhrase: DEV_PURGE_CATALOG_CONFIRM_PHRASE,
            });
        } catch (error) {
            if (!isFiscalBlockError(error)) {
                throw error;
            }
            message.warning(t('products.devPurge.fiscalAutoRetry'));
            return purgeMutation.mutateAsync({
                ...baseRequest,
                confirmPhrase: DEV_PURGE_CATALOG_FISCAL_OVERRIDE_PHRASE,
            });
        }
    }, [includeCategories, message, purgeMutation, t, tenantId, tenantSlug]);

    const handleConfirm = useCallback(async () => {
        try {
            const result = await runPurge();
            message.success(
                t('products.devPurge.success', {
                    products: result.productsDeleted,
                    categories: result.categoriesDeleted,
                }),
            );
            resetModal();
            onSuccess?.();
        } catch (error) {
            openApiErrorMessage(message.open, t, error, {
                logContext: 'DevCatalogPurgeButton',
                fallbackKey: 'products.devPurge.error',
            });
            throw error;
        }
    }, [message.open, onSuccess, resetModal, runPurge, t]);

    return (
        <>
            <Button
                danger
                icon={<DeleteOutlined />}
                loading={loading || purgeMutation.isPending}
                onClick={() => setOpen(true)}
            >
                {t('products.devPurge.button')}
            </Button>

            <Modal
                title={t('products.devPurge.title')}
                open={open}
                onCancel={resetModal}
                onOk={handleConfirm}
                okText={t('products.devPurge.confirm')}
                okButtonProps={{
                    danger: true,
                    disabled: !acknowledged,
                    loading: purgeMutation.isPending,
                }}
                cancelText={t('common.buttons.cancel')}
                destroyOnHidden
            >
                <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                    <Alert type="warning" showIcon title={t('products.devPurge.devOnlyHint')} />
                    <Alert type="info" showIcon title={t('products.devPurge.rksvHintTitle')} description={t('products.devPurge.rksvHintDescription')} />
                    <Typography.Paragraph style={{ marginBottom: 0 }}>
                        {t('products.devPurge.body', { count: productCount, slug: tenantSlug })}
                    </Typography.Paragraph>
                    {productCount === 0 && categoryCount > 0 ? (
                        <Typography.Text type="secondary">
                            {t('products.devPurge.categoriesOnlyHint', { count: categoryCount })}
                        </Typography.Text>
                    ) : null}
                    {productCount === 0 && categoryCount === 0 ? (
                        <Typography.Text type="secondary">{t('products.devPurge.emptyCatalogHint')}</Typography.Text>
                    ) : null}
                    <Checkbox checked={includeCategories} onChange={(e) => setIncludeCategories(e.target.checked)}>
                        {t('products.devPurge.includeCategories')}
                    </Checkbox>
                    <Checkbox checked={acknowledged} onChange={(e) => setAcknowledged(e.target.checked)}>
                        {t('products.devPurge.acknowledge')}
                    </Checkbox>
                </Space>
            </Modal>
        </>
    );
}
