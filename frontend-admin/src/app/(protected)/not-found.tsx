import { NotFoundAccessView } from '@/shared/auth/NotFoundAccessView';

/** Shown inside the protected admin shell when `notFound()` is called under `(protected)`. */
export default function ProtectedNotFound() {
  return <NotFoundAccessView compact />;
}
