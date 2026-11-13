'use client';

import { Button } from 'antd';
import Link from 'next/link';
import React, { useMemo } from 'react';

import { useMonatsbelegStatus } from '@/features/rksv/hooks/useMonatsbeleg';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

/**
 * Header badge: count of cash registers whose Monatsbeleg status is `red` (überfällig).
 * Uses RKSV overview only — no legacy GET /api/CashRegister poll on every page.
 */
export function MonatsbelegGlobalBadge() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canSee = hasPermission(PERMISSIONS.CASHREGISTER_VIEW);

  const { data, isLoading: _isLoading } = useMonatsbelegStatus({ enabled: canSee });

  const redCount = useMemo(
    () =>
      (data ?? []).filter((item) => item.status?.missingMonths?.some((month) => month.isOverdue))
        .length,
    [data]
  );

  if (!canSee || redCount <= 0) {
    return null;
  }

  return (
    <Link
      href="/dashboard"
      aria-label={t('adminShell.header.monatsbelegBadgeAria', { count: redCount })}
    >
      <Button
        type="default"
        size="small"
        className="admin-header-tool-btn admin-header-monatsbeleg-btn"
        danger
      >
        {t('adminShell.header.monatsbelegBadgeLabel', { count: redCount })}
      </Button>
    </Link>
  );
}
