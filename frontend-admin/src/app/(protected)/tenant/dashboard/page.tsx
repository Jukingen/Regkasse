import { redirect } from 'next/navigation';

export default function TenantDashboardRedirectPage() {
  redirect('/tenant/portal');
}
