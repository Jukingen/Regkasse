export type SyncStatusPayload = {
  isSyncing: boolean;
  pendingOrders: number;
  pendingPayments: number;
  lastSyncAt: Date | null;
  nextSyncAt: Date | null;
};

export type SyncProgressPayload = {
  current: number;
  total: number;
};

export type EventMap = {
  'sync:status': SyncStatusPayload;
  'sync:progress': SyncProgressPayload;
  'sync:completed': { synced: number; errors: number };
  'sync:error': Error;
  'sync:warning': { message: string };
  'sync:online': void;
  'sync:offline': void;
  'offline:warning': { hoursRemaining: number };
  'offline:critical': { hoursRemaining: number };
  'offline:expired': { orderIds: string[] };
};

type EventListener<T> = (data: T) => void;

class EventEmitter {
  private listeners = new Map<string, Set<EventListener<EventMap[keyof EventMap]>>>();

  on<T extends keyof EventMap>(event: T, listener: EventListener<EventMap[T]>): void {
    const bucket = this.listeners.get(event) ?? new Set<EventListener<EventMap[keyof EventMap]>>();
    bucket.add(listener as EventListener<EventMap[keyof EventMap]>);
    this.listeners.set(event, bucket);
  }

  off<T extends keyof EventMap>(event: T, listener: EventListener<EventMap[T]>): void {
    this.listeners.get(event)?.delete(listener as EventListener<EventMap[keyof EventMap]>);
  }

  emit<T extends keyof EventMap>(
    ...args: EventMap[T] extends void ? [event: T] : [event: T, data: EventMap[T]]
  ): void {
    const [event, data] = args;
    this.listeners.get(event)?.forEach((listener) => {
      try {
        listener(data as EventMap[typeof event]);
      } catch (error) {
        console.error(`Error in event listener for ${String(event)}:`, error);
      }
    });
  }
}

export const eventEmitter = new EventEmitter();
