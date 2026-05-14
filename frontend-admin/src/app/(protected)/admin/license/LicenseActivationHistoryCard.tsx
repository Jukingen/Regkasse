'use client';

/**
 * Admin audit list for POST /api/license/activate (German copy via `license.activationHistory.*`).
 */

import React, { useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    DatePicker,
    Form,
    Input,
    Popconfirm,
    Select,
    Space,
    Switch,
    Table,
    Tag,
    Typography,
    message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import { ReloadOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import {
    getLicenseActivationAttempts,
    licenseQueryKeys,
    postForceDeactivateActivationAttempt,
    type LicenseActivationAttemptListItemDto,
    type LicenseActivationAttemptsListParams,
} from '@/api/manual/adminLicense';
import { useI18n, formatDate } from '@/i18n';

dayjs.extend(utc);

type FilterFormValues = {
    licenseKey?: string;
    machineFingerprint?: string;
    status?: string;
    dateRange?: [Dayjs, Dayjs] | null;
    onlyFailed?: boolean;
};

export function LicenseActivationHistoryCard() {
    const { t, formatLocale } = useI18n();
    const queryClient = useQueryClient();
    const [form] = Form.useForm<FilterFormValues>();
    const [applied, setApplied] = useState<LicenseActivationAttemptsListParams>({ pageSize: 50 });
    const [page, setPage] = useState(1);

    const queryParams = useMemo(
        () => ({
            ...applied,
            pageNumber: page,
        }),
        [applied, page],
    );

    const listQuery = useQuery({
        queryKey: licenseQueryKeys.activationAttempts(queryParams),
        queryFn: () => getLicenseActivationAttempts(queryParams),
    });

    const forceMutation = useMutation({
        mutationFn: (id: string) => postForceDeactivateActivationAttempt(id),
        onSuccess: async () => {
            message.success(t('license.activationHistory.forceDeactivateSuccess'));
            await queryClient.invalidateQueries({ queryKey: licenseQueryKeys.activationAttemptsRoot });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const data = err.response?.data as { message?: string } | undefined;
                message.error(data?.message || t('license.activationHistory.forceDeactivateFailed'));
                return;
            }
            message.error(t('license.activationHistory.forceDeactivateFailed'));
        },
    });

    const statusColor = (s: string) => {
        if (s === 'Success') return 'green';
        if (s === 'Failed') return 'red';
        if (s === 'Revoked') return 'default';
        return 'blue';
    };

    const columns: ColumnsType<LicenseActivationAttemptListItemDto> = useMemo(
        () => [
            {
                title: t('license.activationHistory.colMaskedKey'),
                dataIndex: 'licenseKeyMasked',
                key: 'licenseKeyMasked',
                width: 200,
            },
            {
                title: t('license.activationHistory.colMachine'),
                dataIndex: 'machineFingerprint',
                key: 'machineFingerprint',
                ellipsis: true,
                render: (v: string) => (
                    <Typography.Text copyable={{ text: v }} style={{ fontFamily: 'ui-monospace, monospace' }}>
                        {v.length > 24 ? `${v.slice(0, 12)}…${v.slice(-8)}` : v}
                    </Typography.Text>
                ),
            },
            {
                title: t('license.activationHistory.colStatus'),
                dataIndex: 'activationStatus',
                key: 'activationStatus',
                width: 110,
                render: (s: string) => <Tag color={statusColor(s)}>{s}</Tag>,
            },
            {
                title: t('license.activationHistory.colFailure'),
                dataIndex: 'failureReason',
                key: 'failureReason',
                ellipsis: true,
                width: 220,
            },
            {
                title: t('license.activationHistory.colClientIp'),
                dataIndex: 'clientIp',
                key: 'clientIp',
                width: 130,
                render: (v: string | null) => v ?? '—',
            },
            {
                title: t('license.activationHistory.colUserAgent'),
                dataIndex: 'userAgent',
                key: 'userAgent',
                ellipsis: true,
                width: 160,
            },
            {
                title: t('license.activationHistory.colActivated'),
                dataIndex: 'activatedAtUtc',
                key: 'activatedAtUtc',
                width: 170,
                render: (iso: string) =>
                    formatDate(iso, formatLocale, {
                        year: 'numeric',
                        month: '2-digit',
                        day: '2-digit',
                        hour: '2-digit',
                        minute: '2-digit',
                        second: '2-digit',
                    }),
            },
            {
                title: t('license.activationHistory.colDeactivated'),
                dataIndex: 'deactivatedAtUtc',
                key: 'deactivatedAtUtc',
                width: 170,
                render: (iso: string | null) =>
                    iso
                        ? formatDate(iso, formatLocale, {
                              year: 'numeric',
                              month: '2-digit',
                              day: '2-digit',
                              hour: '2-digit',
                              minute: '2-digit',
                              second: '2-digit',
                          })
                        : '—',
            },
            {
                title: '',
                key: 'actions',
                fixed: 'right',
                width: 120,
                render: (_: unknown, row) =>
                    row.activationStatus === 'Success' && !row.deactivatedAtUtc ? (
                        <Popconfirm
                            title={t('license.activationHistory.forceDeactivate')}
                            description={t('license.activationHistory.forceDeactivateConfirm')}
                            okButtonProps={{
                                loading:
                                    forceMutation.isPending && forceMutation.variables === row.id,
                            }}
                            onConfirm={() => forceMutation.mutate(row.id)}
                        >
                            <Button
                                size="small"
                                danger
                                loading={forceMutation.isPending && forceMutation.variables === row.id}
                            >
                                {t('license.activationHistory.forceDeactivate')}
                            </Button>
                        </Popconfirm>
                    ) : null,
            },
        ],
        [t, formatLocale, forceMutation.isPending, forceMutation.variables],
    );

    const onApply = (values: FilterFormValues) => {
        const next: LicenseActivationAttemptsListParams = {
            pageSize: 50,
            licenseKey: values.licenseKey?.trim() ? values.licenseKey.trim().toUpperCase() : undefined,
            machineFingerprint: values.machineFingerprint?.trim() ? values.machineFingerprint.trim() : undefined,
        };
        if (values.onlyFailed) {
            next.status = 'Failed';
        } else if (values.status && values.status !== 'all') {
            next.status = values.status;
        }
        const r = values.dateRange;
        if (r?.[0] && r[1]) {
            next.fromUtc = r[0].utc().startOf('day').toISOString();
            next.toUtc = r[1].utc().endOf('day').toISOString();
        }
        setApplied(next);
        setPage(1);
    };

    const onReset = () => {
        form.resetFields();
        setApplied({ pageSize: 50 });
        setPage(1);
    };

    return (
        <Card
            title={t('license.activationHistory.title')}
            extra={
                <Button
                    icon={<ReloadOutlined />}
                    onClick={() => void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.activationAttemptsRoot })}
                >
                    {t('common.buttons.refresh')}
                </Button>
            }
        >
            <Typography.Paragraph type="secondary">{t('license.activationHistory.subtitle')}</Typography.Paragraph>

            <Form form={form} layout="vertical" onFinish={onApply} style={{ marginBottom: 16 }}>
                <Space wrap align="start">
                    <Form.Item name="licenseKey" label={t('license.activationHistory.licenseKeyFilter')} style={{ minWidth: 260 }}>
                        <Input allowClear placeholder={t('license.activationHistory.licenseKeyPlaceholder')} autoComplete="off" />
                    </Form.Item>
                    <Form.Item
                        name="machineFingerprint"
                        label={t('license.activationHistory.machineFingerprintFilter')}
                        style={{ minWidth: 220 }}
                    >
                        <Input allowClear autoComplete="off" />
                    </Form.Item>
                    <Form.Item name="status" label={t('license.activationHistory.statusFilter')} initialValue="all">
                        <Select
                            style={{ width: 160 }}
                            options={[
                                { value: 'all', label: t('license.activationHistory.statusAll') },
                                { value: 'Success', label: t('license.activationHistory.statusSuccess') },
                                { value: 'Failed', label: t('license.activationHistory.statusFailed') },
                                { value: 'Revoked', label: t('license.activationHistory.statusRevoked') },
                            ]}
                        />
                    </Form.Item>
                    <Form.Item name="dateRange" label={t('license.activationHistory.dateRange')}>
                        <DatePicker.RangePicker allowClear />
                    </Form.Item>
                    <Form.Item name="onlyFailed" label={t('license.activationHistory.onlyFailedToggle')} valuePropName="checked">
                        <Switch />
                    </Form.Item>
                    <Form.Item label=" ">
                        <Space>
                            <Button type="primary" htmlType="submit">
                                {t('license.activationHistory.applyFilters')}
                            </Button>
                            <Button onClick={onReset}>{t('license.activationHistory.resetFilters')}</Button>
                        </Space>
                    </Form.Item>
                </Space>
            </Form>

            {listQuery.isError ? (
                <Alert type="error" showIcon message={t('license.activationHistory.loadError')} />
            ) : (
                <Table<LicenseActivationAttemptListItemDto>
                    rowKey={(r) => r.id}
                    loading={listQuery.isFetching}
                    columns={columns}
                    dataSource={listQuery.data?.items ?? []}
                    scroll={{ x: 1100 }}
                    pagination={{
                        current: page,
                        pageSize: applied.pageSize ?? 50,
                        total: listQuery.data?.total ?? 0,
                        showSizeChanger: false,
                        onChange: (p) => setPage(p),
                    }}
                />
            )}
        </Card>
    );
}
