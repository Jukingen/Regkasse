import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

import { API_BASE_URL, axiosInstance } from './api/config';
import { mapKitchenOrder, type KitchenOrder } from './api/kitchenOrderService';
import { sessionManager } from './session/sessionManager';
import { applyKitchenPendingFromOrder } from './kitchenPendingStore';
import { playKitchenNewOrderSound } from './kitchenOrderSound';
import {
  KITCHEN_HUB_CREATED,
  KITCHEN_HUB_ITEM_STATUS_CHANGED,
  KITCHEN_HUB_STATUS_CHANGED,
  kitchenHubReconnectPolicy,
  resolveKitchenHubUrl,
} from './kitchenHubReconnect';

export {
  KITCHEN_HUB_CREATED,
  KITCHEN_HUB_ITEM_STATUS_CHANGED,
  KITCHEN_HUB_PATH,
  KITCHEN_HUB_STATUS_CHANGED,
  kitchenHubReconnectPolicy,
  resolveKitchenHubUrl,
} from './kitchenHubReconnect';

export type KitchenHubConnectionState = 'connected' | 'reconnecting' | 'disconnected';

export type KitchenHubHandlers = {
  onCreated?: (order: KitchenOrder) => void;
  onUpdated?: (order: KitchenOrder) => void;
  onStateChange?: (state: KitchenHubConnectionState) => void;
};

function asRecord(value: unknown): Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

export function mapKitchenHubOrderPayload(value: unknown): KitchenOrder | null {
  const raw = asRecord(value);
  const orderRaw = raw.order ?? raw.Order ?? value;
  const mapped = mapKitchenOrder(orderRaw);
  return mapped.id ? mapped : null;
}

function currentApiBaseUrl(): string {
  return String(axiosInstance.defaults.baseURL || API_BASE_URL || '');
}

const listeners = new Set<KitchenHubHandlers>();
let connection: HubConnection | null = null;
let startPromise: Promise<void> | null = null;
let lastState: KitchenHubConnectionState = 'disconnected';

function emitState(state: KitchenHubConnectionState): void {
  lastState = state;
  listeners.forEach((handler) => handler.onStateChange?.(state));
}

function emitCreated(order: KitchenOrder): void {
  applyKitchenPendingFromOrder(order);
  playKitchenNewOrderSound();
  listeners.forEach((handler) => handler.onCreated?.(order));
}

function emitUpdated(order: KitchenOrder): void {
  applyKitchenPendingFromOrder(order);
  listeners.forEach((handler) => handler.onUpdated?.(order));
}

async function ensureStarted(): Promise<void> {
  if (connection && connection.state !== HubConnectionState.Disconnected) {
    return;
  }
  if (startPromise) {
    await startPromise;
    return;
  }

  const hub = new HubConnectionBuilder()
    .withUrl(resolveKitchenHubUrl(currentApiBaseUrl()), {
      accessTokenFactory: async () => (await sessionManager.getAccessToken()) ?? '',
    })
    .withAutomaticReconnect(kitchenHubReconnectPolicy)
    .configureLogging(LogLevel.Warning)
    .build();

  hub.on(KITCHEN_HUB_CREATED, (payload: unknown) => {
    const order = mapKitchenHubOrderPayload(payload);
    if (order) emitCreated(order);
  });
  hub.on(KITCHEN_HUB_STATUS_CHANGED, (payload: unknown) => {
    const order = mapKitchenHubOrderPayload(payload);
    if (order) emitUpdated(order);
  });
  hub.on(KITCHEN_HUB_ITEM_STATUS_CHANGED, (payload: unknown) => {
    const order = mapKitchenHubOrderPayload(payload);
    if (order) emitUpdated(order);
  });
  hub.onreconnecting(() => emitState('reconnecting'));
  hub.onreconnected(() => {
    void hub.invoke('Subscribe').then(() => emitState('connected')).catch(() => emitState('disconnected'));
  });
  hub.onclose(() => {
    emitState('disconnected');
  });

  connection = hub;
  startPromise = (async () => {
    try {
      await hub.start();
      await hub.invoke('Subscribe');
      emitState('connected');
    } catch {
      emitState('disconnected');
      try {
        await hub.stop();
      } catch {
        // ignore
      }
      if (connection === hub) {
        connection = null;
      }
    } finally {
      startPromise = null;
    }
  })();

  await startPromise;
}

async function stopIfIdle(): Promise<void> {
  if (listeners.size > 0 || !connection) return;
  const current = connection;
  connection = null;
  emitState('disconnected');
  if (current.state !== HubConnectionState.Disconnected) {
    try {
      await current.stop();
    } catch {
      // ignore
    }
  }
}

/**
 * Shared /hubs/kitchen connection. Reconnects with the same delay ladder as FA hubs.
 * First subscriber starts the socket; last unsubscribe stops it.
 */
export function subscribeKitchenHub(handlers: KitchenHubHandlers): () => void {
  listeners.add(handlers);
  handlers.onStateChange?.(lastState);
  void ensureStarted();
  return () => {
    listeners.delete(handlers);
    void stopIfIdle();
  };
}

export function getKitchenHubState(): KitchenHubConnectionState {
  return lastState;
}

/** Test-only: reset singleton listeners/connection without touching a live socket. */
export function resetKitchenHubClientForTests(): void {
  listeners.clear();
  connection = null;
  startPromise = null;
  lastState = 'disconnected';
}
