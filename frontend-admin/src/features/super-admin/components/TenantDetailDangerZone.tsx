'use client';

import React, { useCallback, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Checkbox,
    Input,
    Modal,
    Space,
    Typography,
} from 'antd';
import { DeleteOutlined, UndoOutlined, WarningOutlined } from '@ant-design/icons';
import Link from 'next/link';

import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { useI18n } from '@/i18n';

export type TenantDetailDangerZoneProps = {
    tenant: AdminTenantDetail;
    softDeletePending?: boolean;
    restorePending?: boolean;
    hardDeletePending?: boolean;
    onSoftDelete: () => void | Promise<void>;
    onRestore: () => void | Promise<void>;
    onHardDelete: (confirmSlug: string) => void | Promise<void>;
};

function buildTenantAuditLogsHref(tenantId: string): string {
    const qp = new URLSearchParams({ entityType: 'Tenant', entityId: tenantId });
    return `/audit-logs?${qp.toString()}`;
}

export function TenantDetailDangerZone({
    tenant,
    softDeletePending,
    restorePending,
    hardDeletePending,
    onSoftDelete,
    onRestore,
    onHardDelete,
}: TenantDetailDangerZoneProps) {
    const { t } = useI18n();
    const [softDeleteOpen, setSoftDeleteOpen] = useState(false);
    const [restoreOpen, setRestoreOpen] = useState(false);
    const [hardDeleteOpen, setHardDeleteOpen] = useState(false);
    const [confirmSlug, setConfirmSlug] = useState('');
    const [irreversibleAck, setIrreversibleAck] = useState(false);

    const slugMatches =
        confirmSlug.trim().toLowerCase() === tenant.slug.trim().toLowerCase();
    const hardDeleteReady = slugMatches && irreversibleAck;

    const closeHardDelete = useCallback(() => {
        setHardDeleteOpen(false);
        setConfirmSlug('');
        setIrreversibleAck(false);
    }, []);

    const isDeleted = tenant.status === 'deleted';

    return (
        <Card
            title={
                <Space>
                    <WarningOutlined style={{ color: '#cf1322' }} />
                    {t('tenants.detail.danger.title')}
                </Space>
            }
            style={{
                borderColor: '#ff4d4f',
                marginTop: 24,
            }}
            styles={{ header: { borderBottomColor: '#ffccc7' } }}
        >
            {isDeleted ? (
                <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Alert type="error" showIcon message={t('tenants.detail.settings.danger.deletedWarning')} />
                    <Space wrap>
                        <Button
                            icon={<UndoOutlined />}
                            loading={restorePending}
                            onClick={() => setRestoreOpen(true)}
                        >
                            {t('tenants.detail.settings.danger.restoreButton')}
                        </Button>
                        <Button
                            danger
                            type="primary"
                            icon={<DeleteOutlined />}
                            loading={hardDeletePending}
                            onClick={() => {
                                setConfirmSlug('');
                                setIrreversibleAck(false);
                                setHardDeleteOpen(true);
                            }}
                        >
                            {t('tenants.detail.settings.danger.hardDeleteButton')}
                        </Button>
                    </Space>
                </Space>
            ) : (
                <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                        {t('tenants.detail.settings.danger.softDeleteHint')}
                    </Typography.Paragraph>
                    <Button danger onClick={() => setSoftDeleteOpen(true)}>
                        {t('tenants.detail.settings.danger.softDeleteButton')}
                    </Button>
                </Space>
            )}

            <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
                <Link href={buildTenantAuditLogsHref(tenant.id)}>
                    {t('tenants.detail.settings.danger.auditLink')}
                </Link>
            </Typography.Paragraph>

            <Modal
                title={t('tenants.detail.settings.danger.softDeleteModalTitle')}
                open={softDeleteOpen}
                onCancel={() => setSoftDeleteOpen(false)}
                okText={t('tenants.detail.settings.danger.softDeleteConfirm')}
                okButtonProps={{ danger: true, loading: softDeletePending }}
                cancelText={t('common.cancel', { defaultValue: 'Abbrechen' })}
                onOk={async () => {
                    try {
                        await onSoftDelete();
                        setSoftDeleteOpen(false);
                    } catch {
                        /* parent toast */
                    }
                }}
                destroyOnClose
            >
                <Space direction="vertical" size="small">
                    <Typography.Text>{t('tenants.detail.settings.danger.softDeleteNoAccess')}</Typography.Text>
                    <Typography.Text type="secondary">
                        {t('tenants.detail.settings.danger.softDeleteRecoverable')}
                    </Typography.Text>
                </Space>
            </Modal>

            <Modal
                title={t('tenants.detail.settings.danger.restoreModalTitle')}
                open={restoreOpen}
                onCancel={() => setRestoreOpen(false)}
                okText={t('tenants.detail.settings.danger.restoreConfirm')}
                okButtonProps={{ loading: restorePending }}
                cancelText={t('common.cancel', { defaultValue: 'Abbrechen' })}
                onOk={async () => {
                    try {
                        await onRestore();
                        setRestoreOpen(false);
                    } catch {
                        /* parent toast */
                    }
                }}
                destroyOnClose
            >
                <Typography.Paragraph style={{ marginBottom: 0 }}>
                    {t('tenants.detail.settings.danger.restoreModalBody')}
                </Typography.Paragraph>
            </Modal>

            <Modal
                title={
                    <Space>
                        <WarningOutlined style={{ color: '#cf1322' }} />
                        {t('tenants.detail.settings.danger.hardDeleteModalTitle')}
                    </Space>
                }
                open={hardDeleteOpen}
                onCancel={closeHardDelete}
                okText={t('tenants.detail.settings.danger.hardDeleteConfirm')}
                okButtonProps={{ danger: true, disabled: !hardDeleteReady, loading: hardDeletePending }}
                cancelText={t('common.cancel', { defaultValue: 'Abbrechen' })}
                onOk={async () => {
                    try {
                        await onHardDelete(confirmSlug.trim());
                        closeHardDelete();
                    } catch {
                        /* keep open */
                    }
                }}
                destroyOnClose
            >
                <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Alert type="error" showIcon message={t('tenants.confirmHardDelete.irreversible')} />
                    <Typography.Paragraph style={{ marginBottom: 0 }}>
                        {t('tenants.detail.settings.danger.hardDeleteDataLoss', { name: tenant.name })}
                    </Typography.Paragraph>
                    <Typography.Text>
                        {t('tenants.detail.danger.confirmLabel', { slug: tenant.slug })}
                    </Typography.Text>
                    <Input
                        value={confirmSlug}
                        onChange={(e) => setConfirmSlug(e.target.value)}
                        placeholder={tenant.slug}
                        autoComplete="off"
                        status={confirmSlug.length > 0 && !slugMatches ? 'error' : undefined}
                    />
                    <Checkbox
                        checked={irreversibleAck}
                        onChange={(e) => setIrreversibleAck(e.target.checked)}
                    >
                        {t('tenants.detail.settings.danger.hardDeleteAck')}
                    </Checkbox>
                </Space>
            </Modal>
        </Card>
    );
}
