import { redirect } from 'next/navigation';

/** Deep link used in license reminder emails → status dashboard. */
export default function LicenseIndexPage() {
  redirect('/license/dashboard');
}
