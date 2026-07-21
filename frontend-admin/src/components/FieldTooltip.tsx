'use client';

import { QuestionCircleOutlined } from '@ant-design/icons';
import { Tooltip } from 'antd';
import type { ReactNode } from 'react';

export type FieldTooltipProps = {
  title: ReactNode;
  children: ReactNode;
};

/**
 * Inline help icon next to a label or control for complex fields.
 * Prefer Form.Item `tooltip` when the field is a standard Ant Design form item;
 * use this helper when you need the icon beside arbitrary children.
 */
export function FieldTooltip({ title, children }: FieldTooltipProps) {
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
      {children}
      <Tooltip title={title}>
        <QuestionCircleOutlined
          style={{ color: 'var(--ant-color-text-tertiary, #94a3b8)', cursor: 'help' }}
          aria-hidden
        />
      </Tooltip>
    </span>
  );
}
