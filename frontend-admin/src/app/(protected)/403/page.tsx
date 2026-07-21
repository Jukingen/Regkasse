import { ForbiddenAccessView } from '@/shared/auth/ForbiddenAccessView';

/** `/403` inside the protected admin shell (sidebar + header remain visible). */
export default function ForbiddenPage() {
  return <ForbiddenAccessView compact />;
}
