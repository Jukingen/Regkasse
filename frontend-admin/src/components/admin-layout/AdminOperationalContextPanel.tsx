'use client';

/**
 * Lightweight "context / hints / investigation" band for complex operational admin pages.
 * Keeps copyable IDs and deep links visible while long explanatory copy stays in an optional Collapse.
 *
 * Do not use for critical errors — use Ant Design Alert (or error Result) at a higher priority band.
 */

import React from 'react';
import { Card, Collapse, Space, Typography } from 'antd';

export type AdminOperationalContextCollapsible = {
    /** Stable key for Ant Collapse (default: 'detail'). */
    panelKey?: string;
    /** Panel header (localized at call site). */
    label: React.ReactNode;
    /** Long helper / contract / methodology copy. */
    children: React.ReactNode;
    /** Default closed to reduce vertical push (operational tables first). */
    defaultOpen?: boolean;
};

export type AdminOperationalContextPanelProps = {
    /** Short Card title (localized at call site). */
    title: React.ReactNode;
    /**
     * Optional eyebrow above summary (localized at call site).
     * Use sparingly; prefer a concise `title`.
     */
    eyebrow?: React.ReactNode;
    /**
     * Always-visible body: copyable `Typography.Text code`, deep links, one-line hints.
     * Keep this short so the table stays scannable.
     */
    summary?: React.ReactNode;
    /** Optional long text / contract — rendered inside a Collapse (default collapsed). */
    collapsible?: AdminOperationalContextCollapsible;
    /**
     * `emphasis` — slightly stronger border (investigation / handoff).
     * `default` — neutral hints (diagnostics secondary notes).
     */
    variant?: 'default' | 'emphasis';
    className?: string;
    style?: React.CSSProperties;
};

export function AdminOperationalContextPanel({
    title,
    eyebrow,
    summary,
    collapsible,
    variant = 'default',
    className,
    style,
}: AdminOperationalContextPanelProps) {
    const borderColor = variant === 'emphasis' ? '#91caff' : undefined;

    return (
        <Card
            size="small"
            className={className}
            style={{
                marginBottom: 12,
                ...(borderColor ? { borderColor } : {}),
                ...style,
            }}
            title={title}
        >
            <Space orientation="vertical" size={10} style={{ width: '100%' }}>
                {eyebrow ? (
                    <Typography.Text type="secondary" style={{ fontSize: 11, textTransform: 'uppercase' }}>
                        {eyebrow}
                    </Typography.Text>
                ) : null}
                {summary ?? null}
                {collapsible ? (
                    <Collapse
                        bordered={false}
                        ghost
                        size="small"
                        defaultActiveKey={collapsible.defaultOpen ? [collapsible.panelKey ?? 'detail'] : []}
                        items={[
                            {
                                key: collapsible.panelKey ?? 'detail',
                                label: collapsible.label,
                                children: collapsible.children,
                            },
                        ]}
                    />
                ) : null}
            </Space>
        </Card>
    );
}
