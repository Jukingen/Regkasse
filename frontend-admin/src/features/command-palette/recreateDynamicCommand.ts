import type { AppRouterInstance } from 'next/dist/shared/lib/app-router-context.shared-runtime';

import type { RecentCommandSnapshot } from '@/features/command-palette/recentCommands';
import type { CommandItem } from '@/features/command-palette/types';

/** Rebuild palette actions for API-driven recent entries (not in static catalog). */
export function recreateDynamicCommandItem(
  snapshot: RecentCommandSnapshot,
  router: AppRouterInstance,
  closePalette: () => void
): CommandItem | null {
  const go = (href: string) => {
    closePalette();
    router.push(href);
  };

  if (snapshot.id.startsWith('user:')) {
    const userId = snapshot.id.slice('user:'.length);
    if (!userId) return null;
    return {
      id: snapshot.id,
      type: 'user',
      label: snapshot.label,
      description: snapshot.description,
      group: 'Recent',
      keywords: [userId, snapshot.label],
      action: () => go(`/admin/users?userId=${encodeURIComponent(userId)}`),
    };
  }

  if (snapshot.id.startsWith('receipt:')) {
    const receiptId = snapshot.id.slice('receipt:'.length);
    if (!receiptId) return null;
    return {
      id: snapshot.id,
      type: 'receipt',
      label: snapshot.label,
      description: snapshot.description,
      group: 'Recent',
      keywords: [receiptId, snapshot.label],
      action: () => go(`/receipts/${encodeURIComponent(receiptId)}`),
    };
  }

  if (snapshot.id.startsWith('register:')) {
    const registerId = snapshot.id.slice('register:'.length);
    if (!registerId) return null;
    return {
      id: snapshot.id,
      type: 'register',
      label: snapshot.label,
      description: snapshot.description,
      group: 'Recent',
      keywords: [registerId, snapshot.label],
      action: () => go(`/kassenverwaltung?registerId=${encodeURIComponent(registerId)}`),
    };
  }

  return null;
}
