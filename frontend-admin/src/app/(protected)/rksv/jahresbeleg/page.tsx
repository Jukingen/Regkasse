import { redirect } from 'next/navigation';

/** Deep-link alias for Sonderbeleg Jahresbeleg focus panel. */
export default function RksvJahresbelegRedirectPage() {
  redirect('/rksv/sonderbelege?focus=jahresbeleg');
}
