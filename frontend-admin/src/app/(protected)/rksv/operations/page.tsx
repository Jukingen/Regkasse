import { redirect } from 'next/navigation';

/** Legacy menu/bookmark path: sidebar selected key is /rksv/operations; hub lives at /rksv. */
export default function RksvOperationsLegacyRedirectPage() {
  redirect('/rksv');
}
