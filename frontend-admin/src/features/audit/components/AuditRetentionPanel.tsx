'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useCallback, useEffect, useState } from 'react';
import { Alert, Button, Card, DatePicker, Form, Input, List, Space, Typography } from 'antd';
import dayjs from 'dayjs';

import {
    useDeleteApiAdminLegalHoldId,
    useGetApiAdminLegalHold,
    usePostApiAdminLegalHold,
} from '@/api/generated/admin/admin';
import { fetchAuditRetention } from '@/features/audit/api/auditAdmin';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { useI18n } from '@/i18n';

export function AuditRetentionPanel() {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const [retentionYears, setRetentionYears] = useState(7);
    const [minCutoff, setMinCutoff] = useState<string | null>(null);
    const [cleanupDate, setCleanupDate] = useState<dayjs.Dayjs | null>(null);
    const [cleanupLoading, setCleanupLoading] = useState(false);
    const [holdForm] = Form.useForm();

    const { data: holds, refetch: refetchHolds } = useGetApiAdminLegalHold({ activeOnly: true });
    const createHold = usePostApiAdminLegalHold();
    const deleteHold = useDeleteApiAdminLegalHoldId();

    useEffect(() => {
        fetchAuditRetention()
            .then((info) => {
                setRetentionYears(info.retentionYears);
                setMinCutoff(info.minCutoffDate);
            })
            .catch(() => {
                /* optional panel */
            });
    }, []);

    const runCleanup = useCallback(async () => {
        if (!cleanupDate) {
            message.warning(t('common.auditLogs.cleanupDateRequired'));
            return;
        }
        setCleanupLoading(true);
        try {
            const res = await AXIOS_INSTANCE.delete<{ deletedCount?: number; skippedDueToLegalHoldCount?: number; message?: string }>(
                '/api/AuditLog/cleanup',
                { data: { cutoffDate: cleanupDate.format('YYYY-MM-DD') } },
            );
            message.success(
                t('common.auditLogs.cleanupSuccess', {
                    deleted: String(res.data.deletedCount ?? 0),
                    skipped: String(res.data.skippedDueToLegalHoldCount ?? 0),
                }),
            );
        } catch (e) {
            message.error(e instanceof Error ? e.message : t('common.auditLogs.cleanupFailed'));
        } finally {
            setCleanupLoading(false);
        }
    }, [cleanupDate, t]);

    const createLegalHold = async () => {
        const values = await holdForm.validateFields();
        await createHold.mutateAsync({
            data: {
                fromDate: (values.fromDate as dayjs.Dayjs).format('YYYY-MM-DD'),
                toDate: (values.toDate as dayjs.Dayjs).format('YYYY-MM-DD'),
                reason: values.reason as string,
            },
        });
        message.success(t('common.auditLogs.legalHoldCreated'));
        holdForm.resetFields();
        refetchHolds();
    };

    return (
        <Card size="small" title={t('common.auditLogs.retentionCardTitle')}>
            <Space orientation="vertical" style={{ width: '100%' }} size="middle">
                <Alert
                    type="info"
                    showIcon
                    title={t('common.auditLogs.retentionPolicy', { years: String(retentionYears) })}
                    description={
                        minCutoff
                            ? t('common.auditLogs.retentionMinCutoff', {
                                  date: dayjs(minCutoff).format('DD.MM.YYYY'),
                              })
                            : undefined
                    }
                />

                <Typography.Text strong>{t('common.auditLogs.cleanupTitle')}</Typography.Text>
                <Space wrap>
                    <DatePicker
                        value={cleanupDate}
                        onChange={setCleanupDate}
                        format="DD.MM.YYYY"
                        disabledDate={(d) => (minCutoff ? d.isAfter(dayjs(minCutoff)) : false)}
                    />
                    <Button danger loading={cleanupLoading} onClick={runCleanup}>
                        {t('common.auditLogs.cleanupTrigger')}
                    </Button>
                </Space>

                <Typography.Text strong>{t('common.auditLogs.legalHoldTitle')}</Typography.Text>
                <Form form={holdForm} layout="inline">
                    <Form.Item name="fromDate" rules={[{ required: true }]}>
                        <DatePicker placeholder={t('common.auditLogs.legalHoldFrom')} format="DD.MM.YYYY" />
                    </Form.Item>
                    <Form.Item name="toDate" rules={[{ required: true }]}>
                        <DatePicker placeholder={t('common.auditLogs.legalHoldTo')} format="DD.MM.YYYY" />
                    </Form.Item>
                    <Form.Item name="reason">
                        <Input placeholder={t('common.auditLogs.legalHoldReason')} style={{ minWidth: 160 }} />
                    </Form.Item>
                    <Form.Item>
                        <Button type="primary" loading={createHold.isPending} onClick={createLegalHold}>
                            {t('common.auditLogs.legalHoldCreate')}
                        </Button>
                    </Form.Item>
                </Form>

                <List
                    size="small"
                    header={t('common.auditLogs.legalHoldActiveList')}
                    dataSource={holds ?? []}
                    locale={{ emptyText: t('common.auditLogs.legalHoldEmpty') }}
                    renderItem={(item) => (
                        <List.Item
                            actions={[
                                <Button
                                    key="lift"
                                    type="link"
                                    danger
                                    size="small"
                                    loading={deleteHold.isPending}
                                    onClick={async () => {
                                        if (!item.id) return;
                                        await deleteHold.mutateAsync({ id: item.id });
                                        message.success(t('common.auditLogs.legalHoldLifted'));
                                        refetchHolds();
                                    }}
                                >
                                    {t('common.auditLogs.legalHoldLift')}
                                </Button>,
                            ]}
                        >
                            {dayjs(item.fromDate).format('DD.MM.YYYY')} – {dayjs(item.toDate).format('DD.MM.YYYY')}
                            {item.reason ? ` · ${item.reason}` : ''}
                        </List.Item>
                    )}
                />
            </Space>
        </Card>
    );
}
