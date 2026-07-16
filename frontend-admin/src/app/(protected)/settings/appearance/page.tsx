import { redirect } from 'next/navigation';

/** Legacy alias — canonical route is `/settings/personalization`. */
export default function SettingsAppearanceRedirectPage() {
  redirect('/settings/personalization');
}
