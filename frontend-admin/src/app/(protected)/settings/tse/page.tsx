import { redirect } from 'next/navigation';

/** Deep link → legacy Super Admin hub TSE tab. */
export default function SettingsTsePage() {
    redirect('/settings?tab=tse');
}
