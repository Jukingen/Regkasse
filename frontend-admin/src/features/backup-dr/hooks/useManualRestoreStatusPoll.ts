'use client';

import { useQuery } from '@tanstack/react-query';
import {
  getManualRestoreRequest,
  getManualRestoreRequestQueryKey,
  type RestoreRequestStatusDto,
} from '@/features/backup-dr/logic/manualRestoreApi';
import {
  isManualRestoreTerminalStatus,
  shouldPollManualRestoreStatus,
} from '@/features/backup-dr/logic/manualRestorePresentation';

const DEFAULT_POLL_MS = 3000;

export function useManualRestoreStatusPoll(
  requestId: string | null,
  enabled: boolean,
  pollMs = DEFAULT_POLL_MS,
  /** When true, poll until Completed/Failed/Rejected (approval modal). */
  pollUntilTerminal = false,
) {
  return useQuery({
    queryKey: requestId ? getManualRestoreRequestQueryKey(requestId) : ['manual-restore', 'idle'],
    queryFn: () => getManualRestoreRequest(requestId!),
    enabled: Boolean(requestId) && enabled,
    refetchInterval: (query) => {
      const status = (query.state.data as RestoreRequestStatusDto | undefined)?.status;
      const shouldPoll = pollUntilTerminal
        ? !isManualRestoreTerminalStatus(status)
        : shouldPollManualRestoreStatus(status);
      return shouldPoll ? pollMs : false;
    },
    refetchOnWindowFocus: true,
  });
}
