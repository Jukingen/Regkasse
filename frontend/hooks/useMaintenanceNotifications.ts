import { useCallback, useEffect, useMemo, useState } from 'react';

import {
  acknowledgeMaintenanceNotification,
  fetchActiveMaintenanceNotifications,
  type MaintenanceNotificationDto,
} from '../services/api/maintenanceService';
import { formatCountdown } from '../utils/viennaTagesabschlussReminder';
import { useAuth } from '../contexts/AuthContext';

const POLL_MS = 60_000;
const FORCE_POLL_MS = 15_000;
const TICK_MS = 1_000;
const DAY_MS = 24 * 60 * 60 * 1000;

function computeClientForceDisplay(
  active: MaintenanceNotificationDto | null,
  nowMs: number
): boolean {
  if (!active) return false;
  if (active.effectiveForceDisplay || active.isForceDisplay || active.isMandatory) {
    return true;
  }
  const forceFrom = active.forceDisplayFrom
    ? new Date(active.forceDisplayFrom).getTime()
    : null;
  if (forceFrom != null && nowMs >= forceFrom) {
    return true;
  }
  const timeUntilStart = new Date(active.scheduledStartAt).getTime() - nowMs;
  return timeUntilStart >= 0 && timeUntilStart < DAY_MS;
}

export function useMaintenanceNotifications() {
  const { isAuthenticated } = useAuth();
  const [notifications, setNotifications] = useState<MaintenanceNotificationDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [nowMs, setNowMs] = useState(() => Date.now());

  const refresh = useCallback(async () => {
    if (!isAuthenticated) {
      setNotifications([]);
      return;
    }
    setLoading(true);
    try {
      const items = await fetchActiveMaintenanceNotifications();
      setNotifications(Array.isArray(items) ? items : []);
    } catch {
      // Keep last known state on transient network errors.
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated]);

  const activePreview = notifications[0] ?? null;
  const isForcePreview = computeClientForceDisplay(activePreview, nowMs);
  const pollMs = isForcePreview ? FORCE_POLL_MS : POLL_MS;

  useEffect(() => {
    void refresh();
    if (!isAuthenticated) return;
    const id = setInterval(() => void refresh(), pollMs);
    return () => clearInterval(id);
  }, [isAuthenticated, refresh, pollMs]);

  useEffect(() => {
    if (notifications.length === 0) return;
    const id = setInterval(() => setNowMs(Date.now()), TICK_MS);
    return () => clearInterval(id);
  }, [notifications.length]);

  const active = notifications[0] ?? null;
  const isForceDisplay = useMemo(
    () => computeClientForceDisplay(active, nowMs),
    [active, nowMs]
  );

  const countdownLabel = useMemo(() => {
    if (!active) return '';
    const startMs = new Date(active.scheduledStartAt).getTime();
    const endMs = new Date(active.scheduledEndAt).getTime();
    if (startMs > nowMs) {
      return formatCountdown(Math.floor((startMs - nowMs) / 1000));
    }
    if (endMs > nowMs) {
      return formatCountdown(Math.floor((endMs - nowMs) / 1000));
    }
    return formatCountdown(0);
  }, [active, nowMs]);

  const dismissNotification = useCallback(async (id: string) => {
    try {
      await acknowledgeMaintenanceNotification(id, { dismiss: true, markRead: true });
      setNotifications((prev) => prev.filter((n) => n.id !== id));
    } catch {
      // Ignore; next poll will reconcile.
    }
  }, []);

  return {
    notifications,
    activeNotification: active,
    isForceDisplay,
    canDismiss: Boolean(active?.canDismiss) && !isForceDisplay,
    countdownLabel,
    loading,
    dismissNotification,
    refresh,
  };
}
