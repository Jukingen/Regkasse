import { useMemo } from 'react';

import { usePosStatusOverview } from '../contexts/PosStatusOverviewContext';
import {
  isRksvDemoEnvironment,
  type RksvEnvironmentStatusDto,
} from '../services/api/rksvEnvironmentTypes';

export type RksvStatusQuery = {
  data: RksvEnvironmentStatusDto | null;
  isLoading: boolean;
  isDemo: boolean;
};

/** RKSV deployment environment from GET /api/pos/status/overview → rksvEnvironment. */
export function useRksvStatus(): RksvStatusQuery {
  const { overview, loading } = usePosStatusOverview();
  const data = overview?.rksvEnvironment ?? null;

  return useMemo(
    () => ({
      data,
      isLoading: loading,
      isDemo: isRksvDemoEnvironment(data),
    }),
    [data, loading]
  );
}
