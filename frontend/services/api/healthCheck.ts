import axios from 'axios';

import { API_BASE_URL } from './config';

/**
 * Anonymous liveness probe for `GET /api/health` (and `/health` alias).
 * Uses a bare axios call — never the authenticated `apiClient` interceptor —
 * so stale JWTs on the login screen cannot produce 401s.
 */
export async function checkHealth(): Promise<unknown> {
  try {
    const apiRoot = API_BASE_URL.replace(/\/api\/?$/, '');
    const response = await axios.get(`${apiRoot}/api/health`, {
      timeout: 5000,
      headers: {
        Accept: 'text/plain, application/json',
      },
      // Ensure no Authorization header is sent even if defaults exist.
      transformRequest: [
        (data, headers) => {
          if (headers && typeof headers === 'object') {
            delete (headers as Record<string, unknown>).Authorization;
            delete (headers as Record<string, unknown>).authorization;
          }
          return data;
        },
      ],
    });
    return response.data;
  } catch {
    // Silent fail — callers treat null as offline / unreachable.
    return null;
  }
}
