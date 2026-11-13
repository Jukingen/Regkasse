import { redirect } from 'next/navigation';

/** Legacy alias for Storno & Rückerstattung audit hub. */
export default function StornoRedirectPage() {
  redirect('/payments/storno-refund-audit');
}
