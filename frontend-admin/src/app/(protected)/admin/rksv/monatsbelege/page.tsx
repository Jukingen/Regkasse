import { redirect } from 'next/navigation';

/** Super Admin /admin prefix alias for the mandant Monatsbeleg list. */
export default function AdminRksvMonatsbelegeRedirectPage() {
  redirect('/rksv/monatsbelege');
}
