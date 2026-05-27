'use client';

/**
 * Compliance user activity report — filters, summary, charts, timeline, export, schedule.
 */
import React, { useCallback, useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Col,
    DatePicker,
    Form,
    Input,
    Modal,
    Row,
    Select,
    Space,
    Statistic,
    Table,
    Tag,
    message,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { DownloadOutlined, MailOutlined, SearchOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';

import {
    buildUserActivityExportUrl,
    exportUserActivityReportBlob,
    fetchUserActivityReport,
    scheduleUserActivityReport,
    type UserActivityReport,
} from '@/features/reports/api/userActivityReport';
import { UserActivityChart } from '@/features/reports/components/UserActivityChart';
import { userActivityReportCopy as copy, ACTION_TYPE_OPTIONS } from '@/features/reports/constants/copy';
import { listTenantUsers, type TenantUserRowDto } from '@/features/users/api/users';
import { getAuditActionLabelKey } from '@/features/audit-logs/utils/auditActionLabels';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

type FilterState = {
    userId: string;
    dateRange: [Dayjs, Dayjs];
    actionType: string;
};

const defaultRange: [Dayjs, Dayjs] = [dayjs().subtract(30, 'day'), dayjs()];

type Props = {
    initialUserId?: string;
};

export function UserActivityReport({ initialUserId }: Props) {
    const { t, formatLocale } = useI18n();
    const [filters, setFilters] = useState<FilterState | null>(
        initialUserId
            ? { userId: initialUserId, dateRange: defaultRange, actionType: '' }
            : null,
    );
    const [userSearch, setUserSearch] = useState('');
    const [scheduleOpen, setScheduleOpen] = useState(false);
    const [scheduleForm] = Form.useForm();

    const { data: userRows = [], isLoading: usersLoading } = useQuery({
        queryKey: ['tenant-users-picker', userSearch],
        queryFn: () => listTenantUsers(userSearch.trim() ? { search: userSearch.trim() } : undefined),
        staleTime: 60_000,
    });

    const userOptions = useMemo(
        () =>
            userRows.map((u: TenantUserRowDto) => ({
                value: u.userId,
                label: `${u.name} (${u.userName}) — ${u.role}`,
            })),
        [userRows],
    );

    const reportParams = useMemo(() => {
        if (!filters?.userId) return null;
        return {
            userId: filters.userId,
            fromDate: filters.dateRange[0].format('YYYY-MM-DD'),
            toDate: filters.dateRange[1].format('YYYY-MM-DD'),
            actionType: filters.actionType || undefined,
            includeTimeline: true,
            includeTopUsers: true,
        };
    }, [filters]);

    const {
        data: report,
        isLoading,
        isError,
        refetch,
        isFetching,
    } = useQuery({
        queryKey: ['user-activity-report', reportParams],
        queryFn: () => fetchUserActivityReport(reportParams!),
        enabled: !!reportParams,
        staleTime: 30_000,
    });

    const handleGenerate = (values: {
        userId: string;
        dateRange: [Dayjs, Dayjs];
        actionType?: string;
    }) => {
        setFilters({
            userId: values.userId,
            dateRange: values.dateRange,
            actionType: values.actionType ?? '',
        });
    };

    const handleExport = useCallback(
        async (format: 'csv' | 'pdf') => {
            if (!reportParams) return;
            try {
                const blob = await exportUserActivityReportBlob({ ...reportParams, format });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `user-activity-${report?.userName ?? filters?.userId}.${format === 'pdf' ? 'pdf' : 'csv'}`;
                a.click();
                URL.revokeObjectURL(url);
            } catch {
                message.error(copy.loadError);
            }
        },
        [reportParams, report?.userName, filters?.userId],
    );

    const handleSchedule = async () => {
        if (!reportParams) return;
        try {
            const values = await scheduleForm.validateFields();
            const recipients = String(values.recipients)
                .split(',')
                .map((e: string) => e.trim())
                .filter(Boolean);
            await scheduleUserActivityReport({
                userId: reportParams.userId,
                name: values.name,
                schedule: values.schedule,
                recipients,
                format: 'csv',
                fromDate: reportParams.fromDate,
                toDate: reportParams.toDate,
                actionType: reportParams.actionType,
            });
            message.success(copy.scheduleSuccess);
            setScheduleOpen(false);
        } catch {
            /* validation */
        }
    };

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <Card size="small">
                <Form
                    layout="vertical"
                    initialValues={{
                        userId: initialUserId,
                        dateRange: defaultRange,
                        actionType: '',
                    }}
                    onFinish={handleGenerate}
                >
                    <Row gutter={16}>
                        <Col xs={24} md={10}>
                            <Form.Item
                                name="userId"
                                label={copy.selectUser}
                                rules={[{ required: true, message: copy.selectUser }]}
                            >
                                <Select
                                    showSearch
                                    placeholder={copy.selectUserPlaceholder}
                                    options={userOptions}
                                    loading={usersLoading}
                                    filterOption={false}
                                    onSearch={setUserSearch}
                                    notFoundContent={usersLoading ? null : undefined}
                                />
                            </Form.Item>
                        </Col>
                        <Col xs={24} md={8}>
                            <Form.Item
                                name="dateRange"
                                label={copy.dateRange}
                                rules={[{ required: true }]}
                            >
                                <DatePicker.RangePicker format="DD.MM.YYYY" style={{ width: '100%' }} />
                            </Form.Item>
                        </Col>
                        <Col xs={24} md={6}>
                            <Form.Item name="actionType" label={copy.actionType}>
                                <Select options={ACTION_TYPE_OPTIONS} />
                            </Form.Item>
                        </Col>
                    </Row>
                    <Space wrap>
                        <Button type="primary" htmlType="submit" icon={<SearchOutlined />}>
                            {copy.generate}
                        </Button>
                        <Button
                            icon={<DownloadOutlined />}
                            disabled={!report}
                            onClick={() => handleExport('csv')}
                        >
                            {copy.exportCsv}
                        </Button>
                        <Button disabled={!report} onClick={() => handleExport('pdf')}>
                            {copy.exportPdf}
                        </Button>
                        <Button
                            icon={<MailOutlined />}
                            disabled={!reportParams}
                            onClick={() => {
                                scheduleForm.setFieldsValue({
                                    name: `User activity — ${report?.userName ?? ''}`,
                                    schedule: 'weekly',
                                });
                                setScheduleOpen(true);
                            }}
                        >
                            {copy.scheduleReport}
                        </Button>
                    </Space>
                </Form>
            </Card>

            {!filters && <Alert type="info" message={copy.noData} showIcon />}

            {isError && (
                <Alert
                    type="error"
                    message={copy.loadError}
                    action={<Button size="small" onClick={() => refetch()}>{t('common.buttons.retry')}</Button>}
                />
            )}

            {report && (
                <>
                    <Card size="small" loading={isLoading || isFetching}>
                        <Row gutter={[16, 16]}>
                            <Col xs={12} sm={8} md={4}>
                                <Statistic title={copy.totalActions} value={report.totalActions} />
                            </Col>
                            <Col xs={12} sm={8} md={4}>
                                <Statistic title={copy.totalLogins} value={report.totalLogins} />
                            </Col>
                            <Col xs={12} sm={8} md={4}>
                                <Statistic
                                    title={copy.failedLogins}
                                    value={report.failedLoginAttempts}
                                    valueStyle={
                                        report.failedLoginAttempts > 0 ? { color: '#cf1322' } : undefined
                                    }
                                />
                            </Col>
                            <Col xs={12} sm={8} md={4}>
                                <Statistic title={copy.activeSessions} value={report.activeSessions} />
                            </Col>
                            <Col xs={12} sm={8} md={4}>
                                <Statistic
                                    title={copy.avgSession}
                                    value={report.averageSessionDurationMinutes}
                                    suffix="min"
                                />
                            </Col>
                            <Col xs={24} sm={12} md={8}>
                                <Statistic
                                    title={copy.lastLogin}
                                    value={
                                        report.lastLoginAt
                                            ? formatDateTime(report.lastLoginAt, formatLocale)
                                            : '—'
                                    }
                                    valueStyle={{ fontSize: 14 }}
                                />
                            </Col>
                        </Row>
                        <div style={{ marginTop: 8 }}>
                            <Tag>{report.userName}</Tag>
                            <Tag>{report.email}</Tag>
                            <Tag color="gold">{report.role}</Tag>
                            {report.tenantName && <Tag>{report.tenantName}</Tag>}
                        </div>
                    </Card>

                    <UserActivityChart
                        dailyActivity={report.dailyActivity}
                        actionsPerformed={report.actionsPerformed}
                    />

                    {report.topActiveUsers.length > 0 && (
                        <Card size="small" title={copy.topActiveUsers}>
                            <Table
                                size="small"
                                pagination={false}
                                rowKey="userId"
                                dataSource={report.topActiveUsers}
                                columns={[
                                    { title: copy.selectUser, dataIndex: 'userName', key: 'userName' },
                                    { title: 'Rolle', dataIndex: 'role', key: 'role' },
                                    {
                                        title: copy.totalActions,
                                        dataIndex: 'actionCount',
                                        key: 'actionCount',
                                    },
                                ]}
                            />
                        </Card>
                    )}

                    <Card size="small" title={copy.timeline}>
                        <TimelineTable report={report} formatLocale={formatLocale} t={t} />
                    </Card>
                </>
            )}

            <Modal
                title={copy.scheduleTitle}
                open={scheduleOpen}
                onCancel={() => setScheduleOpen(false)}
                onOk={handleSchedule}
                okText={copy.scheduleReport}
            >
                <Form form={scheduleForm} layout="vertical">
                    <Form.Item name="name" label={copy.scheduleName} rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item name="schedule" label="Intervall" rules={[{ required: true }]}>
                        <Select
                            options={[
                                { value: 'weekly', label: copy.scheduleWeekly },
                                { value: 'monthly', label: copy.scheduleMonthly },
                            ]}
                        />
                    </Form.Item>
                    <Form.Item
                        name="recipients"
                        label={copy.scheduleRecipients}
                        rules={[{ required: true }]}
                    >
                        <Input.TextArea rows={2} placeholder="audit@firma.at, buchhaltung@firma.at" />
                    </Form.Item>
                </Form>
            </Modal>
        </Space>
    );
}

function TimelineTable({
    report,
    formatLocale,
    t,
}: {
    report: UserActivityReport;
    formatLocale: string;
    t: (key: string) => string;
}) {
    const columns = [
        {
            title: 'Zeit',
            dataIndex: 'date',
            key: 'date',
            width: 170,
            render: (v: string) => formatDateTime(v, formatLocale),
        },
        {
            title: 'Aktion',
            dataIndex: 'action',
            key: 'action',
            render: (action: string) => {
                const key = getAuditActionLabelKey(action);
                const label = key ? t(key) : action;
                return <Tag color="blue">{label}</Tag>;
            },
        },
        { title: 'Entität', dataIndex: 'entityType', key: 'entityType', width: 100 },
        { title: 'Status', dataIndex: 'status', key: 'status', width: 90 },
        { title: 'IP', dataIndex: 'ipAddress', key: 'ip', width: 110 },
        { title: 'Session', dataIndex: 'sessionId', key: 'session', ellipsis: true },
        { title: 'Correlation', dataIndex: 'correlationId', key: 'corr', ellipsis: true },
        {
            title: 'TSE',
            dataIndex: 'tseSignature',
            key: 'tse',
            ellipsis: true,
            render: (v: string | null | undefined) => (v?.trim() ? '✓' : '—'),
        },
    ];

    return (
        <Table
            size="small"
            rowKey={(r, i) => `${r.date}-${r.action}-${i}`}
            columns={columns}
            dataSource={report.activityTimeline}
            pagination={{ pageSize: 15 }}
            scroll={{ x: 900 }}
        />
    );
}
