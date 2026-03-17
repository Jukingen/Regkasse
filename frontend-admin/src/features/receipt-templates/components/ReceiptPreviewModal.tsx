'use client';

import React from 'react';
import { Modal, Spin, Alert, Typography } from 'antd';
import { useReceiptTemplates } from '../hooks/useReceiptTemplates';

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
    const { usePreview } = useReceiptTemplates();
    const { data, isLoading, isError, error } = usePreview(templateId!, {
        query: { enabled: !!templateId },
    });

    const notFound = isError && isNotFoundError(error);
    const errorMessage = notFound
        ? 'The template may have been deleted or you do not have access to it.'
        : (error as Error)?.message;

    return (
        <Modal
            title={data?.templateName ? `Preview: ${data.templateName}` : 'Preview'}
            open={!!templateId}
            onCancel={onClose}
            footer={null}
            width={700}
        >
            {isLoading && <Spin tip="Loading preview..." />}
            {isError && (
                <Alert
                    type="error"
                    message={notFound ? 'Template not found' : 'Preview failed'}
                    description={errorMessage}
                    showIcon
                />
            )}
            {data && (
                <div>
                    <p>
                        <strong>Language:</strong> {data.language}
                    </p>
                    <p>
                        <strong>Type:</strong> {data.templateType}
                    </p>
                    <Paragraph>
                        <pre style={{ background: '#f5f5f5', padding: 16, whiteSpace: 'pre-wrap' }}>
                            {data.previewContent || 'No preview available'}
                        </pre>
                    </Paragraph>
                </div>
            )}
        </Modal>
    );
}
