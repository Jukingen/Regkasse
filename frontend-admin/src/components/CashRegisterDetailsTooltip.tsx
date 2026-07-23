'use client';

import { CheckOutlined, CopyOutlined } from '@ant-design/icons';
import { Tooltip, Typography } from 'antd';
import type { ReactNode } from 'react';
import { useMemo } from 'react';

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

type DetailEntry = {
  label: string;
  value: string;
  copyable: boolean;
};

const COPY_ICON = (
  <CopyOutlined key="copy" style={{ color: 'rgba(255,255,255,0.72)', fontSize: 12 }} />
);
const COPIED_ICON = <CheckOutlined key="copied" style={{ color: '#95de64', fontSize: 12 }} />;

function isCopyableValue(value: string, unavailable: string): boolean {
  const trimmed = value.trim();
  return trimmed.length > 0 && trimmed !== unavailable;
}

function DetailRow({
  label,
  value,
  copyable,
  copyTooltip,
  copiedTooltip,
}: {
  label: string;
  value: string;
  copyable: boolean;
  copyTooltip: string;
  copiedTooltip: string;
}) {
  return (
    <div
      style={{
        display: 'flex',
        gap: 8,
        lineHeight: 1.45,
        alignItems: 'flex-start',
      }}
    >
      <Typography.Text
        style={{ color: 'rgba(255,255,255,0.75)', minWidth: 88, flexShrink: 0, userSelect: 'none' }}
      >
        {label}:
      </Typography.Text>
      <Typography.Text
        style={{ color: '#fff', fontWeight: 500, wordBreak: 'break-all', flex: 1 }}
        copyable={
          copyable
            ? {
                text: value,
                tooltips: [copyTooltip, copiedTooltip],
                icon: [COPY_ICON, COPIED_ICON],
              }
            : false
        }
      >
        {value}
      </Typography.Text>
    </div>
  );
}

export function CashRegisterDetailsTooltipContent({
  register,
}: {
  register: AdminCashRegisterListItem;
}) {
  const { t, formatLocale } = useI18n();
  const unavailable = t('cashRegisters.selector.tooltip.unavailable');
  const copyTooltip = t('cashRegisters.selector.tooltip.copyValue');
  const copiedTooltip = t('cashRegisters.selector.tooltip.copied');

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
    : unavailable;
  const balanceText =
    typeof register.currentBalance === 'number'
      ? formatCurrency(register.currentBalance, formatLocale)
      : unavailable;
  const mandantIdText = register.tenantId?.trim() ? register.tenantId.trim() : unavailable;

  const rows = useMemo<DetailEntry[]>(
    () => [
      {
        label: t('cashRegisters.selector.tooltip.kasse'),
        value: name,
        copyable: isCopyableValue(name, unavailable),
      },
      {
        label: t('cashRegisters.selector.tooltip.id'),
        value: register.id,
        copyable: isCopyableValue(register.id, unavailable),
      },
      {
        label: t('cashRegisters.selector.tooltip.mandantId'),
        value: mandantIdText,
        copyable: isCopyableValue(mandantIdText, unavailable),
      },
      {
        label: t('cashRegisters.selector.tooltip.status'),
        value: statusText,
        copyable: isCopyableValue(statusText, unavailable),
      },
      {
        label: t('cashRegisters.selector.tooltip.tse'),
        value: tseText,
        copyable: isCopyableValue(tseText, unavailable),
      },
      {
        label: t('cashRegisters.selector.tooltip.balance'),
        value: balanceText,
        copyable: isCopyableValue(balanceText, unavailable),
      },
    ],
    [
      balanceText,
      mandantIdText,
      name,
      register.id,
      statusText,
      t,
      tseText,
      unavailable,
    ]
  );

  const allText = useMemo(
    () => rows.map((row) => `${row.label}: ${row.value}`).join('\n'),
    [rows]
  );

  return (
    <div
      data-testid="cash-register-details-tooltip"
      style={{ minWidth: 240, maxWidth: 420 }}
      onMouseDown={(event) => {
        // Keep tooltip open while selecting / clicking copy controls.
        event.stopPropagation();
      }}
    >
      {rows.map((row) => (
        <DetailRow
          key={row.label}
          label={row.label}
          value={row.value}
          copyable={row.copyable}
          copyTooltip={copyTooltip}
          copiedTooltip={copiedTooltip}
        />
      ))}
      <div
        style={{
          marginTop: 8,
          paddingTop: 8,
          borderTop: '1px solid rgba(255,255,255,0.18)',
          display: 'flex',
          justifyContent: 'flex-end',
        }}
      >
        <Typography.Text
          data-testid="cash-register-details-copy-all"
          style={{ color: 'rgba(255,255,255,0.9)', fontSize: 12 }}
          copyable={{
            text: allText,
            tooltips: [
              t('cashRegisters.selector.tooltip.copyAll'),
              t('cashRegisters.selector.tooltip.copied'),
            ],
            icon: [COPY_ICON, COPIED_ICON],
          }}
        >
          {t('cashRegisters.selector.tooltip.copyAll')}
        </Typography.Text>
      </div>
    </div>
  );
}

/**
 * Hover/focus tooltip with full register identity and per-row copy helpers.
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
      mouseLeaveDelay={0.45}
      trigger={['hover', 'focus']}
      styles={{ root: { maxWidth: 440 } }}
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
