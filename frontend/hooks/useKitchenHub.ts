import { useEffect, useRef, useState } from 'react';

import type { KitchenOrder } from '../services/api/kitchenOrderService';
import { listKitchenOrders } from '../services/api/kitchenOrderService';
import {
  getKitchenHubState,
  subscribeKitchenHub,
  type KitchenHubConnectionState,
} from '../services/kitchenHubClient';
import { syncKitchenPendingFromOrders } from '../services/kitchenPendingStore';

export function useKitchenHub(options: {
  enabled?: boolean;
  onCreated?: (order: KitchenOrder) => void;
  onUpdated?: (order: KitchenOrder) => void;
}): KitchenHubConnectionState {
  const enabled = options.enabled !== false;
  const onCreatedRef = useRef(options.onCreated);
  const onUpdatedRef = useRef(options.onUpdated);
  onCreatedRef.current = options.onCreated;
  onUpdatedRef.current = options.onUpdated;
  const [state, setState] = useState<KitchenHubConnectionState>(getKitchenHubState);

  useEffect(() => {
    if (!enabled) {
      setState('disconnected');
      return;
    }

    return subscribeKitchenHub({
      onCreated: (order) => onCreatedRef.current?.(order),
      onUpdated: (order) => onUpdatedRef.current?.(order),
      onStateChange: setState,
    });
  }, [enabled]);

  return state;
}

/** Keeps the KDS tab badge live even when the kitchen screen is not focused. */
export function KitchenHubBridge({ enabled }: { enabled: boolean }): null {
  useKitchenHub({ enabled });

  useEffect(() => {
    if (!enabled) return;
    let cancelled = false;
    void listKitchenOrders()
      .then((rows) => {
        if (!cancelled) syncKitchenPendingFromOrders(rows);
      })
      .catch(() => {
        if (!cancelled) syncKitchenPendingFromOrders([]);
      });
    return () => {
      cancelled = true;
    };
  }, [enabled]);

  return null;
}
