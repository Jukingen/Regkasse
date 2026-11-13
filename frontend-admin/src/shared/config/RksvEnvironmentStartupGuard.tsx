'use client';

/**
 * App-startup RKSV public-env check.
 * Logs once when NEXT_PUBLIC_RKSV_ENVIRONMENT is missing or invalid (dev-friendly).
 */
import { useEffect } from 'react';

import {
  parseStrictRksvPublicEnvironment,
  warnRksvPublicEnvironmentInConsole,
} from '@/shared/config/rksvEnvironment';

export function RksvEnvironmentStartupGuard() {
  useEffect(() => {
    warnRksvPublicEnvironmentInConsole(parseStrictRksvPublicEnvironment());
  }, []);

  return null;
}
