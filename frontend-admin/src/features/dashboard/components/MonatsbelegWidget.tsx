'use client';

import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Button, Card, Spin, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { MonatsbelegComplianceTable } from '@/features/dashboard/components/MonatsbelegComplianceTable';
import { useMonatsbelegDashboard } from '@/features/rksv/hooks/useMonatsbelegStatus';
import { useI18n } from '@/i18n/I18nProvider';

const MAX_AUTO_RETRIES = 3;
const LOADING_TIMEOUT_MS = 10_000;

type MonatsbelegWidgetProps = {
    enabled?: boolean;
};

export function MonatsbelegWidget({ enabled = true }: MonatsbelegWidgetProps) {
    const { t } = useI18n();
    const { data, isLoading, error, refetch, hasRegisters, isFetching } = useMonatsbelegDashboard(enabled);
    const [retryCount, setRetryCount] = useState(0);
    const [loadTimedOut, setLoadTimedOut] = useState(false);
    const isLoadingRef = useRef(isLoading);

    isLoadingRef.current = isLoading;

    const handleRetry = useCallback(() => {
        setRetryCount(0);
        setLoadTimedOut(false);
        void refetch();
    }, [refetch]);

    useEffect(() => {
        if (!isLoading) {
            setLoadTimedOut(false);
            return;
        }

        const timeoutId = window.setTimeout(() => {
            if (isLoadingRef.current) {
                setLoadTimedOut(true);
            }
        }, LOADING_TIMEOUT_MS);

        return () => window.clearTimeout(timeoutId);
    }, [isLoading]);

    useEffect(() => {
        if (!error || retryCount >= MAX_AUTO_RETRIES) return;

        const timer = window.setTimeout(() => {
            setRetryCount((prev) => prev + 1);
            void refetch();
        }, 2000 * (retryCount + 1));

        return () => window.clearTimeout(timer);
    }, [error, retryCount, refetch]);

    const displayError =
        error ??
        (loadTimedOut ? new Error(t('dashboard.monatsbeleg.loadTimeout')) : null);

    const cardTitle = t('dashboard.monatsbeleg.title');

    if (!enabled) return null;

    if (displayError) {
        return (
            <Card
                title={cardTitle}
                variant="borderless"
                style={{ marginBottom: 24 }}
                extra={
                    <Button icon={<ReloadOutlined />} onClick={handleRetry} size="small" loading={isFetching}>
                        {t('dashboard.monatsbeleg.retry')}
                    </Button>
                }
            >
                <Alert
                    title={t('dashboard.monatsbeleg.loadFailedTitle')}
                    description={
                        displayError.message || t('dashboard.monatsbeleg.loadFailedDescription')
                    }
                    type="error"
                    showIcon
                />
            </Card>
        );
    }

    if (isLoading) {
        return (
            <Card title={cardTitle} variant="borderless" style={{ marginBottom: 24 }}>
                <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: 128 }}>
                    <Spin description={t('dashboard.monatsbeleg.loading')} />
                </div>
            </Card>
        );
    }

    if (!hasRegisters || data.length === 0) {
        return (
            <Card
                title={cardTitle}
                variant="borderless"
                style={{ marginBottom: 24 }}
                extra={
                    <Button icon={<ReloadOutlined />} onClick={handleRetry} size="small" loading={isFetching}>
                        {t('dashboard.monatsbeleg.refresh')}
                    </Button>
                }
            >
                <Alert
                    title={t('dashboard.monatsbeleg.noDataTitle')}
                    description={t('dashboard.monatsbeleg.noDataDescription')}
                    type="info"
                    showIcon
                />
            </Card>
        );
    }

    return (
        <MonatsbelegComplianceTable
            rows={data}
            loading={isFetching}
            hasRegisters={hasRegisters}
            headerExtra={
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {t('dashboard.monatsbeleg.refreshInterval')}
                </Typography.Text>
            }
            onRefresh={handleRetry}
            refreshLoading={isFetching}
        />
    );
}
