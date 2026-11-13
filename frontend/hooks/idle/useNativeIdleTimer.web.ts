export type NativeIdleTimerOptions = {
  timeout: number;
  onAction: () => void;
};

/** Web stub — does not import react-native-idle-timer-detection. */
export function useNativeIdleTimer(_options: NativeIdleTimerOptions) {
  return {
    reset: () => {},
    panResponder: { panHandlers: {} as Record<string, unknown> },
  };
}
