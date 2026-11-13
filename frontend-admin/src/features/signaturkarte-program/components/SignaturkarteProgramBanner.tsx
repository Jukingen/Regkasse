'use client';

/**
 * Mai 2027 Signaturkarte program banner — independent of certificate ExpiresAt.
 * Severity: info (>90d), warning (30–90d), critical (≤7d / overdue).
 */
import { Alert, Button, Space } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useMemo, useState } from 'react';

import { getSignaturkarteProgramStatus } from '@/features/signaturkarte-program/api/signaturkarteProgram';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const DISMISS_KEY = 'signaturkarte-program-banner-dismissed-until';
const INFO_DISMISS_DAYS = 7;

function readDismissedUntil(): number | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = localStorage.getItem(DISMISS_KEY);
    if (!raw) return null;
    const n = Number(raw);
    return Number.isFinite(n) ? n : null;
  } catch {
    return null;
  }
}

export function SignaturkarteProgramBanner() {
  const { t, textLocale } = useI18n();
  const router = useRouter();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SETTINGS_VIEW);
  const [dismissedUntil, setDismissedUntil] = useState<number | null>(() => readDismissedUntil());

  const query = useQuery({
    queryKey: ['signaturkarte-program', 'status'],
    queryFn: ({ signal }) => getSignaturkarteProgramStatus(signal),
    enabled: allowed,
    staleTime: 5 * 60 * 1000,
    refetchOnWindowFocus: true,
  });

  const status = query.data;
  const severity = status?.bannerSeverity ?? null;

  const hiddenByDismiss = useMemo(() => {
    if (severity !== 'info') return false;
    if (dismissedUntil == null) return false;
    return Date.now() < dismissedUntil;
  }, [severity, dismissedUntil]);

  if (!allowed || !status?.enabled || !severity || status.totals.nonCompliant <= 0) {
    return null;
  }

  if (hiddenByDismiss) return null;

  const alertType = severity === 'critical' ? 'error' : severity === 'warning' ? 'warning' : 'info';
  const localeTag = textLocale === 'tr' ? 'tr-TR' : textLocale === 'en' ? 'en-GB' : 'de-AT';
  const deadlineLabel = new Date(status.deadlineUtc).toLocaleDateString(localeTag, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });

  const dismissInfo = () => {
    const until = Date.now() + INFO_DISMISS_DAYS * 24 * 60 * 60 * 1000;
    try {
      localStorage.setItem(DISMISS_KEY, String(until));
    } catch {
      /* ignore */
    }
    setDismissedUntil(until);
  };

  return (
    <Alert
      banner
      showIcon
      type={alertType}
      style={{ marginBottom: 12 }}
      data-signaturkarte-program-banner={severity}
      data-certificate-expiry="false"
      title={t(`signaturkarteProgram.banner.${severity}.title`)}
      description={
        <Space orientation="vertical" size="small">
          <span>
            {t(`signaturkarteProgram.banner.${severity}.body`, {
              days: status.daysRemaining,
              open: status.totals.nonCompliant,
              deadline: deadlineLabel,
            })}
          </span>
          <span style={{ opacity: 0.85 }}>{t('signaturkarteProgram.banner.separationNote')}</span>
          <Space wrap>
            <Button
              size="small"
              type="primary"
              onClick={() => router.push('/admin/tse/signaturkarte-program')}
            >
              {t('signaturkarteProgram.banner.openReport')}
            </Button>
            {severity === 'info' ? (
              <Button size="small" onClick={dismissInfo}>
                {t('signaturkarteProgram.banner.dismiss')}
              </Button>
            ) : null}
          </Space>
        </Space>
      }
    />
  );
}
