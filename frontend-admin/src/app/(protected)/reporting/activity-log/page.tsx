import { redirect } from 'next/navigation';

/** Legacy route — canonical manager activity log is /audit-logs/activity. */
export default function ReportingActivityLogPage() {
  redirect('/audit-logs/activity');
}
