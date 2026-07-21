import { redirect } from 'next/navigation';

/** Alias for Benutzer-Aktivität report. */
export default function ReportingUserActivityRedirectPage() {
  redirect('/admin/reports/user-activity');
}
