import { redirect } from 'next/navigation';

/** Legacy path — canonical card transactions UI lives under /admin/payments/card-transactions. */
export default function CardTransactionsRedirectPage() {
  redirect('/admin/payments/card-transactions');
}
