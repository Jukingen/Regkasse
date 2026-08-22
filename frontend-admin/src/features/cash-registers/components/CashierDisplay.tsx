'use client';

import { Typography } from 'antd';

import { useI18n } from '@/i18n';

export type CashierDisplayUser = {
  firstName?: string | null;
  lastName?: string | null;
  userName?: string | null;
  email?: string | null;
};

export type CashierDisplayProps = {
  user?: CashierDisplayUser | null;
  /** Display name from the admin register DTO (`currentCashierName`). */
  displayName?: string | null;
  userName?: string | null;
  email?: string | null;
};

function fullName(user: CashierDisplayUser | null | undefined): string {
  if (!user) {
    return '';
  }
  return [user.firstName, user.lastName].filter((part) => part?.trim()).join(' ').trim();
}

/**
 * Open-shift cashier: display name plus login, so similar names stay distinguishable.
 */
export function CashierDisplay({ user, displayName, userName, email }: CashierDisplayProps) {
  const { t } = useI18n();

  const name = fullName(user) || displayName?.trim() || '';
  const login = user?.userName?.trim() || userName?.trim() || '';
  const mail = user?.email?.trim() || email?.trim() || '';

  if (!name && !login) {
    return <Typography.Text type="secondary">{t('cashRegisters.detail.noCashier')}</Typography.Text>;
  }

  const title = name || login;
  const showUserName = Boolean(login && login.localeCompare(title, undefined, { sensitivity: 'accent' }) !== 0);

  return (
    <div>
      <Typography.Text strong>{title}</Typography.Text>
      {showUserName ? (
        <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
          ({login})
        </Typography.Text>
      ) : null}
      {mail ? (
        <div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {mail}
          </Typography.Text>
        </div>
      ) : null}
    </div>
  );
}
