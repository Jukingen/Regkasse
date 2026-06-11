import { redirect } from 'next/navigation';

/** Legacy path — canonical DEP test UI lives under /admin/rksv/dep-export. */
export default function RksvDepExportRedirectPage() {
    redirect('/admin/rksv/dep-export');
}
