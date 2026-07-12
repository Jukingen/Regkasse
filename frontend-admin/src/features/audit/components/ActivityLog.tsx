'use client';

import { useCallback, useMemo, useState } from 'react';
import { Button, Card, DatePicker, Input, Select, Space, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import { FileExcelOutlined, ReloadOutlined } from '@ant-design/icons';

import {
    ACTIVITY_LOG_ACTION_FILTER_VALUES,
    activityLogActionTagColor,
} from '@/features/audit/constants/activityLogActions';
import {
    defaultActivityLogFilters,
    type ActivityLogFilters,
    type ActivityLogRow,
    useActivityLog,
} from '@/features/audit/hooks/useActivityLog';
import { getAuditActionLabelKey } from '@/features/audit-logs/utils/auditActionLabels';
import { resolveActivityActionLabel } from '@/features/audit/utils/resolveActivityActionLabel';
import { buildAuditLogExportQuery } from '@/features/audit-logs/utils/buildAuditLogExportQuery';
import { downloadAuditLogExport } from '@/features/audit-logs/utils/exportAuditLogs';
import { useTenantStaff } from '@/features/staff/hooks/useTenantStaff';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

const { RangePicker } = DatePicker;

function staffDisplayName(firstName?: string | null, lastName?: string | null, userName?: string | null): string {
    const full = [firstName, lastName].filter(Boolean).join(' ').trim();
    return full || userName?.trim() || '—';
}

export function ActivityLog() {
    const { t, formatLocale } = useI18n();
    const { message } = useAntdApp();
    const [filters, setFilters] = useState<ActivityLogFilters>(defaultActivityLogFilters);
    const [searchDraft, setSearchDraft] = useState('');

    const { staff, isLoading: staffLoading } = useTenantStaff({ page: 1, pageSize: 200 });
    const { activities, total, isLoading, isFetching, refetch } = useActivityLog(filters);

    const actionOptions = useMemo(
        () =>
            ACTIVITY_LOG_ACTION_FILTER_VALUES.map((value) => {
                const labelKey = value ? getAuditActionLabelKey(value) : null;
                return {
                    value,
                    label: value
                        ? labelKey
                            ? t(labelKey as 'common.auditLogs.actionLabels.login')
                            : t(`activity.actions.${value}` as 'activity.actions.USER_LOGIN')
                        : t('activity.filters.allActions'),
                };
            }),
        [t],
    );

    const staffOptions = useMemo(
        () =>
            staff
                .filter((user) => Boolean(user.id))
                .map((user) => ({
                    value: user.id!,
                    label: staffDisplayName(user.firstName, user.lastName, user.userName),
                }))
                .sort((a, b) => a.label.localeCompare(b.label, formatLocale)),
        [formatLocale, staff],
    );

    const formatActionLabel = useCallback(
        (action: string) => resolveActivityActionLabel(action, t),
        [t],
    );

    const columns: ColumnsType<ActivityLogRow> = useMemo(
        () => [
            {
                title: t('activity.table.time'),
                dataIndex: 'timestamp',
                key: 'timestamp',
                width: 168,
                render: (value: string) => (value ? formatDateTime(value, formatLocale) : '—'),
            },
            {
                title: t('activity.table.user'),
                dataIndex: 'userName',
                key: 'userName',
                render: (name: string) => <Tag color="blue">{name}</Tag>,
            },
            {
                title: t('activity.table.action'),
                dataIndex: 'action',
                key: 'action',
                render: (action: string) => (
                    <Tag color={activityLogActionTagColor(action)}>{formatActionLabel(action)}</Tag>
                ),
            },
            {
                title: t('activity.table.description'),
                dataIndex: 'description',
                key: 'description',
                ellipsis: true,
            },
            {
                title: t('activity.table.details'),
                dataIndex: 'details',
                key: 'details',
                ellipsis: true,
                render: (details: ActivityLogRow['details']) => {
                    if (details == null) return '—';
                    const text = typeof details === 'string' ? details : JSON.stringify(details);
                    return (
                        <span style={{ fontSize: 12, color: '#94a3b8' }}>
                            {text.length > 80 ? `${text.slice(0, 80)}…` : text}
                        </span>
                    );
                },
            },
        ],
        [formatActionLabel, formatLocale, t],
    );

    const exportToCsv = useCallback(async () => {
        try {
            const [from, to] = filters.dateRange ?? [null, null];
            const query = buildAuditLogExportQuery({
                startDate: from?.format('YYYY-MM-DD'),
                endDate: to?.format('YYYY-MM-DD'),
                userId: filters.userId,
                action: filters.actionType || undefined,
                page: 1,
                pageSize: 1000,
            });
            await downloadAuditLogExport('csv', query, {
                exportFailedMessage: t('activity.exportFailed'),
            });
            message.success(t('activity.exportSuccess'));
        } catch {
            message.error(t('activity.exportFailed'));
        }
    }, [filters.actionType, filters.dateRange, filters.userId, message, t]);

    const onDateRangeChange = (dates: [Dayjs | null, Dayjs | null] | null) => {
        setFilters((prev) => ({
            ...prev,
            dateRange: dates,
            page: 1,
        }));
    };

    return (
        <Card
            title={t('activity.title')}
            extra={
                <Space>
                    <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching}>
                        {t('common.buttons.refresh')}
                    </Button>
                    <Button icon={<FileExcelOutlined />} onClick={exportToCsv}>
                        {t('activity.export')}
                    </Button>
                </Space>
            }
        >
            <Space wrap style={{ marginBottom: 16 }}>
                <RangePicker
                    value={filters.dateRange}
                    format={DAYJS_DATE_FORMAT}
                    onChange={onDateRangeChange}
                    allowClear
                />

                <Select
                    placeholder={t('activity.filters.userPlaceholder')}
                    style={{ minWidth: 180 }}
                    allowClear
                    showSearch
                    optionFilterProp="label"
                    loading={staffLoading}
                    value={filters.userId}
                    options={staffOptions}
                    onChange={(value) =>
                        setFilters((prev) => ({
                            ...prev,
                            userId: value,
                            page: 1,
                        }))
                    }
                />

                <Select
                    placeholder={t('activity.filters.actionTypePlaceholder')}
                    style={{ minWidth: 180 }}
                    allowClear
                    value={filters.actionType || undefined}
                    options={actionOptions}
                    onChange={(value) =>
                        setFilters((prev) => ({
                            ...prev,
                            actionType: value ?? '',
                            page: 1,
                        }))
                    }
                />

                <Input.Search
                    placeholder={t('activity.filters.search')}
                    allowClear
                    value={searchDraft}
                    onChange={(e) => setSearchDraft(e.target.value)}
                    onSearch={(value) => {
                        setSearchDraft(value);
                        setFilters((prev) => ({
                            ...prev,
                            search: value,
                            page: 1,
                        }));
                    }}
                    style={{ width: 220 }}
                />
            </Space>

            <Table<ActivityLogRow>
                columns={columns}
                dataSource={activities}
                loading={isLoading}
                rowKey="id"
                pagination={{
                    current: filters.page,
                    pageSize: filters.pageSize,
                    total,
                    showSizeChanger: true,
                    showTotal: (count) => t('activity.totalEntries', { count }),
                    onChange: (page, pageSize) =>
                        setFilters((prev) => ({
                            ...prev,
                            page,
                            pageSize,
                        })),
                }}
                locale={{ emptyText: t('activity.table.noData') }}
                scroll={{ x: 960 }}
            />
        </Card>
    );
}
