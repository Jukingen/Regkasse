'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';

import {
  type ActiveSession,
  fetchMySessions,
  terminateAllOtherSessions,
  terminateSession,
} from '@/features/auth/api/sessionsApi';

export const USER_SESSIONS_QUERY_KEY = ['user', 'sessions'] as const;

export function useSessions() {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: USER_SESSIONS_QUERY_KEY,
    queryFn: fetchMySessions,
    staleTime: 30_000,
  });

  const invalidate = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: USER_SESSIONS_QUERY_KEY });
  }, [queryClient]);

  const revokeMutation = useMutation({
    mutationFn: (sessionId: string) => terminateSession(sessionId),
    onSuccess: () => invalidate(),
  });

  const revokeOthersMutation = useMutation({
    mutationFn: () => terminateAllOtherSessions(),
    onSuccess: () => invalidate(),
  });

  const revoke = useCallback(
    async (sessionId: string) => {
      await revokeMutation.mutateAsync(sessionId);
    },
    [revokeMutation]
  );

  const revokeOthers = useCallback(async () => {
    return revokeOthersMutation.mutateAsync();
  }, [revokeOthersMutation]);

  return {
    data: (query.data ?? []) as ActiveSession[],
    sessions: (query.data ?? []) as ActiveSession[],
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    refetch: query.refetch,
    invalidate,
    revoke,
    revokeOthers,
    isRevoking: revokeMutation.isPending || revokeOthersMutation.isPending,
  };
}
