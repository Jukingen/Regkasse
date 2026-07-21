/** Manager activity log action filter values (backend AuditLog.Action). */
export const ACTIVITY_LOG_ACTION_FILTER_VALUES = [
  '',
  'USER_LOGIN',
  'USER_LOGOUT',
  'PAYMENT_CONFIRM',
  'POS_PAY_OUTCOME',
  'ShiftStarted',
  'ShiftEnded',
  'POS_SPL_RCPT',
  'CUSTOMER_CREATE',
  'CUSTOMER_UPDATE',
  'USER_UPDATE',
] as const;

export type ActivityLogActionFilter = (typeof ACTIVITY_LOG_ACTION_FILTER_VALUES)[number];

const ACTION_TAG_COLORS: Record<string, string> = {
  USER_LOGIN: 'green',
  USER_LOGOUT: 'default',
  PAYMENT_CONFIRM: 'gold',
  PAYMENT_INITIATE: 'gold',
  PAYMENT_CANCEL: 'volcano',
  PAYMENT_REFUND: 'orange',
  PaymentCreated: 'blue',
  PaymentReversal: 'volcano',
  POS_PAY_OUTCOME: 'blue',
  POS_PAY_EX: 'red',
  POS_REG_READY: 'purple',
  ShiftStarted: 'purple',
  ShiftEnded: 'orange',
  POS_SPL_RCPT: 'cyan',
  CUSTOMER_CREATE: 'lime',
  CUSTOMER_UPDATE: 'geekblue',
  USER_UPDATE: 'geekblue',
};

export function activityLogActionTagColor(action: string | null | undefined): string {
  const key = action?.trim() ?? '';
  return ACTION_TAG_COLORS[key] ?? 'default';
}
