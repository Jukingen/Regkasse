'use client';

import { Tooltip, Typography } from 'antd';
import type { ReactNode } from 'react';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { REGISTER_STATUS } from '@/features/cash-registers/utils/registerStatus';
import { normalizeTseHealthStatus } from '@/features/cash-registers/utils/tseHealthStatus';
import { useI18n } from '@/i18n';
import { formatCurrency } from '@/i18n/formatting';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';

export type CashRegisterDetailsTooltipProps = {
  register: AdminCashRegisterListItem | null | undefined;
  children: ReactNode;
  /** Prefer placing tooltip below header controls. */
  placement?: 'top' | 'bottom' | 'bottomLeft' | 'topLeft';
};

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: 'flex', gap: 8, lineHeight: 1.45 }}>
      <Typography.Text style={{ color: 'rgba(255,255,255,0.75)', minWidth: 56 }}>
        {label}:
      </Typography.Text>
      <Typography.Text style={{ color: '#fff', fontWeight: 500 }}>{value}</Typography.Text>
    </div>
  );
}

export function CashRegisterDetailsTooltipContent({
  register,
}: {
  register: AdminCashRegisterListItem;
}) {
  const { t, formatLocale } = useI18n();
  const name = register.location?.trim()
    ? `${formatRegisterDisplayLabel(register.registerNumber)} — ${register.location.trim()}`
    : formatRegisterDisplayLabel(register.registerNumber);
  const isOpen = register.status === REGISTER_STATUS.open;
  const statusText = isOpen
    ? t('cashRegisters.statusBadge.open.text')
    : t('cashRegisters.statusBadge.closed.generic.text');
  const tseNormalized = register.tseHealthStatus
    ? normalizeTseHealthStatus(register.tseHealthStatus)
    : null;
  const tseText = tseNormalized
    ? t(`cashRegisters.tseHealth.${tseNormalized}`)
    : t('cashRegisters.selector.tooltip.unavailable');
  const balanceText =
    typeof register.currentBalance === 'number'
      ? formatCurrency(register.currentBalance, formatLocale)
      : t('cashRegisters.selector.tooltip.unavailable');

  return (
    <div data-testid="cash-register-details-tooltip" style={{ minWidth: 200 }}>
      <DetailRow label={t('cashRegisters.selector.tooltip.kasse')} value={name} />
      <DetailRow label={t('cashRegisters.selector.tooltip.id')} value={register.id} />
      <DetailRow label={t('cashRegisters.selector.tooltip.status')} value={statusText} />
      <DetailRow label={t('cashRegisters.selector.tooltip.tse')} value={tseText} />
      <DetailRow label={t('cashRegisters.selector.tooltip.balance')} value={balanceText} />
    </div>
  );
}

/**
 * Hover/focus tooltip with full register identity (name, id, status, TSE, balance).
 */
export function CashRegisterDetailsTooltip({
  register,
  children,
  placement = 'bottomLeft',
}: CashRegisterDetailsTooltipProps) {
  if (!register) {
    return <>{children}</>;
  }

  return (
    <Tooltip
      placement={placement}
      mouseEnterDelay={0.25}
      trigger={['hover', 'focus']}
      title={<CashRegisterDetailsTooltipContent register={register} />}
    >
      <span
        className="cash-register-details-tooltip-trigger"
        tabIndex={0}
        style={{ display: 'inline-block', maxWidth: '100%', outline: 'none' }}
      >
        {children}
      </span>
    </Tooltip>
  );
}
