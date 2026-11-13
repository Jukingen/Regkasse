import { redirect } from 'next/navigation';

/** Deep link → legacy Super Admin hub FinanzOnline tab. */
export default function SettingsFinanzOnlinePage() {
  redirect('/settings?tab=finanzonline');
}
