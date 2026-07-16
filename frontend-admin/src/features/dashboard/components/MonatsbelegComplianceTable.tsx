'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useCallback, useMemo, useState, type ReactNode } from 'react';
import Link from 'next/link';
import { Alert, Button, Card, Progress, Space, Table, Tag, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { RegisterMonatsbelegRow } from '@/features/dashboard/hooks/useAdminMonatsbelegOverview';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';
import type { MissingMonth } from '@/features/dashboard/api/monatsbelegStatus';
import { customInstance } from '@/lib/axios';
import { useQueryClient } from '@tanstack/react-query';
import { monatsbelegQueryKeys } from '@/features/rksv/hooks/useMonatsbeleg';
import { isDemoAutoSuggestMonatsbelegEnabled } from '@/shared/config/demoMonatsbelegFeature';
import { MonatsbelegPastMonthsAlert } from '@/features/rksv/components/MonatsbelegPastMonthsAlert';
import { PastMonthsMonatsbelegModal } from '@/features/rksv/components/PastMonthsMonatsbelegModal';
import {
    collectPastMissingMonatsbelege,
    countPastMissingMonatsbelege,
} from '@/features/rksv/utils/monatsbelegMissingMonths';
import {
    formatViennaYearMonth,
    getViennaCalendarYearMonth,
} from '@/shared/utils/viennaCalendar';
import { useI18n } from '@/i18n/I18nProvider';

const germanMonthYearFormatter = new Intl.DateTimeFormat('de-DE', {
    month: 'long',
    year: 'numeric',
});

function parseYearMonth(value: string | null | undefined): { year: number; month: number } | null {
    if (!value) return null;
    const match = /^(\d{4})-(\d{2})$/.exec(value.trim());
    if (!match) return null;
    const year = Number(match[1]);
    const month = Number(match[2]);
    if (!Number.isFinite(year) || !Number.isFinite(month) || month < 1 || month > 12) return null;
    return { year, month };
}

function formatMonthYear(year: number, month: number): string {
    const date = new Date(year, month - 1, 1);
    return germanMonthYearFormatter.format(date);
}

function formatLastCompletedLabel(
    value: string | null | undefined,
    noMonatsbelegLabel: string,
): string {
    const parsed = parseYearMonth(value);
    if (!parsed) return noMonatsbelegLabel;
    return formatMonthYear(parsed.year, parsed.month);
}

function formatMissingMonthsLabel(
    missingMonths: MissingMonth[] | undefined,
    noMissingLabel: string,
    overdueSuffix: string,
): string {
    if (!missingMonths || missingMonths.length === 0) {
        return noMissingLabel;
    }

    return missingMonths
        .map((missingMonth) => {
            const monthLabel = formatMonthYear(missingMonth.year, missingMonth.month);
            return missingMonth.isOverdue ? `${monthLabel}${overdueSuffix}` : monthLabel;
        })
        .join(', ');
}

function getYearlyProgress(missingMonths: MissingMonth[] | undefined): { completed: number; total: number; percent: number } {
    const now = new Date();
    const currentYear = now.getFullYear();
    const lastRequiredMonthInYear = Math.max(0, Math.min(12, now.getMonth()));
    const total = lastRequiredMonthInYear;
    if (total === 0) {
        return { completed: 0, total: 0, percent: 100 };
    }

    const missingCount = (missingMonths ?? []).filter(
        (m) => m.year === currentYear && m.month >= 1 && m.month <= lastRequiredMonthInYear,
    ).length;
    const completed = Math.max(0, total - missingCount);
    const percent = Math.round((completed / total) * 100);

    return { completed, total, percent };
}

export type MonatsbelegComplianceTableProps = {
    rows: RegisterMonatsbelegRow[];
    loading: boolean;
    loadError?: boolean;
    hasRegisters?: boolean;
    headerExtra?: ReactNode;
    onRefresh?: () => void;
    refreshLoading?: boolean;
};

export function MonatsbelegComplianceTable({
    rows,
    loading,
    loadError = false,
    hasRegisters = true,
    headerExtra,
    onRefresh,
    refreshLoading = false,
}: MonatsbelegComplianceTableProps) {
  const { message } = useAntdApp();
    const { t } = useI18n();

    const { hasPermission } = usePermissions();
    const queryClient = useQueryClient();
    const canMonatsbeleg = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);
    const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);
    const demoAutoSuggestEnabled = isDemoAutoSuggestMonatsbelegEnabled();
    const [pastMonthsModalOpen, setPastMonthsModalOpen] = useState(false);

    const overviewItems = useMemo(
        () => rows.map((row) => ({ cashRegisterId: row.registerId, status: row.status })),
        [rows],
    );
    const otherMissingCount = useMemo(() => countPastMissingMonatsbelege(overviewItems), [overviewItems]);
    const pastMissingEntries = useMemo(() => collectPastMissingMonatsbelege(overviewItems), [overviewItems]);

    const createCurrentMonthMonatsbelegForTest = useCallback(async (record: RegisterMonatsbelegRow) => {
        const { year: viennaYear, month: viennaMonth } = getViennaCalendarYearMonth();
        const month = viennaMonth === 1 ? 12 : viennaMonth - 1;
        const year = viennaMonth === 1 ? viennaYear - 1 : viennaYear;

        try {
            await customInstance({
                url: '/api/rksv/special-receipts/monatsbeleg?force=true',
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                data: {
                    cashRegisterId: record.registerId,
                    year,
                    month,
                    reason: 'Demo Monatsbeleg Vormonat',
                },
            });
            await queryClient.invalidateQueries({ queryKey: monatsbelegQueryKeys.statusOverview });
            message.success(
                t('dashboard.monatsbeleg.createSuccess', {
                    month: formatViennaYearMonth(year, month),
                })
            );
        } catch {
            message.error(t('dashboard.monatsbeleg.createFailed'));
        }
    }, [message, queryClient, t]);

    const columns: ColumnsType<RegisterMonatsbelegRow> = useMemo(
        () => [
            {
                title: t('dashboard.monatsbeleg.colRegister'),
                key: 'register',
                render: (_, record) => (
                    <Space orientation="vertical" size={0}>
                        <Typography.Text strong>
                            {record.register.location?.trim() || '—'}
                        </Typography.Text>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            Nr. {record.register.registerNumber}
                        </Typography.Text>
                    </Space>
                ),
            },
            {
                title: t('dashboard.monatsbeleg.colLastMonatsbeleg'),
                key: 'last',
                width: 220,
                render: (_, record) => {
                    if (record.statusLoading) return <Typography.Text type="secondary">…</Typography.Text>;
                    if (record.statusError) return <Typography.Text type="warning">—</Typography.Text>;
                    return (
                        <Typography.Text>
                            {formatLastCompletedLabel(
                                record.status?.lastCompletedMonth,
                                t('dashboard.monatsbeleg.noMonatsbeleg'),
                            )}
                        </Typography.Text>
                    );
                },
            },
            {
                title: t('dashboard.monatsbeleg.colMissingMonths'),
                key: 'missing-months',
                width: 360,
                render: (_, record) => {
                    if (record.statusLoading) return <Tag color="default">{t('dashboard.monatsbeleg.loadingTag')}</Tag>;
                    if (record.statusError) return <Tag color="default">{t('dashboard.monatsbeleg.statusFailed')}</Tag>;
                    return (
                        <Typography.Text>
                            {formatMissingMonthsLabel(
                                record.status?.missingMonths ?? undefined,
                                t('dashboard.monatsbeleg.noMissingMonths'),
                                t('dashboard.monatsbeleg.overdueSuffix'),
                            )}
                        </Typography.Text>
                    );
                },
            },
            {
                title: t('dashboard.monatsbeleg.colProgress'),
                key: 'progress',
                width: 260,
                render: (_, record) => {
                    if (record.statusLoading) return <Typography.Text type="secondary">…</Typography.Text>;
                    if (record.statusError) return <Typography.Text type="warning">—</Typography.Text>;

                    const progress = getYearlyProgress(record.status?.missingMonths ?? undefined);
                    return (
                        <Space orientation="vertical" size={4} style={{ width: '100%' }}>
                            <Progress percent={progress.percent} size="small" />
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                {t('dashboard.monatsbeleg.progressSummary', {
                                    completed: progress.completed,
                                    total: progress.total,
                                })}
                            </Typography.Text>
                        </Space>
                    );
                },
            },
            {
                title: t('dashboard.monatsbeleg.colAction'),
                key: 'action',
                width: 320,
                render: (_, record) => {
                    if (!canMonatsbeleg || !canOpenSonderbelege) {
                        return (
                            <Typography.Text type="secondary">
                                {t('dashboard.monatsbeleg.noPermission')}
                            </Typography.Text>
                        );
                    }

                    const { year: viennaYear, month: viennaMonth } = getViennaCalendarYearMonth();
                    const currentMonthLabel = formatMonthYear(viennaYear, viennaMonth);
                    const currentMonthMissing = record.status?.currentMonthExists === false;
                    return (
                        <Space orientation="vertical" size={8}>
                            <Link
                                href={`${RKSV_SONDERBELEGE_PATH}?registerId=${encodeURIComponent(record.registerId)}&kind=monatsbeleg`}
                            >
                                <Button type="primary" size="small" disabled={!currentMonthMissing}>
                                    {t('dashboard.monatsbeleg.createForMonth', { month: currentMonthLabel })}
                                </Button>
                            </Link>
                            {demoAutoSuggestEnabled ? (
                                <Button
                                    size="small"
                                    onClick={() => void createCurrentMonthMonatsbelegForTest(record)}
                                    disabled={!currentMonthMissing}
                                >
                                    {t('dashboard.monatsbeleg.createTestCurrentMonth')}
                                </Button>
                            ) : null}
                        </Space>
                    );
                },
            },
        ],
        [canMonatsbeleg, canOpenSonderbelege, createCurrentMonthMonatsbelegForTest, demoAutoSuggestEnabled, t],
    );

    return (
        <Card
            title={t('dashboard.monatsbeleg.title')}
            variant="borderless"
            style={{ marginBottom: 24 }}
            extra={
                <Space size="small">
                    {headerExtra}
                    {onRefresh ? (
                        <Button
                            icon={<ReloadOutlined />}
                            size="small"
                            loading={refreshLoading}
                            onClick={onRefresh}
                            aria-label={t('dashboard.monatsbeleg.refreshA11y')}
                        />
                    ) : null}
                </Space>
            }
        >
            <MonatsbelegPastMonthsAlert
                otherMissingCount={otherMissingCount}
                canCreate={canMonatsbeleg}
                onManagePastMonths={() => setPastMonthsModalOpen(true)}
            />

            {demoAutoSuggestEnabled ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 12 }}
                    title={t('dashboard.monatsbeleg.demoModeTitle')}
                />
            ) : null}
            <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                {t('dashboard.monatsbeleg.intro')}
            </Typography.Paragraph>
            {loadError ? (
                <Alert
                    type="error"
                    showIcon
                    style={{ marginBottom: 12 }}
                    title={t('dashboard.monatsbeleg.statusLoadFailed')}
                />
            ) : null}
            {!loading && !loadError && !hasRegisters ? (
                <Typography.Text type="secondary">{t('dashboard.monatsbeleg.noRegisters')}</Typography.Text>
            ) : null}
            <Table<RegisterMonatsbelegRow>
                rowKey={(r) => r.registerId}
                loading={loading}
                pagination={false}
                columns={columns}
                dataSource={rows}
                locale={{ emptyText: t('dashboard.monatsbeleg.emptyTable') }}
            />

            <PastMonthsMonatsbelegModal
                open={pastMonthsModalOpen}
                entries={pastMissingEntries}
                onClose={() => setPastMonthsModalOpen(false)}
                onCreated={() => {
                    if (onRefresh) void onRefresh();
                }}
            />
        </Card>
    );
}
