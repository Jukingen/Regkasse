'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type AdminSessionListParams,
  fetchAdminSessions,
  fetchAdminUserSessions,
  forceLogoutUser,
  logoutBulkAdminSessions,
  terminateAdminSession,
  terminateAllAdminSessions,
  terminateAllUserSessions,
} from '@/api/manual/adminSessions';

export const adminSessionsQueryKey = ['admin', 'sessions'] as const;

export function adminSessionsListQueryKey(params: AdminSessionListParams) {
  return [...adminSessionsQueryKey, 'list', params] as const;
}

export function adminUserSessionsQueryKey(userId: string) {
  return ['admin', 'sessions', 'user', userId] as const;
}

export function useAdminSessions(enabled: boolean, params: AdminSessionListParams = {}) {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: adminSessionsListQueryKey(params),
    queryFn: ({ signal }) => fetchAdminSessions(params, signal),
    enabled,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: adminSessionsQueryKey });
  };

  const terminateOne = useMutation({
    mutationFn: (sessionId: string) => terminateAdminSession(sessionId),
    onSuccess: invalidate,
  });

  const terminateAll = useMutation({
    mutationFn: () => terminateAllAdminSessions(),
    onSuccess: invalidate,
  });

  const terminateBulk = useMutation({
    mutationFn: (sessionIds: string[]) => logoutBulkAdminSessions(sessionIds),
    onSuccess: invalidate,
  });

  return {
    sessions: query.data ?? [],
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
    terminateOne,
    terminateAll,
    terminateBulk,
  };
}

export function useAdminUserSessionActions(userId: string, enabled: boolean) {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: adminUserSessionsQueryKey(userId),
    queryFn: ({ signal }) => fetchAdminUserSessions(userId, signal),
    enabled: enabled && !!userId,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: adminSessionsQueryKey });
    void queryClient.invalidateQueries({ queryKey: adminUserSessionsQueryKey(userId) });
  };

  const terminateAll = useMutation({
    mutationFn: () => terminateAllUserSessions(userId),
    onSuccess: invalidate,
  });

  const forceLogout = useMutation({
    mutationFn: () => forceLogoutUser(userId),
    onSuccess: invalidate,
  });

  return {
    sessions: query.data ?? [],
    isLoading: query.isLoading,
    terminateAll,
    forceLogout,
  };
}
