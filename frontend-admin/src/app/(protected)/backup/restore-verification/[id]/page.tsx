'use client';

import { useParams, useRouter } from 'next/navigation';
import { useEffect } from 'react';

import { restoreVerificationDetailPath } from '@/shared/backupAreaRoutes';

/** Hub alias — canonical detail is `/admin/restore-verification/{id}`. */
export default function BackupRestoreVerificationDetailRedirectPage() {
  const params = useParams();
  const router = useRouter();
  const id = typeof params?.id === 'string' ? params.id : '';

  useEffect(() => {
    if (id) router.replace(restoreVerificationDetailPath(id));
  }, [id, router]);
  return null;
}
