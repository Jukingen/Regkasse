'use client';

import { useCallback, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
    Alert,
    Button,
    Col,
    Descriptions,
    List,
    Modal,
    Row,
    Space,
    Spin,
    Tag,
    Typography,
} from 'antd';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { copyTextToClipboard } from '@/lib/clipboard';
import { formatEUR } from '@/shared/utils/currency';
import {
    MonthCard,
    monthStatusTagColor,
    type MonthCardAction,
    type MonthCardActionPayload,
    type MonthCardStatus,
} from '@/features/rksv/components/MonthCard';
import { useReceiptsByMonth } from '@/features/rksv/hooks/useReceiptsByMonth';
import {
    buildMonatsbelegMonthDeepLink,
    buildMonthPaymentsHref,
    buildMonthReceiptsHref,
} from '@/features/rksv/utils/monatsbelegMonthLinks';
import { rksvSpecialReceiptKindLabelDe } from '@/features/rksv-operations/rksvSpecialReceiptDisplay';

export type MonatsbelegTimelineMonth = {
    month: number;
    status: MonthCardStatus;
    receiptId?: string;
};

export type MonatsbelegTimelineProps = {
    year: number;
    months: MonatsbelegTimelineMonth[];
    cashRegisterId?: string;
    canRecreate?: boolean;
    /** Opens parent CreateMonatsbelegModal for late / recreate flows. */
    onCreateLate: (year: number, month: number) => void;
};

type SelectedMonth = {
    year: number;
    month: number;
    status: MonthCardStatus;
};

function formatPeriodLabel(year: number, month: number, locale: string): string {
    return new Intl.DateTimeFormat(locale, {
        month: 'long',
        year: 'numeric',
        timeZone: 'Europe/Vienna',
    }).format(new Date(Date.UTC(year, month - 1, 1)));
}

