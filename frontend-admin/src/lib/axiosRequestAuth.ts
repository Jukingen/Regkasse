/**
 * Pure helpers for axios request auth / tenant headers.
 * Kept separate from the singleton instance so unit tests need no network.
 */
import { AxiosHeaders, type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios';

import { TENANT_HTTP_HEADER } from '@/features/auth/services/tenantStorage';

export type AxiosRequestAuthInput = {
  tenantSlug: string | null | undefined;
  accessToken: string | null | undefined;
  acceptLanguage: string;
  /** Extra headers (e.g. CSRF) applied after auth/tenant. */
  extraHeaders?: Record<string, string>;
  /**
   * Development only: also set `?tenant=` when the slug is present and not already set.
   * Production must not inject this query param.
   */
  injectDevTenantQuery?: boolean;
};

/**
 * Sets Authorization, X-Tenant-Id, Accept-Language (and optional extras) via AxiosHeaders.
 * Mutates and returns `config` for interceptor chaining.
 */
export function applyAxiosRequestAuthHeaders<T extends AxiosRequestConfig>(
  config: T,
  input: AxiosRequestAuthInput
): T {
  const headers =
    config.headers instanceof AxiosHeaders
      ? AxiosHeaders.from(config.headers)
      : new AxiosHeaders(config.headers as Record<string, string> | undefined);

  headers.set('Accept-Language', input.acceptLanguage);

  const tenantSlug = input.tenantSlug?.trim();
  if (tenantSlug) {
    headers.set(TENANT_HTTP_HEADER, tenantSlug);

    if (input.injectDevTenantQuery) {
      const params = config.params ?? {};
      if (typeof params === 'object' && params !== null && !Array.isArray(params)) {
        const record = params as Record<string, unknown>;
        if (record.tenant == null) {
          config.params = { ...record, tenant: tenantSlug };
        }
      } else if (config.params == null) {
        config.params = { tenant: tenantSlug };
      }
    }
  }

  const token = input.accessToken?.trim();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  if (input.extraHeaders) {
    for (const [key, value] of Object.entries(input.extraHeaders)) {
      headers.set(key, value);
    }
  }

  config.headers = headers;
  return config;
}

/** Read a header value from AxiosHeaders or a plain bag (tests / diagnostics). */
export function readAxiosHeader(
  headers: AxiosRequestConfig['headers'] | InternalAxiosRequestConfig['headers'] | undefined,
  name: string
): string | undefined {
  if (!headers) return undefined;
  if (headers instanceof AxiosHeaders) {
    const value = headers.get(name);
    if (value == null) return undefined;
    return Array.isArray(value) ? value.join(', ') : String(value);
  }
  const bag = headers as Record<string, unknown>;
  const direct = bag[name] ?? bag[name.toLowerCase()];
  if (direct == null) return undefined;
  return Array.isArray(direct) ? direct.map(String).join(', ') : String(direct);
}
