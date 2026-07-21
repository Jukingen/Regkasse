'use client';

import {
  CheckCircleOutlined,
  LockOutlined,
  MinusCircleOutlined,
  StopOutlined,
  ToolOutlined,
} from '@ant-design/icons';
import { Tag, Tooltip } from 'antd';
import type { ReactNode } from 'react';

import type { CashRegister } from '@/api/generated/model';
import {
  type ResolveCashRegisterStatusBadgeOptions,
  resolveCashRegisterStatusBadge,
} from '@/features/cash-registers/utils/cashRegisterStatusConfig';
import { REGISTER_STATUS, rawRegisterStatus } from '@/features/cash-registers/utils/registerStatus';
import { useLicense } from '@/features/license/hooks/useLicense';
import { useI18n } from '@/i18n';

export type CashRegisterStatusBadgeProps = {
  register: CashRegister;
  licenseExpired?: boolean;
  showTooltip?: boolean;
  /** When true, uses Ant Design icons instead of emoji (table density). */
  useIcon?: boolean;
};

function resolveStatusIcon(status: number | undefined): ReactNode | undefined {
  switch (status) {
    case REGISTER_STATUS.open:
      return <CheckCircleOutlined />;
    case REGISTER_STATUS.closed:
      return <LockOutlined />;
    case REGISTER_STATUS.maintenance:
      return <ToolOutlined />;
    case REGISTER_STATUS.disabled:
      return <MinusCircleOutlined />;
    case REGISTER_STATUS.decommissioned:
      return <StopOutlined />;
    default:
      return <MinusCircleOutlined />;
  }
}

function isLicenseExpiredKind(kind: string | undefined): boolean {
  return kind === 'expired' || kind === 'lockdown' || kind === 'no_license';
}

export function CashRegisterStatusBadge({
  register,
  licenseExpired,
  showTooltip = true,
  useIcon = false,
}: CashRegisterStatusBadgeProps) {
  const { t } = useI18n();
  const { licenseStatus } = useLicense();

  const resolvedLicenseExpired =
    licenseExpired ??
    (isClosedRegisterForBadge(register) && isLicenseExpiredKind(licenseStatus?.kind));

  const options: ResolveCashRegisterStatusBadgeOptions = {
    licenseExpired: resolvedLicenseExpired,
  };

  const config = resolveCashRegisterStatusBadge(register, t, options);
  const status = rawRegisterStatus(register);

  const tag = (
    <Tag color={config.tagColor} icon={useIcon ? resolveStatusIcon(status) : undefined}>
      {useIcon ? config.text : `${config.emoji} ${config.text}`}
    </Tag>
  );

  if (showTooltip && config.tooltip) {
    return <Tooltip title={config.tooltip}>{tag}</Tooltip>;
  }

  return tag;
}

function isClosedRegisterForBadge(register: CashRegister): boolean {
  return rawRegisterStatus(register) === REGISTER_STATUS.closed;
}
