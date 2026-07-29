'use client';

import { useEffect, useRef } from 'react';

import { postLicenseRenewalFunnelPageView } from '@/api/manual/adminLicense';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

/**
 * Fire-and-forget renewal UI page-view for Super Admin funnel analytics.
 * Server dedupes to one event per tenant per UTC day.
 */
export function useLicenseRenewalFunnelPageView(enabled: boolean) {
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_MANAGE,
  });
  const sentRef = useRef(false);

  useEffect(() => {
    if (!enabled || !isAuthorized || sentRef.current) return;
    sentRef.current = true;
    void postLicenseRenewalFunnelPageView().catch(() => {
      // Analytics must not block renewal UX.
      sentRef.current = false;
    });
  }, [enabled, isAuthorized]);
}
