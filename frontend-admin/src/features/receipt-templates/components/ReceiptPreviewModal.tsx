'use client';

import React from 'react';
import { Modal, Spin, Alert, Typography } from 'antd';
import { useReceiptTemplates } from '../hooks/useReceiptTemplates';

const { Paragraph } = Typography;

interface ReceiptPreviewModalProps {
    templateId: string | null;
    onClose: () => void;
}

export default function ReceiptPreviewModal({ templateId, onClose }: ReceiptPreviewModalProps) {
    const { usePreview } = useReceiptTemplates();
    const { data, isLoading, isError, error } = usePreview(templateId!, {
        query: { enabled: !!templateId },
    });

    return (
        <Modal
            title={`Preview: ${data?.templateName || 'Receipt Template'}`}
            open={!!templateId}
            onCancel={onClose}
            footer={null}
            width={700}
        >
            {isLoading && <Spin />}
            {isError && (
                <Alert
                    type="error"
                    message="Preview failed"
                    description={(error as Error)?.message}
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
