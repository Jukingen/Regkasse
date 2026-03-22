'use client';

/**
 * Provenance badges — copy from @/shared/operatorTruthCopy (single source for operator German strings).
 */

import React from 'react';
import { Tag, Tooltip } from 'antd';
import {
    OPERATOR_TRUTH_BADGE,
    OPERATOR_TRUTH_BADGE_KINDS,
    type OperatorTruthBadgeKind,
} from '@/shared/operatorTruthCopy';

export type AdminTruthBadgeKind = OperatorTruthBadgeKind;

export const ADMIN_TRUTH_BADGE_KINDS = OPERATOR_TRUTH_BADGE_KINDS;

/** @deprecated Use OPERATOR_TRUTH_BADGE in operatorTruthCopy; kept for tests and imports. */
export const ADMIN_TRUTH_BADGE = OPERATOR_TRUTH_BADGE;

export function adminTruthTooltip(kind: AdminTruthBadgeKind): string {
    return OPERATOR_TRUTH_BADGE[kind].tooltip;
}

export type AdminTruthBadgeProps = {
    kind: AdminTruthBadgeKind;
    size?: 'small' | 'default';
    className?: string;
};

const TAG_FONT: Record<NonNullable<AdminTruthBadgeProps['size']>, number> = {
    small: 10,
    default: 12,
};

/**
 * Focusable wrapper so keyboard users receive the same provenance text as hover (Tooltip).
 */
export function AdminTruthBadge({ kind, size = 'small', className }: AdminTruthBadgeProps) {
    const c = OPERATOR_TRUTH_BADGE[kind];
    const aria = `Datenlage: ${c.shortLabel}. ${c.tooltip}`;
    return (
        <Tooltip title={c.tooltip} placement="topLeft">
            <span
                tabIndex={0}
                className={className}
                style={{
                    cursor: 'help',
                    display: 'inline-flex',
                    alignItems: 'center',
                    outline: 'none',
                }}
                aria-label={aria}
            >
                <Tag
                    color={c.antColor}
                    style={{
                        fontSize: TAG_FONT[size],
                        marginInlineEnd: 0,
                        lineHeight: size === 'small' ? '16px' : undefined,
                    }}
                >
                    {c.shortLabel}
                </Tag>
            </span>
        </Tooltip>
    );
}
