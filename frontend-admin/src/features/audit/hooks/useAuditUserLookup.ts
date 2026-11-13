'use client';

import { useMutation } from '@tanstack/react-query';
import { useCallback, useState } from 'react';

import type { UserInfo } from '@/api/generated/model';
import { getUsersList } from '@/features/users/api/usersGateway';

export type AuditUserLookupResult = {
  id: string;
  userName: string;
  email: string;
  displayName: string;
  role: string;
};

function mapUser(u: UserInfo): AuditUserLookupResult | null {
  const id = u.id?.trim();
  if (!id) return null;
  const displayName =
    `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() ||
    u.userName?.trim() ||
    u.email?.trim() ||
    id;
  return {
    id,
    userName: u.userName?.trim() || '—',
    email: u.email?.trim() || '—',
    displayName,
    role: u.role?.trim() || '—',
  };
}

/**
 * Quick user lookup for audit filters — searches User Management list by name/email/id.
 * Does not replace free-text audit `search`; use result to set `userId` precisely.
 */
export function useAuditUserLookup() {
  const [result, setResult] = useState<AuditUserLookupResult | null>(null);
  const [matches, setMatches] = useState<AuditUserLookupResult[]>([]);
  const [hasSearched, setHasSearched] = useState(false);

  const mutation = useMutation({
    mutationFn: async (term: string) => {
      const query = term.trim();
      if (!query) return [] as AuditUserLookupResult[];
      const response = await getUsersList({ query, page: 1, pageSize: 10 });
      return (response.items ?? [])
        .map(mapUser)
        .filter((u): u is AuditUserLookupResult => u != null);
    },
    onSuccess: (items) => {
      setHasSearched(true);
      setMatches(items);
      setResult(items[0] ?? null);
    },
    onError: () => {
      setHasSearched(true);
      setMatches([]);
      setResult(null);
    },
  });

  const lookup = useCallback(
    (term: string) => {
      const trimmed = term.trim();
      if (!trimmed) {
        setMatches([]);
        setResult(null);
        setHasSearched(false);
        return;
      }
      mutation.mutate(trimmed);
    },
    [mutation]
  );

  const clear = useCallback(() => {
    setMatches([]);
    setResult(null);
    setHasSearched(false);
    mutation.reset();
  }, [mutation]);

  return {
    lookup,
    clear,
    result,
    matches,
    hasSearched,
    isLookingUp: mutation.isPending,
    isError: mutation.isError,
    error: mutation.error,
  };
}
