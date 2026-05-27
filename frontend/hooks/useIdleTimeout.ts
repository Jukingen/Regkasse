import { useCallback, useEffect, useRef } from 'react';
import { AppState, Platform, type AppStateStatus } from 'react-native';
import { useIdleTimer } from 'react-native-idle-timer-detection';

export type IdleTimeoutConfig = {
    timeoutMinutes: number;
    warningBeforeMinutes: number;
    onWarning: () => void;
    onTimeout: () => void;
    enabled?: boolean;
};

export type UseIdleTimeoutResult = {
    /** Resets idle + warning timers (e.g. "Continue session"). */
    reset: () => void;
    /** Spread on root `View` for native touch detection. */
    panHandlers: Record<string, unknown>;
};

/**
 * POS idle logout: dual-phase warning + timeout.
 * Native: touch via `react-native-idle-timer-detection` pan handlers.
 * Web: document events. Timers pause while app is in background.
 */
export function useIdleTimeout({
    timeoutMinutes,
    warningBeforeMinutes,
    onWarning,
    onTimeout,
    enabled = true,
}: IdleTimeoutConfig): UseIdleTimeoutResult {
    const onWarningRef = useRef(onWarning);
    const onTimeoutRef = useRef(onTimeout);
    const warningShownRef = useRef(false);
    const scheduleRef = useRef<() => void>(() => {});

    useEffect(() => {
        onWarningRef.current = onWarning;
        onTimeoutRef.current = onTimeout;
    }, [onWarning, onTimeout]);

    useEffect(() => {
        if (!enabled || timeoutMinutes <= 0) return;

        const timeoutMs = timeoutMinutes * 60 * 1000;
        const warningMs = Math.max(0, (timeoutMinutes - Math.max(0, warningBeforeMinutes)) * 60 * 1000);

        let timeoutId: ReturnType<typeof setTimeout> | undefined;
        let warningId: ReturnType<typeof setTimeout> | undefined;
        let appStateSub: { remove: () => void } | undefined;

        const clearAll = () => {
            if (timeoutId) clearTimeout(timeoutId);
            if (warningId) clearTimeout(warningId);
            timeoutId = undefined;
            warningId = undefined;
        };

        const schedule = () => {
            clearAll();
            warningShownRef.current = false;

            if (warningBeforeMinutes > 0 && warningMs < timeoutMs) {
                warningId = setTimeout(() => {
                    if (!warningShownRef.current) {
                        warningShownRef.current = true;
                        onWarningRef.current();
                    }
                }, warningMs);
            }

            timeoutId = setTimeout(() => {
                onTimeoutRef.current();
            }, timeoutMs);
        };

        scheduleRef.current = schedule;

        const onActivity = () => schedule();

        const cleanups: Array<() => void> = [];

        if (Platform.OS === 'web' && typeof document !== 'undefined') {
            const events = ['touchstart', 'mousedown', 'keydown', 'scroll', 'wheel'] as const;
            events.forEach((ev) => {
                document.addEventListener(ev, onActivity, { passive: true });
                cleanups.push(() => document.removeEventListener(ev, onActivity));
            });
        }

        const handleAppState = (next: AppStateStatus) => {
            if (next === 'active') {
                schedule();
            } else {
                clearAll();
            }
        };

        appStateSub = AppState.addEventListener('change', handleAppState);
        schedule();

        return () => {
            clearAll();
            cleanups.forEach((fn) => fn());
            appStateSub?.remove();
        };
    }, [enabled, timeoutMinutes, warningBeforeMinutes]);

    const idleTouch = useIdleTimer({
        timeout: Math.max(60, timeoutMinutes * 60),
        onAction: () => {
            if (enabled) scheduleRef.current();
        },
    });

    const reset = useCallback(() => {
        scheduleRef.current();
        idleTouch.reset();
    }, [idleTouch]);

    const panHandlers =
        Platform.OS === 'web' ? {} : (idleTouch.panResponder.panHandlers as Record<string, unknown>);

    return { reset, panHandlers };
}