export function MonatsbelegTimeline({
    year,
    months,
    cashRegisterId,
    canRecreate = false,
    onCreateLate,
}: MonatsbelegTimelineProps) {
    const { t, textLocale } = useI18n();
    const { message, modal } = useAntdApp();
    const router = useRouter();

    const [summaryMonth, setSummaryMonth] = useState<SelectedMonth | null>(null);
    const [summaryOpen, setSummaryOpen] = useState(false);
    const [receiptsMonth, setReceiptsMonth] = useState<{ year: number; month: number } | null>(
        null,
    );
    const [receiptsModalOpen, setReceiptsModalOpen] = useState(false);

    // Prefer explicit receipts selection when that modal is open; otherwise summary month.
    const activeMonthParams = useMemo(() => {
        if (!cashRegisterId?.trim()) return null;
        if (receiptsModalOpen && receiptsMonth) {
            return {
                cashRegisterId,
                year: receiptsMonth.year,
                month: receiptsMonth.month,
            };
        }
        if (summaryOpen && summaryMonth) {
            return {
                cashRegisterId,
                year: summaryMonth.year,
                month: summaryMonth.month,
            };
        }
        return null;
    }, [cashRegisterId, receiptsModalOpen, receiptsMonth, summaryOpen, summaryMonth]);

    const receiptsQuery = useReceiptsByMonth(activeMonthParams);

    const receiptIdByMonth = useMemo(() => {
        const map = new Map<number, string>();
        for (const row of months) {
            if (row.receiptId) map.set(row.month, row.receiptId);
        }
        return map;
    }, [months]);

    const statusLabel = useCallback(
        (status: MonthCardStatus) => {
            switch (status) {
                case 'missing':
                    return t('rksvHub.monatsbelegTimeline.statusMissing');
                case 'completed':
                    return t('rksvHub.monatsbelegTimeline.statusCompleted');
                case 'pending':
                default:
                    return t('rksvHub.monatsbelegTimeline.statusPending');
            }
        },
        [t],
    );

    const closeSummary = useCallback(() => {
        setSummaryOpen(false);
        setSummaryMonth(null);
    }, []);

    const closeReceiptsModal = useCallback(() => {
        setReceiptsModalOpen(false);
        setReceiptsMonth(null);
    }, []);

    const handleAction = useCallback(
        (action: MonthCardAction, data: MonthCardActionPayload) => {
            const { month, year: actionYear } = data;

            if (action === 'copy-link') {
                const origin =
                    typeof globalThis.window !== 'undefined' ? globalThis.window.location.origin : '';
                const link = buildMonatsbelegMonthDeepLink({
                    origin,
                    registerId: cashRegisterId,
                    year: actionYear,
                    month,
                });
                void copyTextToClipboard(link).then((ok) => {
                    if (ok) message.success(t('rksvHub.monatsbelegTimeline.copyLinkSuccess'));
                    else message.error(t('rksvHub.monatsbelegTimeline.copyLinkFailed'));
                });
                return;
            }

            if (!cashRegisterId?.trim()) {
                message.warning(t('rksvHub.monatsbelegTimeline.needRegister'));
                return;
            }

            if (action === 'view-receipts') {
                setSummaryOpen(false);
                setReceiptsMonth({ year: actionYear, month });
                setReceiptsModalOpen(true);
                return;
            }

            if (action === 'view-revenue') {
                setSummaryOpen(false);
                router.push(buildMonthPaymentsHref(cashRegisterId, actionYear, month));
                return;
            }

            if (action === 'view-report') {
                const receiptId = receiptIdByMonth.get(month);
                if (!receiptId) {
                    message.warning(t('rksvHub.monatsbelegTimeline.viewReportMissing'));
                    return;
                }
                setSummaryOpen(false);
                router.push(`/receipts/${receiptId}`);
                return;
            }

            if (action === 'create-late') {
                setSummaryOpen(false);
                onCreateLate(actionYear, month);
                return;
            }

            if (action === 'recreate') {
                if (!canRecreate) {
                    message.warning(t('rksvHub.monatsbelegTimeline.recreateDenied'));
                    return;
                }
                modal.confirm({
                    title: t('rksvHub.monatsbelegTimeline.recreateConfirmTitle'),
                    content: t('rksvHub.monatsbelegTimeline.recreateConfirmBody', {
                        period: formatPeriodLabel(actionYear, month, textLocale),
                    }),
                    okText: t('rksvHub.monatsbelegTimeline.recreateConfirmOk'),
                    cancelText: t('rksvHub.monatsbelegTimeline.recreateConfirmCancel'),
                    okButtonProps: { danger: true },
                    onOk: () => {
                        setSummaryOpen(false);
                        onCreateLate(actionYear, month);
                    },
                });
            }
        },
        [
            canRecreate,
            cashRegisterId,
            message,
            modal,
            onCreateLate,
            receiptIdByMonth,
            router,
            t,
            textLocale,
        ],
    );

    const openSummary = useCallback((data: MonthCardActionPayload) => {
        setSummaryMonth({ year: data.year, month: data.month, status: data.status });
        setSummaryOpen(true);
    }, []);

    const receiptsListHref =
        cashRegisterId && receiptsMonth
            ? buildMonthReceiptsHref(cashRegisterId, receiptsMonth.year, receiptsMonth.month)
            : null;

    const summaryPayload: MonthCardActionPayload | null = summaryMonth
        ? {
              month: summaryMonth.month,
              year: summaryMonth.year,
              status: summaryMonth.status,
          }
        : null;

    const transactionCount = receiptsQuery.data?.totalCount ?? 0;
    const revenue = receiptsQuery.data?.revenue ?? 0;
    const revenueIncomplete = receiptsQuery.data ? !receiptsQuery.data.revenueIsComplete : false;

    return (
        <>
            <Row gutter={[12, 12]}>
                {months.map((item) => (
                    <Col xs={12} sm={8} md={6} lg={4} xl={3} key={`timeline-${item.month}`}>
                        <MonthCard
                            month={item.month}
                            year={year}
                            status={item.status}
                            canRecreate={canRecreate}
                            onAction={handleAction}
                            onOpenSummary={openSummary}
                        />
                    </Col>
                ))}
            </Row>

            <Modal
                title={
                    summaryMonth
                        ? t('rksvHub.monatsbelegTimeline.summaryTitle', {
                              period: formatPeriodLabel(
                                  summaryMonth.year,
                                  summaryMonth.month,
                                  textLocale,
                              ),
                          })
                        : t('rksvHub.monatsbelegTimeline.summaryTitleFallback')
                }
                open={summaryOpen}
                onCancel={closeSummary}
                destroyOnHidden
                footer={null}
            >
                {summaryMonth && summaryPayload ? (
                    <Spin spinning={receiptsQuery.isLoading || receiptsQuery.isFetching}>
                        <Descriptions bordered size="small" column={1}>
                            <Descriptions.Item label={t('rksvHub.monatsbelegTimeline.summaryStatus')}>
                                <Tag color={monthStatusTagColor(summaryMonth.status)}>
                                    {statusLabel(summaryMonth.status)}
                                </Tag>
                            </Descriptions.Item>
                            <Descriptions.Item label={t('rksvHub.monatsbelegTimeline.summaryRevenue')}>
                                {receiptsQuery.isError
                                    ? '—'
                                    : formatEUR(revenue)}
                                {revenueIncomplete ? (
                                    <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                                        {t('rksvHub.monatsbelegTimeline.summaryRevenuePartial')}
                                    </Typography.Text>
                                ) : null}
                            </Descriptions.Item>
                            <Descriptions.Item
                                label={t('rksvHub.monatsbelegTimeline.summaryTransactions')}
                            >
                                {receiptsQuery.isError ? '—' : transactionCount}
                            </Descriptions.Item>
                        </Descriptions>

                        {receiptsQuery.isError ? (
                            <Alert
                                style={{ marginTop: 16 }}
                                type="warning"
                                showIcon
                                title={t('rksvHub.monatsbelegTimeline.summaryLoadWarning')}
                            />
                        ) : null}

                        <Space wrap style={{ marginTop: 16 }}>
                            {summaryMonth.status === 'missing' ? (
                                <Button
                                    type="primary"
                                    onClick={() => handleAction('create-late', summaryPayload)}
                                >
                                    {t('rksvHub.monatsbelegTimeline.summaryCreate')}
                                </Button>
                            ) : null}
                            {summaryMonth.status === 'completed' ? (
                                <Button
                                    type="primary"
                                    onClick={() => handleAction('view-report', summaryPayload)}
                                >
                                    {t('rksvHub.monatsbelegTimeline.summaryViewReport')}
                                </Button>
                            ) : null}
                            <Button onClick={() => handleAction('view-receipts', summaryPayload)}>
                                {t('rksvHub.monatsbelegTimeline.summaryViewReceipts')}
                            </Button>
                            <Button onClick={() => handleAction('view-revenue', summaryPayload)}>
                                {t('rksvHub.monatsbelegTimeline.summaryViewRevenue')}
                            </Button>
                            {summaryMonth.status === 'completed' && canRecreate ? (
                                <Button
                                    danger
                                    onClick={() => handleAction('recreate', summaryPayload)}
                                >
                                    {t('rksvHub.monatsbelegTimeline.summaryRecreate')}
                                </Button>
                            ) : null}
                        </Space>
                    </Spin>
                ) : null}
            </Modal>

            <Modal
                title={
                    receiptsMonth
                        ? t('rksvHub.monatsbelegTimeline.receiptsModalTitle', {
                              period: formatPeriodLabel(
                                  receiptsMonth.year,
                                  receiptsMonth.month,
                                  textLocale,
                              ),
                          })
                        : t('rksvHub.monatsbelegTimeline.receiptsModalTitleFallback')
                }
                open={receiptsModalOpen}
                onCancel={closeReceiptsModal}
                width={800}
                destroyOnHidden
                footer={
                    <Space>
                        {receiptsListHref ? (
                            <Link href={receiptsListHref}>
                                <Button type="primary">
                                    {t('rksvHub.monatsbelegTimeline.receiptsModalOpenFull')}
                                </Button>
                            </Link>
                        ) : null}
                        <Button onClick={closeReceiptsModal}>
                            {t('rksvHub.monatsbelegTimeline.receiptsModalClose')}
                        </Button>
                    </Space>
                }
            >
                {receiptsQuery.isError ? (
                    <Alert
                        type="error"
                        showIcon
                        title={t('rksvHub.monatsbelegTimeline.receiptsModalLoadError')}
                    />
                ) : (
                    <Spin spinning={receiptsQuery.isLoading || receiptsQuery.isFetching}>
                        <List
                            locale={{
                                emptyText: t('rksvHub.monatsbelegTimeline.receiptsModalEmpty'),
                            }}
                            dataSource={receiptsQuery.data?.items ?? []}
                            renderItem={(receipt) => {
                                const id = receipt.receiptId?.trim();
                                const number = receipt.receiptNumber?.trim() || '—';
                                const kind = receipt.rksvSpecialReceiptKind?.trim();
                                return (
                                    <List.Item
                                        actions={
                                            id
                                                ? [
                                                      <Link key="open" href={`/receipts/${id}`}>
                                                          {t(
                                                              'rksvHub.monatsbelegTimeline.receiptsModalOpenOne',
                                                          )}
                                                      </Link>,
                                                  ]
                                                : undefined
                                        }
                                    >
                                        <List.Item.Meta
                                            title={
                                                id ? (
                                                    <Link href={`/receipts/${id}`}>{number}</Link>
                                                ) : (
                                                    number
                                                )
                                            }
                                            description={
                                                <Space orientation="vertical" size={0}>
                                                    <Typography.Text type="secondary">
                                                        {receipt.issuedAt
                                                            ? formatDateTime(receipt.issuedAt, '')
                                                            : '—'}
                                                        {kind
                                                            ? ` · ${rksvSpecialReceiptKindLabelDe(kind)}`
                                                            : ''}
                                                    </Typography.Text>
                                                    <Typography.Text>
                                                        {formatEUR(receipt.grandTotal ?? 0)}
                                                    </Typography.Text>
                                                </Space>
                                            }
                                        />
                                    </List.Item>
                                );
                            }}
                        />
                    </Spin>
                )}
            </Modal>
        </>
    );
}
