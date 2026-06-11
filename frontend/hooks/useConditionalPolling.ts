import { useEffect, useRef } from 'react';
import { AppState, type AppStateStatus } from 'react-native';

/**
 * Runs `callback` on an interval only while the app is in the foreground (`active`).
 * Invokes `callback` once when the app returns to the foreground.
 */
export function subscribeForegroundPolling(
  callback: () => void,
  intervalMs: number,
  enabled = true,
): () => void {
  if (!enabled) {
    return () => {};
  }

  let intervalId: ReturnType<typeof setInterval> | undefined;

  const stop = () => {
    if (intervalId != null) {
      clearInterval(intervalId);
      intervalId = undefined;
    }
  };

  const start = () => {
    callback();
    stop();
    intervalId = setInterval(callback, intervalMs);
  };

  const handleAppStateChange = (nextAppState: AppStateStatus) => {
    if (nextAppState === 'active') {
      start();
    } else {
      stop();
    }
  };

  if (AppState.currentState === 'active') {
    start();
  }

  const subscription = AppState.addEventListener('change', handleAppStateChange);

  return () => {
    subscription.remove();
    stop();
  };
}

/**
 * Polls only while the app is foregrounded; stops in background to save battery and API traffic.
 */
export function useConditionalPolling(
  callback: () => void,
  intervalMs: number,
  enabled = true,
): void {
  const callbackRef = useRef(callback);
  callbackRef.current = callback;

  useEffect(() => {
    return subscribeForegroundPolling(
      () => callbackRef.current(),
      intervalMs,
      enabled,
    );
  }, [intervalMs, enabled]);
}
