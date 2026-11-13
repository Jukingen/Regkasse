import { redirect } from 'next/navigation';

/** Legacy billing overview → unified license management. Sales remain at `/admin/billing/sales`. */
export default function AdminBillingOverviewRedirectPage() {
  redirect('/admin/license-management');
}
