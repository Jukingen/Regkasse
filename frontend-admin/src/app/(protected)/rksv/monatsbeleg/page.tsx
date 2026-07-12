import { redirect } from 'next/navigation';

/** Deep-link alias for Sonderbeleg Monatsbeleg focus panel. */
export default function RksvMonatsbelegRedirectPage() {
    redirect('/rksv/sonderbelege?focus=monatsbeleg');
}
