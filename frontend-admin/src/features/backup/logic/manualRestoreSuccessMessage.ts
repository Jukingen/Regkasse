/**
 * Success toast after POST /api/admin/restore/request.
 * Development Super Admin auto-executes → Executing; Staging/Production stays PendingApproval.
 */
export function manualRestoreSuccessMessageKey(status: string | null | undefined): string {
  const normalized = (status ?? '').trim().toLowerCase();
  if (normalized === 'executing' || normalized === 'approved') {
    return 'backupDr.manualRestore.messages.requestExecuting';
  }
  return 'backupDr.manualRestore.messages.requestCreated';
}
