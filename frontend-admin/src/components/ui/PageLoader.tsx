'use client';

import { useEffect, useState } from 'react';
import { usePathname, useSearchParams } from 'next/navigation';

/**
 * Thin top bar shown briefly on client-side route changes (App Router).
 * Mount inside `<Suspense>` when used from a layout that does not already suspend.
 */
export function PageLoader() {
    const pathname = usePathname();
    const searchParams = useSearchParams();
    const searchKey = searchParams.toString();
    const [active, setActive] = useState(false);

    useEffect(() => {
        setActive(true);
        const timer = window.setTimeout(() => setActive(false), 400);
        return () => window.clearTimeout(timer);
    }, [pathname, searchKey]);

    if (!active) return null;

    return (
        <div
            role="progressbar"
            aria-hidden
            className="fa-page-loader"
            style={{
                position: 'fixed',
                top: 0,
                left: 0,
                right: 0,
                height: 3,
                zIndex: 9999,
                overflow: 'hidden',
                pointerEvents: 'none',
                background: 'var(--ant-color-fill-quaternary, #f0f0f0)',
            }}
        >
            <div
                style={{
                    height: '100%',
                    width: '40%',
                    background: 'var(--ant-color-primary, #1677ff)',
                    animation: 'fa-page-loader-slide 0.7s ease-in-out infinite',
                }}
            />
        </div>
    );
}
