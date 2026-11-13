'use client';

import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { useEffect, useRef, useState } from 'react';

import { asFiskalyBatchProgressEvent, type FiskalyBatchProgressEvent } from '@/features/fiskaly/api/fiskalyBatch';
import {
  FISKALY_OPERATION_STATUS_HUB_PATH,
  type FiskalyOperationStatusEvent,
} from '@/features/fiskaly/fiskalyOperationStatus';

function getApiBaseUrl(): string {
  const configured = process.env.NEXT_PUBLIC_API_BASE_URL;
  if (configured) return configured.replace(/\/$/, '');
  if (process.env.NODE_ENV === 'development') return 'http://localhost:5184';
  throw new Error('NEXT_PUBLIC_API_BASE_URL is required.');
}

function asStatusEvent(payload: unknown): FiskalyOperationStatusEvent | null {
  if (payload == null || typeof payload !== 'object') return null;
  const row = payload as Record<string, unknown>;
  if (typeof row.id !== 'string' && typeof row.id !== 'number') return null;
  return {
    id: String(row.id),
    tenantId: String(row.tenantId ?? ''),
    operationType: String(row.operationType ?? ''),
    status: String(row.status ?? ''),
    progressPercent: typeof row.progressPercent === 'number' ? row.progressPercent : 0,
    cashRegisterId: String(row.cashRegisterId ?? ''),
    cashRegisterName: typeof row.cashRegisterName === 'string' ? row.cashRegisterName : null,
    receiptNumber: typeof row.receiptNumber === 'string' ? row.receiptNumber : null,
    errorCode: typeof row.errorCode === 'string' ? row.errorCode : null,
    errorMessage: typeof row.errorMessage === 'string' ? row.errorMessage : null,
    createdAtUtc: String(row.createdAtUtc ?? ''),
    completedAtUtc: typeof row.completedAtUtc === 'string' ? row.completedAtUtc : null,
  };
}

/**
 * Subscribes to Fiskaly operation status via SignalR (HttpOnly cookie).
 * Returns `connected=false` so callers can fall back to 3s polling.
 */
export function useFiskalyOperationStatusLive(options: {
  enabled?: boolean;
  onEvent?: (evt: FiskalyOperationStatusEvent) => void;
  onBatchProgress?: (evt: FiskalyBatchProgressEvent) => void;
}): { connected: boolean } {
  const enabled = options.enabled !== false;
  const onEventRef = useRef(options.onEvent);
  onEventRef.current = options.onEvent;
  const onBatchProgressRef = useRef(options.onBatchProgress);
  onBatchProgressRef.current = options.onBatchProgress;
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    if (!enabled) {
      setConnected(false);
      return;
    }

    let stopped = false;
    let connection: HubConnection | null = null;

    const start = async () => {
      const hubUrl = `${getApiBaseUrl()}${FISKALY_OPERATION_STATUS_HUB_PATH}`;
      connection = new HubConnectionBuilder()
        .withUrl(hubUrl, { withCredentials: true })
        .withAutomaticReconnect([0, 2000, 5000, 10_000, 20_000])
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on('OperationStatus', (payload: unknown) => {
        const evt = asStatusEvent(payload);
        if (evt) onEventRef.current?.(evt);
      });

      connection.on('BatchProgress', (payload: unknown) => {
        const evt = asFiskalyBatchProgressEvent(payload);
        if (evt) onBatchProgressRef.current?.(evt);
      });

      connection.onreconnecting(() => {
        if (!stopped) setConnected(false);
      });
      connection.onreconnected(() => {
        if (stopped) return;
        void connection?.invoke('Subscribe').then(() => {
          if (!stopped) setConnected(true);
        }).catch(() => {
          if (!stopped) setConnected(false);
        });
      });
      connection.onclose(() => {
        if (!stopped) setConnected(false);
      });

      try {
        await connection.start();
        await connection.invoke('Subscribe');
        if (!stopped) setConnected(true);
      } catch {
        if (!stopped) setConnected(false);
        try {
          await connection.stop();
        } catch {
          // ignore
        }
      }
    };

    void start();

    return () => {
      stopped = true;
      setConnected(false);
      const current = connection;
      connection = null;
      if (current && current.state !== HubConnectionState.Disconnected) {
        void current.stop();
      }
    };
  }, [enabled]);

  return { connected };
}
