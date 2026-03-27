'use client';

import React from 'react';
import { Modal, Spin, Alert, Typography } from 'antd';
import { useReceiptTemplates } from '../hooks/useReceiptTemplates';
import { useI18n } from '@/i18n';

const { Paragraph } = Typography;

function isNotFoundError(err: unknown): boolean {
    const status =
        (err as { response?: { status?: number } })?.response?.status ??
        (err as { normalized?: { status?: number } })?.normalized?.status;
    return status === 404;
}

interface ReceiptPreviewModalProps {
    templateId: string | null;
    onClose: () => void;
}

export default function ReceiptPreviewModal({ templateId, onClose }: ReceiptPreviewModalProps) {
    const { t } = useI18n();
    const { usePreview } = useReceiptTemplates();
    const { data, isLoading, isError, error } = usePreview(templateId!, {
        query: { enabled: !!templateId },
    });

    const notFound = isError && isNotFoundError(error);
    const errorMessage = notFound
        ? t('receiptTemplates.preview.notFoundDescription')
        : (error as Error)?.message;

    return (
        <Modal
            title={data?.templateName ? t('receiptTemplates.preview.titleWithName', { name: data.templateName }) : t('receiptTemplates.preview.title')}
            open={!!templateId}
            onCancel={onClose}
            footer={null}
            width={700}
        >
            {isLoading && <Spin tip={t('receiptTemplates.preview.loading')} />}
            {isError && (
                <Alert
                    type="error"
                    message={notFound ? t('receiptTemplates.preview.errorNotFound') : t('receiptTemplates.preview.errorGeneric')}
                    description={errorMessage}
                    showIcon
                />
            )}
            {data && (
                <div>
                    <p>
                        <strong>{t('receiptTemplates.preview.labelLanguage')}:</strong> {data.language}
                    </p>
                    <p>
                        <strong>{t('receiptTemplates.preview.labelType')}:</strong> {data.templateType}
                    </p>
                    <Paragraph>
                        <pre style={{ background: '#f5f5f5', padding: 16, whiteSpace: 'pre-wrap' }}>
                            {data.previewContent || t('receiptTemplates.preview.noPreview')}
                        </pre>
                    </Paragraph>
                </div>
            )}
        </Modal>
    );
}
