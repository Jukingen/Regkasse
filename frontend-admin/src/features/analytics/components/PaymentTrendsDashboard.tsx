'use client';

import { useMemo, useState } from 'react';
import {
    ArrowDownOutlined,
    ArrowUpOutlined,
    DollarOutlined,
    MinusOutlined,
    ShoppingOutlined,
} from '@ant-design/icons';
import { Card, Col, DatePicker, Row, Select, Space, Statistic, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import {
    Area,
    Bar,
    BarChart,
    CartesianGrid,
    Cell,
    ComposedChart,
    Legend,
    Line,
    Pie,
    PieChart,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from 'recharts';

import styles from '@/features/analytics/components/paymentTrendsDashboard.module.css';
import { usePaymentTrends } from '@/features/analytics/hooks/usePaymentTrends';
import type { TrendPeriod } from '@/features/payments/types/paymentTrends';
import { PageSkeleton } from '@/components/Skeleton';
import { useI18n } from '@/i18n/I18nProvider';
import { formatUserMonthDay, formatUserMonthYear, DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

const { RangePicker } = DatePicker;

const PIE_COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884d8', '#82ca9d'];

const PERIOD_OPTIONS: TrendPeriod[] = ['Daily', 'Weekly', 'Monthly'];

type MethodPieRow = {
    method: string;
    amount: number;
    percentage: number;
};

/** Full-page payment trend analytics with charts and period comparison. */
export function PaymentTrendsDashboard() {
    const { t } = useI18n();
    const [period, setPeriod] = useState<TrendPeriod>('Daily');
    const [dateRange, setDateRange] = useState<[Dayjs, Dayjs] | null>(null);

    const { data: trends, isLoading, isError } = usePaymentTrends(period, dateRange);

    const chartData = useMemo(
        () =>
            (trends?.trendData ?? []).map((point) => ({
                ...point,
                xLabel:
                    point.label ??
                    (period === 'Monthly'
                        ? formatUserMonthYear(point.date)
                        : formatUserMonthDay(point.date)),
            })),
        [period, trends?.trendData],
    );

    const methodPieData = useMemo((): MethodPieRow[] => {
        const rows = trends?.comparison.paymentMethodComparison ?? [];
        const total = rows.reduce((sum, row) => sum + row.currentAmount, 0);
        return rows.map((row) => ({
            method: row.method,
            amount: row.currentAmount,
            percentage: total > 0 ? (row.currentAmount / total) * 100 : 0,
        }));
    }, [trends?.comparison.paymentMethodComparison]);

    const growth = trends?.comparison.growthPercentage ?? 0;
    const growthPositive = growth > 0;
    const growthNegative = growth < 0;

    if (isLoading && !trends) {
        return <PageSkeleton widgets={6} />;
    }

    if (isError) {
        return (
            <Typography.Text type="danger">{t('payments.trendsDashboard.loadError')}</Typography.Text>
        );
    }

    return (
        <div className={styles.dashboard}>
            <Card className={styles.controlsCard}>
                <Space wrap>
                    <Select
                        value={period}
                        onChange={setPeriod}
                        style={{ minWidth: 160 }}
                        options={PERIOD_OPTIONS.map((value) => ({
                            value,
                            label: t(`payments.trends.period.${value}`),
                        }))}
                    />
                    <RangePicker
                        format={DAYJS_DATE_FORMAT}
                        value={dateRange}
                        onChange={(values) => {
                            if (!values?.[0] || !values[1]) {
                                setDateRange(null);
                                return;
                            }
                            setDateRange([values[0], values[1]]);
                        }}
                        allowClear
                    />
                </Space>
            </Card>

            <Row gutter={[16, 16]} className={styles.metricRow}>
                <Col xs={24} sm={12} lg={6}>
                    <Card>
                        <Statistic
                            title={t('payments.trendsDashboard.totalRevenue')}
                            value={trends?.summary.totalRevenue ?? 0}
                            precision={2}
                            prefix={<DollarOutlined />}
                            suffix="€"
                            styles={{ content: {  color: '#3f8600'  } }}
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} lg={6}>
                    <Card>
                        <Statistic
                            title={t('payments.trendsDashboard.transactions')}
                            value={trends?.summary.totalTransactions ?? 0}
                            prefix={<ShoppingOutlined />}
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} lg={6}>
                    <Card>
                        <Statistic
                            title={t('payments.trendsDashboard.avgBasket')}
                            value={trends?.summary.averageTransactionValue ?? 0}
                            precision={2}
                            prefix={<DollarOutlined />}
                            suffix="€"
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} lg={6}>
                    <Card>
                        <Statistic
                            title={t('payments.trendsDashboard.growth')}
                            value={Math.abs(growth)}
                            precision={1}
                            suffix="%"
                            prefix={
                                growthPositive ? (
                                    <ArrowUpOutlined />
                                ) : growthNegative ? (
                                    <ArrowDownOutlined />
                                ) : (
                                    <MinusOutlined />
                                )
                            }
                            styles={{ content: {  color: growthPositive ? '#3f8600' : growthNegative ? '#cf1322' : undefined  } }}
                        />
                        <div className={styles.growthHint}>{t('payments.trendsDashboard.vsPreviousPeriod')}</div>
                    </Card>
                </Col>
            </Row>

            <Card title={t('payments.trendsDashboard.revenueTrendTitle')} className={styles.chartCard}>
                {chartData.length > 0 ? (
                    <ResponsiveContainer width="100%" height={400}>
                        <ComposedChart data={chartData}>
                            <CartesianGrid strokeDasharray="3 3" />
                            <XAxis dataKey="xLabel" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
                            <YAxis yAxisId="left" tick={{ fontSize: 11 }} />
                            <YAxis yAxisId="right" orientation="right" tick={{ fontSize: 11 }} />
                            <Tooltip
                                formatter={(value, name) => {
                                    const n = Number(value ?? 0);
                                    if (name === 'transactionCount') {
                                        return [n, t('payments.trends.chart.count')];
                                    }
                                    return [`€${n.toFixed(2)}`, name];
                                }}
                            />
                            <Legend />
                            <Area
                                yAxisId="left"
                                type="monotone"
                                dataKey="totalAmount"
                                stroke="#8884d8"
                                fill="#8884d8"
                                fillOpacity={0.25}
                                name={t('payments.trendsDashboard.revenueSeries')}
                            />
                            <Line
                                yAxisId="right"
                                type="monotone"
                                dataKey="averageAmount"
                                stroke="#82ca9d"
                                dot={false}
                                name={t('payments.trendsDashboard.avgBasketSeries')}
                            />
                        </ComposedChart>
                    </ResponsiveContainer>
                ) : (
                    <Typography.Text type="secondary">{t('payments.trends.empty')}</Typography.Text>
                )}
            </Card>

            <Card title={t('payments.trendsDashboard.transactionVolumeTitle')} className={styles.chartCard}>
                {chartData.length > 0 ? (
                    <ResponsiveContainer width="100%" height={300}>
                        <BarChart data={chartData}>
                            <CartesianGrid strokeDasharray="3 3" />
                            <XAxis dataKey="xLabel" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
                            <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                            <Tooltip />
                            <Bar
                                dataKey="transactionCount"
                                fill="#ffc658"
                                name={t('payments.trendsDashboard.transactionSeries')}
                                radius={[4, 4, 0, 0]}
                                maxBarSize={48}
                            />
                        </BarChart>
                    </ResponsiveContainer>
                ) : (
                    <Typography.Text type="secondary">{t('payments.trends.empty')}</Typography.Text>
                )}
            </Card>

            <Card title={t('payments.trendsDashboard.paymentMethodsTitle')}>
                <Row gutter={[16, 16]}>
                    <Col xs={24} md={12}>
                        {methodPieData.length > 0 ? (
                            <ResponsiveContainer width="100%" height={300}>
                                <PieChart>
                                    <Pie
                                        data={methodPieData}
                                        dataKey="amount"
                                        nameKey="method"
                                        cx="50%"
                                        cy="50%"
                                        outerRadius={100}
                                        label={({ name, percent }) =>
                                            `${name} (${((percent ?? 0) * 100).toFixed(0)}%)`
                                        }
                                    >
                                        {methodPieData.map((entry, index) => (
                                            <Cell
                                                key={entry.method}
                                                fill={PIE_COLORS[index % PIE_COLORS.length]}
                                            />
                                        ))}
                                    </Pie>
                                    <Tooltip formatter={(value) => `€${Number(value ?? 0).toFixed(2)}`} />
                                </PieChart>
                            </ResponsiveContainer>
                        ) : (
                            <Typography.Text type="secondary">{t('payments.trends.empty')}</Typography.Text>
                        )}
                    </Col>
                    <Col xs={24} md={12}>
                        <div className={styles.methodList}>
                            {methodPieData.map((method) => (
                                <div key={method.method} className={styles.methodRow}>
                                    <span className={styles.methodName}>{method.method}</span>
                                    <span className={styles.methodAmount}>€{method.amount.toFixed(2)}</span>
                                    <span className={styles.methodPct}>
                                        ({method.percentage.toFixed(1)}%)
                                    </span>
                                </div>
                            ))}
                        </div>
                    </Col>
                </Row>
            </Card>
        </div>
    );
}
