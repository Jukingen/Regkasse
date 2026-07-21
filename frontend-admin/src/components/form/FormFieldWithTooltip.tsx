'use client';

import { Form } from 'antd';
import type { FormItemProps } from 'antd/es/form';
import type { ReactNode } from 'react';

export type FormFieldWithTooltipProps = FormItemProps & {
  /** Tooltip shown next to the label (Ant Design Form.Item tooltip). */
  tooltip?: ReactNode;
  /** Always-visible hint below the control (Form.Item extra). */
  hint?: ReactNode;
};

/**
 * Thin Form.Item wrapper that standardizes tooltip + optional hint for complex fields.
 * Prefer this over scattering QuestionCircle icons ad hoc.
 */
export function FormFieldWithTooltip({
  tooltip,
  hint,
  children,
  extra,
  ...formItemProps
}: FormFieldWithTooltipProps) {
  return (
    <Form.Item {...formItemProps} tooltip={tooltip} extra={hint ?? extra}>
      {children}
    </Form.Item>
  );
}
