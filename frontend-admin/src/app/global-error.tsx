'use client';

/**
 * Root App Router error boundary — captures React render failures in production.
 * Must define its own `<html>` / `<body>` (Next.js requirement for global-error).
 */
import * as Sentry from '@sentry/nextjs';
import { useEffect } from 'react';

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    if (process.env.NODE_ENV === 'production') {
      Sentry.captureException(error, {
        tags: {
          source: 'global-error',
          ...(error.digest ? { digest: error.digest } : {}),
        },
      });
    }
  }, [error]);

  return (
    <html lang="de">
      <body style={{ fontFamily: 'system-ui, sans-serif', padding: 24 }}>
        <h2>Etwas ist schiefgelaufen</h2>
        <p>Die Seite konnte nicht geladen werden. Bitte versuchen Sie es erneut.</p>
        <button type="button" onClick={() => reset()}>
          Erneut versuchen
        </button>
      </body>
    </html>
  );
}
