'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo } from 'react';

import {
  acknowledgeMaintenanceNotification,
  fetchActiveMaintenanceNotifications,
  type MaintenanceNotificationDto,
} from '@/api/manual/maintenanceNotifications';
import { useAuth } from '@/features/auth/hooks/useAuth';

const activeKey = ['admin', 'maintenance-notifications', 'active'] as const;

const POLL_MS = 60_000;
const FORCE_POLL_MS = 15_000;
const DAY_MS = 24 * 60 * 60 * 1000;

function computeClientForceDisplay(active: MaintenanceNotificationDto | null): boolean {
  if (!active) return false;
  if (active.effectiveForceDisplay || active.isForceDisplay || active.isMandatory) {
    return true;
  }

  const forceFrom = active.forceDisplayFrom
    ? new Date(active.forceDisplayFrom).getTime()
    : null;
  if (forceFrom != null && Date.now() >= forceFrom) {
    return true;
  }

  // Auto-force display within 24 hours of scheduled start (client-side catch-up).
  const timeUntilStart = new Date(active.scheduledStartAt).getTime() - Date.now();
  return timeUntilStart >= 0 && timeUntilStart < DAY_MS;
}

export function useMaintenanceNotifications() {
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: activeKey,
    queryFn: ({ signal }) => fetchActiveMaintenanceNotifications(signal),
    enabled: isAuthenticated,
    refetchInterval: (q) => {
      if (!isAuthenticated) return false;
      const active = q.state.data?.[0] ?? null;
      return computeClientForceDisplay(active) ? FORCE_POLL_MS : POLL_MS;
    },
  });

  const dismissMutation = useMutation({
    mutationFn: (id: string) =>
      acknowledgeMaintenanceNotification(id, { dismiss: true, markRead: true }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: activeKey });
    },
  });

  const notifications = query.data ?? [];
  const active = notifications[0] ?? null;
  const isForceDisplay = useMemo(() => computeClientForceDisplay(active), [active]);

  return {
    notifications,
    activeNotification: active,
    isForceDisplay,
    isLoading: query.isLoading,
    dismissNotification: (id: string) => dismissMutation.mutateAsync(id),
    isDismissing: dismissMutation.isPending,
    refetch: query.refetch,
  };
}

export type { MaintenanceNotificationDto };
