import { redirect } from 'next/navigation';

/** Alias route — staff performance lives at /reporting/staff. */
export default function AuditLogsStaffPage() {
  redirect('/reporting/staff');
}
