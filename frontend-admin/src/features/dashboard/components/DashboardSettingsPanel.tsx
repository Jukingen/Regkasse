'use client';

import React from 'react';
import { Drawer, Switch, Typography, List } from 'antd';
import type { DashboardWidgetCatalogItem, DashboardWidgetPreference } from '@/features/dashboard/types';

type Props = {
    open: boolean;
    onClose: () => void;
    catalog: DashboardWidgetCatalogItem[];
    widgets: DashboardWidgetPreference[];
    onVisibilityChange: (widgetId: string, isVisible: boolean) => void;
};

/** Toggle which widgets appear on the dashboard. */
export function DashboardSettingsPanel({
    open,
    onClose,
    catalog,
    widgets,
    onVisibilityChange,
}: Props) {
    const visibility = new Map(widgets.map((w) => [w.widgetId, w.isVisible]));

    return (
        <Drawer
            title="Dashboard anpassen"
            placement="right"
            width={360}
            open={open}
            onClose={onClose}
        >
            <Typography.Paragraph type="secondary">
                Widgets ein- oder ausblenden. Sichtbare Widgets können per Ziehen auf dem Dashboard
                sortiert werden.
            </Typography.Paragraph>
            <List
                dataSource={catalog}
                rowKey="widgetId"
                renderItem={(item) => {
                    const checked = visibility.get(item.widgetId) ?? item.defaultVisible;
                    return (
                        <List.Item
                            actions={[
                                <Switch
                                    key="vis"
                                    checked={checked}
                                    onChange={(v) => onVisibilityChange(item.widgetId, v)}
                                    aria-label={`${item.title} anzeigen`}
                                />,
                            ]}
                        >
                            <List.Item.Meta title={item.title} description={item.description} />
                        </List.Item>
                    );
                }}
            />
        </Drawer>
    );
}
