'use client';

import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';

import { type ActivityDto, connectActivityStream } from '@/api/manual/activityEvents';

const unreadKey = ['admin', 'activities', 'unread-count'] as const;
const listKey = ['admin', 'activities', 'list'] as const;

/**
 * Subscribes to GET /api/admin/activities/stream while enabled.
 * Prepends live activities to local state and refreshes unread count.
 */
export function useActivityStream(enabled: boolean) {
  const queryClient = useQueryClient();
  const [liveItems, setLiveItems] = useState<ActivityDto[]>([]);
  const seenIds = useRef(new Set<string>());

  useEffect(() => {
    if (!enabled) {
      setLiveItems([]);
      seenIds.current.clear();
      return;
    }

    const abort = new AbortController();

    void connectActivityStream(
      {
        onActivity: (activity) => {
          if (seenIds.current.has(activity.id)) {
            return;
          }
          seenIds.current.add(activity.id);
          setLiveItems((prev) => [activity, ...prev].slice(0, 50));
          void queryClient.invalidateQueries({ queryKey: unreadKey });
          void queryClient.invalidateQueries({ queryKey: listKey });
        },
      },
      abort.signal
    ).catch(() => {
      // Stream errors are non-fatal; polling fallback remains active.
    });

    return () => {
      abort.abort();
      setLiveItems([]);
      seenIds.current.clear();
    };
  }, [enabled, queryClient]);

  return { liveItems };
}
