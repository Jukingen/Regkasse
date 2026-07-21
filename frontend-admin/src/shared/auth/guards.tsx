'use client';

import { Spin } from 'antd';
import { useRouter } from 'next/navigation';
import { FC, ReactNode, useEffect } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { technicalConsole } from '@/shared/dev/technicalConsole';

interface GuardProps {
  children: ReactNode;
}

/**
 * Protects routes that require authentication.
 * Redirects to /login if not authenticated.
 */
export const AuthGuard: FC<GuardProps> = ({ children }) => {
  const { isAuthenticated, isLoadingAuth, user } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoadingAuth && !isAuthenticated) {
      technicalConsole.devLog('[AuthGuard] not authenticated, redirecting to /login');
      router.replace('/login');
    }
  }, [isAuthenticated, isLoadingAuth, router]);

  if (isLoadingAuth) {
    return (
      <div
        style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}
      >
        <Spin size="large" description="Authenticating..." />
      </div>
    );
  }

  if (!isAuthenticated) {
    return null; // Render nothing while redirecting
  }

  return <>{children}</>;
};

/**
 * Protects routes that are only for guests (e.g. Login).
 * Redirects to /dashboard if already authenticated.
 */
export const GuestGuard: FC<GuardProps> = ({ children }) => {
  const { isAuthenticated, isLoadingAuth } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoadingAuth && isAuthenticated) {
      technicalConsole.devLog('[GuestGuard] already authenticated, redirecting to /dashboard');
      router.replace('/dashboard');
    }
  }, [isAuthenticated, isLoadingAuth, router]);

  if (isLoadingAuth) {
    return (
      <div
        style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}
      >
        <Spin size="large" description="Checking session..." />
      </div>
    );
  }

  if (isAuthenticated) {
    return null; // Render nothing while redirecting
  }

  return <>{children}</>;
};
