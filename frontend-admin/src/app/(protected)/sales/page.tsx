import { redirect } from 'next/navigation';

/** Legacy/test alias — Verkauf & Vorgänge primary list is receipts. */
export default function SalesRedirectPage() {
    redirect('/receipts');
}
