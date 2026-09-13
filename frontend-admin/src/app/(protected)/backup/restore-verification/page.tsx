'use client';

import { useRouter } from 'next/navigation';
import { useEffect } from 'react';

import { ADMIN_RESTORE_VERIFICATION_PATH } from '@/shared/backupAreaRoutes';

/** Hub alias — canonical list is `/admin/restore-verification`. */
export default function BackupRestoreVerificationRedirectPage() {
  const router = useRouter();
  useEffect(() => {
    router.replace(ADMIN_RESTORE_VERIFICATION_PATH);
  }, [router]);
  return null;
}
