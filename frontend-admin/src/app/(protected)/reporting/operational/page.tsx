import { redirect } from 'next/navigation';

/** Alias for operative POS payment reports (`/reporting`). */
export default function ReportingOperationalRedirectPage() {
  redirect('/reporting');
}
