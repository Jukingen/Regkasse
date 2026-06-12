'use client';

import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Button, Card, Spin, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { MonatsbelegComplianceTable } from '@/features/dashboard/components/MonatsbelegComplianceTable';
import { useMonatsbelegDashboard } from '@/features/rksv/hooks/useMonatsbelegStatus';

const MAX_AUTO_RETRIES = 3;
const LOADING_TIMEOUT_MS = 10_000;

type MonatsbelegWidgetProps = {
    enabled?: boolean;
};

export function MonatsbelegWidget({ enabled = true }: MonatsbelegWidgetProps) {
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
        (loadTimedOut ? new Error('Anfrage-Timeout nach 10 Sekunden. Bitte erneut versuchen.') : null);

    if (!enabled) return null;

    if (displayError) {
        return (
            <Card
                title="Monatsbeleg (RKSV)"
                variant="borderless"
                style={{ marginBottom: 24 }}
                extra={
                    <Button icon={<ReloadOutlined />} onClick={handleRetry} size="small" loading={isFetching}>
                        Wiederholen
                    </Button>
                }
            >
                <Alert
                    title="Daten konnten nicht geladen werden"
                    description={
                        displayError.message ||
                        'Bitte versuchen Sie es später erneut oder kontaktieren Sie den Support.'
                    }
                    type="error"
                    showIcon
                />
            </Card>
        );
    }

    if (isLoading) {
        return (
            <Card title="Monatsbeleg (RKSV)" variant="borderless" style={{ marginBottom: 24 }}>
                <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: 128 }}>
                    <Spin description="Lade Monatsbeleg-Daten…" />
                </div>
            </Card>
        );
    }

    if (!hasRegisters || data.length === 0) {
        return (
            <Card
                title="Monatsbeleg (RKSV)"
                variant="borderless"
                style={{ marginBottom: 24 }}
                extra={
                    <Button icon={<ReloadOutlined />} onClick={handleRetry} size="small" loading={isFetching}>
                        Aktualisieren
                    </Button>
                }
            >
                <Alert
                    title="Keine Daten verfügbar"
                    description="Es wurden noch keine Kassen gefunden. Monatsbeleg-Daten erscheinen, sobald Kassen angelegt sind."
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
                    Aktualisierung alle 5 Minuten
                </Typography.Text>
            }
            onRefresh={handleRetry}
            refreshLoading={isFetching}
        />
    );
}
