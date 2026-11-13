import { useIdleTimer } from 'react-native-idle-timer-detection';

export type NativeIdleTimerOptions = {
  timeout: number;
  onAction: () => void;
};

/** Native idle pan-responder. Web uses `useNativeIdleTimer.web.ts`. */
export function useNativeIdleTimer(options: NativeIdleTimerOptions) {
  return useIdleTimer({
    timeout: options.timeout,
    onAction: options.onAction,
  });
}
