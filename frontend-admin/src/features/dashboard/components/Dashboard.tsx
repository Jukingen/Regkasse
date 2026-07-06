'use client';

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Button, Spin } from 'antd';
import { SettingOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import {
    dashboardPreferencesQueryKeys,
    fetchDashboardPreferences,
    fetchDashboardWidgetCatalog,
    saveDashboardPreferences,
} from '@/features/dashboard/api/dashboardPreferences';
import { WidgetGrid } from '@/features/dashboard/components/WidgetGrid';
import { DashboardSettingsPanel } from '@/features/dashboard/components/DashboardSettingsPanel';
import {
    filterDashboardCatalogByPermissions,
    filterDashboardLayoutByCatalog,
} from '@/features/dashboard/logic/dashboardWidgetVisibility';
import type { DashboardWidgetPreference } from '@/features/dashboard/types';
import { usePermissions } from '@/hooks/usePermissions';

type DashboardProps = {
    /** Optional fixed sections rendered above the customizable widget grid. */
    headerSlot?: React.ReactNode;
};

/** Customizable admin dashboard with persisted per-user widget layout. */
export function Dashboard({ headerSlot }: DashboardProps) {
    const queryClient = useQueryClient();
    const { hasPermission } = usePermissions();
    const [layout, setLayout] = useState<DashboardWidgetPreference[] | null>(null);
    const [settingsOpen, setSettingsOpen] = useState(false);
    const saveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

    const catalogQuery = useAuthorizedQuery({
        queryKey: dashboardPreferencesQueryKeys.catalog,
        queryFn: fetchDashboardWidgetCatalog,
        staleTime: 5 * 60_000,
        requiredRole: [],
    });

    const preferencesQuery = useAuthorizedQuery({
        queryKey: dashboardPreferencesQueryKeys.preferences,
        queryFn: fetchDashboardPreferences,
        requiredRole: [],
    });

    useEffect(() => {
        if (preferencesQuery.data?.widgets) {
            setLayout(preferencesQuery.data.widgets);
        }
    }, [preferencesQuery.data?.widgets]);

    const saveMutation = useMutation({
        mutationFn: saveDashboardPreferences,
        onSuccess: (data) => {
            setLayout(data.widgets);
            queryClient.setQueryData(dashboardPreferencesQueryKeys.preferences, data);
        },
    });

    const scheduleSave = useCallback(
        (next: DashboardWidgetPreference[]) => {
            setLayout(next);
            if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
            saveTimerRef.current = setTimeout(() => {
                saveMutation.mutate({ widgets: next });
            }, 400);
        },
        [saveMutation],
    );

    useEffect(
        () => () => {
            if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
        },
        [],
    );

    const titleById = useMemo(() => {
        const map = new Map<string, string>();
        for (const item of catalogQuery.data ?? []) {
            map.set(item.widgetId, item.title);
        }
        return map;
    }, [catalogQuery.data]);

    const allowedCatalog = useMemo(
        () => filterDashboardCatalogByPermissions(catalogQuery.data ?? [], hasPermission),
        [catalogQuery.data, hasPermission],
    );

    const visibleLayout = useMemo(
        () => (layout ? filterDashboardLayoutByCatalog(layout, allowedCatalog) : null),
        [layout, allowedCatalog],
    );

    const handleVisibilityChange = (widgetId: string, isVisible: boolean) => {
        if (!layout) return;
        const next = layout.map((w) =>
            w.widgetId === widgetId ? { ...w, isVisible } : w,
        );
        scheduleSave(next);
    };

    const handleWidgetSettingsChange = (widgetId: string, settings: Record<string, unknown>) => {
        if (!layout) return;
        const next = layout.map((w) =>
            w.widgetId === widgetId ? { ...w, settings: { ...w.settings, ...settings } } : w,
        );
        scheduleSave(next);
    };

    const loading = catalogQuery.isLoading || preferencesQuery.isLoading;
    const error = catalogQuery.error ?? preferencesQuery.error;

    return (
        <>
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 16 }}>
                <Button
                    icon={<SettingOutlined />}
                    onClick={() => setSettingsOpen(true)}
                    disabled={loading || !layout}
                >
                    Widgets anpassen
                </Button>
            </div>

            {headerSlot}

            {loading ? (
                <div style={{ textAlign: 'center', padding: 48 }}>
                    <Spin size="large" />
                </div>
            ) : null}

            {error ? (
                <Alert
                    type="error"
                    showIcon
                    title="Dashboard konnte nicht geladen werden"
                    style={{ marginBottom: 16 }}
                />
            ) : null}

            {visibleLayout && !loading ? (
                <WidgetGrid
                    widgets={visibleLayout}
                    titleById={titleById}
                    onReorder={scheduleSave}
                    onWidgetSettingsChange={handleWidgetSettingsChange}
                />
            ) : null}

            {saveMutation.isError ? (
                <Alert
                    type="warning"
                    showIcon
                    closable
                    title="Layout konnte nicht gespeichert werden"
                    style={{ marginTop: 8 }}
                />
            ) : null}

            <DashboardSettingsPanel
                open={settingsOpen}
                onClose={() => setSettingsOpen(false)}
                catalog={allowedCatalog}
                widgets={visibleLayout ?? []}
                onVisibilityChange={handleVisibilityChange}
            />
        </>
    );
}
