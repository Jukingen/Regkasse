'use client';

import { useMemo } from 'react';
import { Button, Col, Descriptions, Modal, Row, Typography } from 'antd';
import { LinkOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';

import type { AuditLogEntryDto } from '@/api/generated/model';
import { useGetApiAuditLogCorrelationCorrelationId } from '@/api/generated/audit-log/audit-log';
import { parseAuditJsonField } from '@/features/audit-logs/utils/parseAuditJsonField';
import { useI18n } from '@/i18n';

type Props = {
    open: boolean;
    record: AuditLogEntryDto | null;
    onClose: () => void;
};

function JsonBlock({ label, raw }: { label: string; raw?: string | null }) {
    if (!raw?.trim()) return null;
    let formatted = raw;
    try {
        formatted = JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
        /* keep raw */
    }
    return (
        <div style={{ marginBottom: 16 }}>
            <Typography.Text strong>{label}</Typography.Text>
            <pre
                style={{
                    marginTop: 8,
                    maxHeight: 200,
                    overflow: 'auto',
                    fontSize: 11,
                    background: '#fafafa',
                    padding: 8,
                    borderRadius: 4,
                }}
            >
                {formatted}
            </pre>
        </div>
    );
}

function DiffPanel({ title, raw }: { title: string; raw?: string | null }) {
    if (!raw?.trim()) return null;
    return (
        <Col xs={24} md={12}>
            <Typography.Text strong>{title}</Typography.Text>
            <pre
                style={{
                    marginTop: 8,
                    maxHeight: 240,
                    overflow: 'auto',
                    fontSize: 11,
                    background: '#fafafa',
                    padding: 8,
                    borderRadius: 4,
                }}
            >
                {raw}
            </pre>
        </Col>
    );
}

export function AuditDetailModal({ open, record, onClose }: Props) {
    const { t } = useI18n();
    const correlationId = record?.correlationId?.trim();

    const { data: relatedData, isLoading: relatedLoading } = useGetApiAuditLogCorrelationCorrelationId(
        correlationId ?? '',
        { query: { enabled: open && !!correlationId } },
    );

    const related = relatedData?.auditLogs ?? [];

    const parsedChanges = useMemo(() => {
        if (!record?.changes?.trim()) return null;
        try {
            return JSON.parse(record.changes) as unknown;
        } catch {
            return null;
        }
    }, [record?.changes]);

    if (!record) return null;

    return (
        <Modal
            open={open}
            onCancel={onClose}
            footer={null}
            width={960}
            title={t('common.auditLogs.detailModalTitle')}
            destroyOnClose
        >
            <Descriptions size="small" column={2} bordered>
                <Descriptions.Item label={t('common.auditLogs.table.time')}>
                    {record.timestamp ? dayjs(record.timestamp).format('DD.MM.YYYY HH:mm:ss') : '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('common.auditLogs.table.action')}>{record.action ?? '—'}</Descriptions.Item>
                <Descriptions.Item label={t('common.auditLogs.table.user')}>
                    {record.actorDisplayName ?? record.userId ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('common.auditLogs.table.status')}>{String(record.status ?? '—')}</Descriptions.Item>
                <Descriptions.Item label={t('common.auditLogs.table.entity')}>
                    {record.entityType}
                    {record.entityId ? ` · ${record.entityId}` : ''}
                </Descriptions.Item>
                <Descriptions.Item label="IP">{record.ipAddress ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Correlation" span={2}>
                    {correlationId ? (
                        <Typography.Text code copyable>
                            {correlationId}
                        </Typography.Text>
                    ) : (
                        '—'
                    )}
                </Descriptions.Item>
            </Descriptions>

            <Typography.Title level={5} style={{ marginTop: 16 }}>
                {t('common.auditLogs.detailBeforeAfter')}
            </Typography.Title>
            <Row gutter={16}>
                <DiffPanel title={t('common.auditLogs.detailOldValues')} raw={record.oldValues} />
                <DiffPanel title={t('common.auditLogs.detailNewValues')} raw={record.newValues} />
            </Row>

            {parsedChanges ? (
                <JsonBlock label={t('common.auditLogs.detailChanges')} raw={JSON.stringify(parsedChanges, null, 2)} />
            ) : null}

            <JsonBlock label={t('common.auditLogs.detailRequest')} raw={record.requestData} />
            <JsonBlock label={t('common.auditLogs.detailResponse')} raw={record.responseData} />

            {correlationId ? (
                <>
                    <Typography.Title level={5}>
                        <LinkOutlined /> {t('common.auditLogs.detailRelatedEvents')}
                    </Typography.Title>
                    {relatedLoading ? (
                        <Typography.Text type="secondary">{t('common.loading.data')}</Typography.Text>
                    ) : (
                        <ul style={{ paddingLeft: 20, margin: 0 }}>
                            {related.map((r) => (
                                <li key={r.id}>
                                    {dayjs(r.timestamp).format('HH:mm:ss')} — {r.action}{' '}
                                    <Typography.Text type="secondary">
                                        ({parseAuditJsonField(r.metadata, 'targetUserId') ?? r.entityName ?? r.entityType})
                                    </Typography.Text>
                                </li>
                            ))}
                        </ul>
                    )}
                </>
            ) : null}

            <div style={{ marginTop: 16, textAlign: 'right' }}>
                <Button onClick={onClose}>{t('common.buttons.close')}</Button>
            </div>
        </Modal>
    );
}
