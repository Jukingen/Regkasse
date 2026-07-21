import { customInstance } from '@/lib/axios';

/** Updates server-side last-activity for the current JWT session (`sid` claim). */
export async function refreshAuthSession(): Promise<void> {
  await customInstance<{ message?: string }>({
    url: '/api/Auth/refresh-session',
    method: 'POST',
  });
}
