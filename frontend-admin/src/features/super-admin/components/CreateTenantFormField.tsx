'use client';

/**
 * Tenant create form field with label, tooltip, live validation feedback, and hint text.
 */
import { Form } from 'antd';
import type { FormItemProps } from 'antd/es/form';
import React from 'react';

import styles from '@/styles/tenant-form.module.css';

export type CreateTenantFormFieldProps = FormItemProps & {
  /** Static guidance shown below the field (always visible). */
  hint?: string;
  /** Optional success line (styled .success) shown via Form.Item help. */
  successHelp?: React.ReactNode;
};

export function CreateTenantFormField({
  hint,
  successHelp,
  rules,
  validateTrigger = ['onChange', 'onBlur'],
  hasFeedback = true,
  children,
  help,
  ...formItemProps
}: CreateTenantFormFieldProps) {
  const resolvedHelp =
    help ?? (successHelp ? <div className={styles.success}>{successHelp}</div> : undefined);

  return (
    <Form.Item
      {...formItemProps}
      rules={rules}
      validateTrigger={validateTrigger}
      hasFeedback={hasFeedback}
      help={resolvedHelp}
      extra={hint ? <div className={styles.hint}>{hint}</div> : undefined}
    >
      {children}
    </Form.Item>
  );
}
