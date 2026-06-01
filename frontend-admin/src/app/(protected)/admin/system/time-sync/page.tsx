'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useEffect } from 'react';
import { Badge, Button, Card, Col, Descriptions, Form, InputNumber, Row, Space, Spin, Switch, Table, Tag, Typography } from 'antd';
import { ReloadOutlined, SaveOutlined, SyncOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import {
    getAdminTimeSyncLogs,
    getAdminTimeSyncStatus,
    postManualTimeSync,
    putAdminTimeSyncConfiguration,
    type NtpAdminConfigurationUpdateDto,
} from '@/api/manual/adminSystemTimeSync';

dayjs.extend(utc);

function badgeTag(statusBadge: string, t: (k: string) => string): React.ReactNode {
    const s = statusBadge.toLowerCase();
    if (s === 'critical')
        return (
            <Tag color="red">
                {t('timeSync.badge.critical')}
            </Tag>
        );
    if (s === 'warning')
        return (
            <Tag color="orange">
                {t('timeSync.badge.warning')}
            </Tag>
        );
    return (
        <Tag color="green">
            {t('timeSync.badge.synchronized')}
        </Tag>
    );
}

export default function AdminTimeSyncPage() {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const queryClient = useQueryClient();
    const [form] = Form.useForm<NtpAdminConfigurationUpdateDto>();

    const statusQuery = useQuery({
        queryKey: ['admin', 'time-sync', 'status'],
        queryFn: () => getAdminTimeSyncStatus(),
    });

    const logsQuery = useQuery({
        queryKey: ['admin', 'time-sync', 'logs'],
        queryFn: () => getAdminTimeSyncLogs(),
    });

    const cfg = statusQuery.data?.effectiveConfiguration;

    useEffect(() => {
        if (!cfg) return;
        form.setFieldsValue({
            autoSyncEnabled: cfg.autoSyncEnabled,
            syncIntervalMinutes: cfg.syncIntervalMinutes,
            maxAllowedOffsetSeconds: cfg.maxAllowedOffsetSeconds,
            criticalOffsetSeconds: cfg.criticalOffsetSeconds,
        });
    }, [cfg, form]);

    const syncMutation = useMutation({
        mutationFn: () => postManualTimeSync(),
        onSuccess: (res) => {
            void queryClient.invalidateQueries({ queryKey: ['admin', 'time-sync'] });
            void queryClient.invalidateQueries({ queryKey: ['admin', 'time-sync', 'drift-summary'] });
            if (res.success) message.success(t('timeSync.actions.syncSuccess'));
            else message.error(res.message || t('timeSync.actions.syncFailed'));
        },
        onError: () => message.error(t('timeSync.actions.syncFailed')),
    });

    const saveMutation = useMutation({
        mutationFn: (body: NtpAdminConfigurationUpdateDto) => putAdminTimeSyncConfiguration(body),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ['admin', 'time-sync'] });
            message.success(t('timeSync.actions.saved'));
        },
        onError: () => message.error(t('common.messages.unknownError')),
    });

    const s = statusQuery.data;

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <AdminPageHeader
                title={t('timeSync.page.title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
                    { title: t('timeSync.page.title'), href: '/admin/system/time-sync' },
                ]}
                actions={
                    <Space>
                        <Button
                            icon={<ReloadOutlined />}
                            onClick={() => {
                                void queryClient.invalidateQueries({ queryKey: ['admin', 'time-sync'] });
                            }}
                        >
                            {t('common.buttons.refresh')}
                        </Button>
                        <Button
                            type="primary"
                            icon={<SyncOutlined />}
                            loading={syncMutation.isPending}
                            onClick={() => syncMutation.mutate()}
                        >
                            {t('timeSync.actions.syncNow')}
                        </Button>
                    </Space>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t('timeSync.page.subtitle')}
                </Typography.Paragraph>
            </AdminPageHeader>

            {statusQuery.isLoading ? (
                <Spin />
            ) : s ? (
                <Row gutter={[16, 16]}>
                    <Col xs={24} lg={14}>
                        <Card title={t('timeSync.statusCard.title')}>
                            <Descriptions column={1} size="small">
                                <Descriptions.Item label={t('timeSync.statusCard.systemUtc')}>
                                    {dayjs.utc(s.systemTimeUtc).format('YYYY-MM-DD HH:mm:ss')}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('timeSync.statusCard.systemLocal')}>
                                    {s.systemTimeLocalVienna}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('timeSync.statusCard.ntpReference')}>
                                    {s.ntpTimeUtc ? dayjs.utc(s.ntpTimeUtc).format('YYYY-MM-DD HH:mm:ss') : '—'}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('timeSync.statusCard.offset')}>
                                    {s.offsetSeconds != null ? s.offsetSeconds.toFixed(3) : '—'}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('timeSync.statusCard.lastSync')}>
                                    {s.lastSyncAt ? dayjs.utc(s.lastSyncAt).format('YYYY-MM-DD HH:mm:ss') : '—'}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('timeSync.statusCard.badge')}>
                                    {badgeTag(s.statusBadge, t)}
                                </Descriptions.Item>
                            </Descriptions>
                        </Card>
                    </Col>
                    <Col xs={24} lg={10}>
                        <Card title={t('timeSync.config.title')}>
                            {cfg?.hasDatabaseOverride ? (
                                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                                    {t('timeSync.config.dbOverrideHint')}
                                </Typography.Paragraph>
                            ) : null}
                            <Form
                                form={form}
                                layout="vertical"
                                onFinish={(values) => saveMutation.mutate(values)}
                                initialValues={
                                    cfg
                                        ? {
                                              autoSyncEnabled: cfg.autoSyncEnabled,
                                              syncIntervalMinutes: cfg.syncIntervalMinutes,
                                              maxAllowedOffsetSeconds: cfg.maxAllowedOffsetSeconds,
                                              criticalOffsetSeconds: cfg.criticalOffsetSeconds,
                                          }
                                        : undefined
                                }
                            >
                                <Form.Item
                                    name="autoSyncEnabled"
                                    label={t('timeSync.config.autoSync')}
                                    valuePropName="checked"
                                >
                                    <Switch />
                                </Form.Item>
                                <Form.Item
                                    name="syncIntervalMinutes"
                                    label={t('timeSync.config.intervalMinutes')}
                                    rules={[{ required: true, type: 'number', min: 1, max: 1440 }]}
                                >
                                    <InputNumber style={{ width: '100%' }} min={1} max={1440} />
                                </Form.Item>
                                <Form.Item
                                    name="maxAllowedOffsetSeconds"
                                    label={t('timeSync.config.maxOffset')}
                                    rules={[{ required: true, type: 'number', min: 1, max: 3600 }]}
                                >
                                    <InputNumber style={{ width: '100%' }} min={1} max={3600} />
                                </Form.Item>
                                <Form.Item
                                    name="criticalOffsetSeconds"
                                    label={t('timeSync.config.criticalOffset')}
                                    rules={[{ required: true, type: 'number', min: 1, max: 86400 }]}
                                >
                                    <InputNumber style={{ width: '100%' }} min={1} max={86400} />
                                </Form.Item>
                                <Button
                                    type="primary"
                                    htmlType="submit"
                                    icon={<SaveOutlined />}
                                    loading={saveMutation.isPending}
                                >
                                    {t('timeSync.actions.saveConfig')}
                                </Button>
                            </Form>
                        </Card>
                    </Col>
                </Row>
            ) : null}

            <Card title={t('timeSync.history.title')}>
                <Table
                    loading={logsQuery.isLoading}
                    rowKey="id"
                    pagination={false}
                    dataSource={logsQuery.data ?? []}
                    scroll={{ x: true }}
                    columns={[
                        {
                            title: t('timeSync.history.syncTime'),
                            dataIndex: 'syncTimeUtc',
                            render: (v: string) => dayjs.utc(v).format('YYYY-MM-DD HH:mm:ss'),
                        },
                        {
                            title: t('timeSync.history.offset'),
                            dataIndex: 'offsetSeconds',
                            render: (v: number) => v.toFixed(3),
                        },
                        {
                            title: t('timeSync.history.ntpServer'),
                            dataIndex: 'ntpServerUsed',
                            ellipsis: true,
                        },
                        {
                            title: t('timeSync.history.result'),
                            dataIndex: 'isSuccess',
                            render: (ok: boolean, row) =>
                                ok ? (
                                    <Badge status="success" text={t('timeSync.history.success')} />
                                ) : (
                                    <Space orientation="vertical" size={0}>
                                        <Badge status="error" text={t('timeSync.history.failed')} />
                                        {row.errorMessage ? (
                                            <Typography.Text type="danger" ellipsis style={{ maxWidth: 280 }}>
                                                {row.errorMessage}
                                            </Typography.Text>
                                        ) : null}
                                    </Space>
                                ),
                        },
                    ]}
                />
            </Card>
        </div>
    );
}
