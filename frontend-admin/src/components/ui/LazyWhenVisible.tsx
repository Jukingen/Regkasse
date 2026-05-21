'use client';

import React, { useEffect, useRef, useState, type ReactNode } from 'react';

type LazyWhenVisibleProps = {
    children: ReactNode;
    /** When false, never activates the observer (e.g. permission gate). */
    enabled?: boolean;
    rootMargin?: string;
    minHeight?: number;
};

/**
 * Defers heavy children (data hooks in parent) until the block nears the viewport.
 * Parent should gate API `enabled` with the `onVisible` callback state.
 */
export function LazyWhenVisible({
    children,
    enabled = true,
    rootMargin = '120px',
    minHeight = 1,
}: LazyWhenVisibleProps) {
    const ref = useRef<HTMLDivElement>(null);
    const [visible, setVisible] = useState(false);

    useEffect(() => {
        if (!enabled || visible) return;
        const el = ref.current;
        if (!el) return;

        const observer = new IntersectionObserver(
            ([entry]) => {
                if (entry?.isIntersecting) {
                    setVisible(true);
                    observer.disconnect();
                }
            },
            { rootMargin },
        );
        observer.observe(el);
        return () => observer.disconnect();
    }, [enabled, visible, rootMargin]);

    return (
        <div ref={ref} style={{ minHeight }} data-lazy-visible={visible ? 'true' : 'false'}>
            {visible ? children : null}
        </div>
    );
}

/** Hook companion when render-prop pattern is awkward. */
export function useLazyWhenVisible(enabled = true, rootMargin = '120px') {
    const ref = useRef<HTMLDivElement>(null);
    const [visible, setVisible] = useState(false);

    useEffect(() => {
        if (!enabled || visible) return;
        const el = ref.current;
        if (!el) return;

        const observer = new IntersectionObserver(
            ([entry]) => {
                if (entry?.isIntersecting) {
                    setVisible(true);
                    observer.disconnect();
                }
            },
            { rootMargin },
        );
        observer.observe(el);
        return () => observer.disconnect();
    }, [enabled, visible, rootMargin]);

    return { ref, visible };
}
