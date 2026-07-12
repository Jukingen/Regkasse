'use client';

import { useState } from 'react';
import { Table, Card, Badge, Button, Space, Tag, Modal } from 'antd';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { useElmahErrors, type ElmahErrorRow } from '@/features/errors/hooks/useElmahErrors';
import { formatDate } from '@/lib/dateFormatter';
import { useAntdApp } from '@/hooks/useAntdApp';
import { customInstance } from '@/lib/axios';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useI18n } from '@/i18n';

export default function ElmahErrorsPage() {
    const { t } = useI18n();
    const { message, modal } = useAntdApp();
    const queryClient = useQueryClient();
    const [selectedError, setSelectedError] = useState<ElmahErrorRow | null>(null);
    const { data: errors, isLoading, refetch } = useElmahErrors();
    const [modalVisible, setModalVisible] = useState(false);

    const clearMutation = useMutation({
        mutationFn: async () => {
            await customInstance({ url: '/api/admin/errors', method: 'DELETE' });
        },
        onSuccess: async () => {
            message.success(t('adminShell.elmah.clearSuccess'));
            await queryClient.invalidateQueries({ queryKey: ['elmah-errors'] });
        },
        onError: () => {
            message.error(t('adminShell.elmah.clearFailed'));
        },
    });

    const handleClear = () => {
        modal.confirm({
            title: t('adminShell.elmah.clearConfirmTitle'),
            content: t('adminShell.elmah.clearConfirmBody'),
            okText: t('common.buttons.delete'),
            okButtonProps: { danger: true },
            cancelText: t('common.buttons.cancel'),
            onOk: () => clearMutation.mutateAsync(),
        });
    };

    const columns = [
        {
            title: t('adminShell.elmah.columns.date'),
            dataIndex: 'timeUtc',
            key: 'timeUtc',
            render: (date: string) => formatDate(date),
        },
        {
            title: t('adminShell.elmah.columns.status'),
            dataIndex: 'statusCode',
            key: 'statusCode',
            render: (code: number) => (
                <Badge color={code >= 500 ? 'red' : code >= 400 ? 'orange' : 'green'} text={String(code)} />
            ),
        },
        {
            title: t('adminShell.elmah.columns.message'),
            dataIndex: 'message',
            key: 'message',
            ellipsis: true,
        },
        {
            title: t('adminShell.elmah.columns.type'),
            dataIndex: 'type',
            key: 'type',
            render: (type: string) => <Tag color="blue">{type}</Tag>,
        },
        {
            title: t('adminShell.elmah.columns.user'),
            dataIndex: 'user',
            key: 'user',
        },
        {
            title: t('adminShell.elmah.columns.actions'),
            key: 'actions',
            render: (_: unknown, record: ElmahErrorRow) => (
                <Button
                    size="small"
                    onClick={() => {
                        setSelectedError(record);
                        setModalVisible(true);
                    }}
                >
                    {t('adminShell.elmah.details')}
                </Button>
            ),
        },
    ];

    return (
        <AdminPageShell>
            <AdminPageHeader title={t('nav.errorLogs')} />
            <Card
                title={t('nav.errorLogs')}
                extra={
                    <Space>
                        <Button onClick={() => refetch()}>{t('common.buttons.refresh')}</Button>
                        <Button danger loading={clearMutation.isPending} onClick={handleClear}>
                            {t('adminShell.elmah.clearLogs')}
                        </Button>
                    </Space>
                }
            >
                <Table
                    columns={columns}
                    dataSource={errors ?? []}
                    loading={isLoading}
                    rowKey="errorId"
                    pagination={{ pageSize: 20 }}
                />
            </Card>

            <Modal
                title={t('adminShell.elmah.detailsTitle')}
                open={modalVisible}
                onCancel={() => setModalVisible(false)}
                footer={[
                    <Button key="close" onClick={() => setModalVisible(false)}>
                        {t('common.buttons.close')}
                    </Button>,
                ]}
                width={800}
                destroyOnHidden
            >
                {selectedError && (
                    <pre style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
                        {JSON.stringify(selectedError, null, 2)}
                    </pre>
                )}
            </Modal>
        </AdminPageShell>
    );
}
