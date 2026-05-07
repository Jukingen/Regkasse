import { redirect } from 'next/navigation';

/** Alias route: main operator overview lives at `/dashboard`. */
export default function AdminDashboardAliasPage() {
    redirect('/dashboard');
}
