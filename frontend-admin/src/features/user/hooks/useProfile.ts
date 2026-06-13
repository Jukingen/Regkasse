'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSyncExternalStore } from 'react';

import { AUTH_KEYS } from '@/features/auth/hooks/useAuth';
import { authStorage } from '@/features/auth/services/authStorage';
import { customInstance } from '@/lib/axios';

export const PROFILE_QUERY_KEY = ['user-profile'] as const;

/** GET /api/user/profile — aligned with backend UserProfileDto. */
export type UserProfile = {
  id: string;
  userName: string;
  email?: string | null;
  firstName: string;
  lastName: string;
  role: string;
  employeeNumber?: string | null;
  phoneNumber?: string | null;
};

export type UpdateProfilePayload = {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string | null;
};

type UpdateProfileResponse = {
  message?: string;
};

const emptySubscribe = () => () => {};

async function fetchProfile(): Promise<UserProfile> {
  return customInstance<UserProfile>({
    url: '/api/user/profile',
    method: 'GET',
  });
}

async function updateProfile(payload: UpdateProfilePayload): Promise<UpdateProfileResponse> {
  return customInstance<UpdateProfileResponse>({
    url: '/api/user/profile',
    method: 'PUT',
    data: payload,
  });
}

export function useProfile() {
  const isBrowser = useSyncExternalStore(emptySubscribe, () => true, () => false);
  const hasCredentials = isBrowser && authStorage.hasToken();

  return useQuery({
    queryKey: PROFILE_QUERY_KEY,
    queryFn: fetchProfile,
    enabled: hasCredentials,
    staleTime: 30_000,
  });
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: updateProfile,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: PROFILE_QUERY_KEY }),
        queryClient.invalidateQueries({ queryKey: AUTH_KEYS.user }),
      ]);
    },
  });
}
