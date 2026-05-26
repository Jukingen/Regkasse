type AdminRoutePathResolver = string | ((context: AdminTargetContext) => string);

export type AdminTarget =
  | 'licenseOverview'
  | 'licenseExtend'
  | 'tenantUsers'
  | 'cashRegisters'
  | 'rksvSonderbelege'
  | 'monatsbericht'
  | 'jahresbericht'
  | 'tagesbericht'
  | 'userManagement'
  | 'tenantManagement';

export interface AdminTargetContext {
  machineHash?: string | null;
  intent?: 'extend' | 'renew' | 'upgrade';
  tenantId?: string | null;
  tenantSlug?: string | null;
  returnTo?: string | null;
  forcePlatformHost?: boolean;
}

type AdminRouteDefinition = {
  path: AdminRoutePathResolver;
  requiresTenant: boolean;
  allowedWithoutTenant: boolean;
  allowedForSuperAdmin: boolean;
};

export const ADMIN_ROUTES: Record<AdminTarget, AdminRouteDefinition> = {
  licenseOverview: {
    path: '/admin/license',
    requiresTenant: false,
    allowedWithoutTenant: true,
    allowedForSuperAdmin: true,
  },
  licenseExtend: {
    path: '/admin/license',
    requiresTenant: false,
    allowedWithoutTenant: true,
    allowedForSuperAdmin: true,
  },
  tenantUsers: {
    path: (context) => {
      const tenantId = context.tenantId?.trim();
      if (!tenantId) {
        throw new Error('tenantId is required for tenantUsers');
      }
      return `/admin/tenants/${encodeURIComponent(tenantId)}/users`;
    },
    requiresTenant: true,
    allowedWithoutTenant: false,
    allowedForSuperAdmin: true,
  },
  cashRegisters: {
    path: '/kassenverwaltung',
    requiresTenant: true,
    allowedWithoutTenant: false,
    allowedForSuperAdmin: true,
  },
  rksvSonderbelege: {
    path: '/rksv/sonderbelege',
    requiresTenant: true,
    allowedWithoutTenant: false,
    allowedForSuperAdmin: true,
  },
  monatsbericht: {
    path: '/reporting/monatsbericht',
    requiresTenant: true,
    allowedWithoutTenant: false,
    allowedForSuperAdmin: true,
  },
  jahresbericht: {
    path: '/reporting/jahresbericht',
    requiresTenant: true,
    allowedWithoutTenant: false,
    allowedForSuperAdmin: true,
  },
  tagesbericht: {
    path: '/reporting/tagesbericht',
    requiresTenant: true,
    allowedWithoutTenant: false,
    allowedForSuperAdmin: true,
  },
  userManagement: {
    path: '/users',
    requiresTenant: true,
    allowedWithoutTenant: false,
    allowedForSuperAdmin: true,
  },
  tenantManagement: {
    path: '/admin/tenants',
    requiresTenant: false,
    allowedWithoutTenant: true,
    allowedForSuperAdmin: true,
  },
};

function trimEnv(value: string | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed && trimmed.length > 0 ? trimmed : undefined;
}

function normalizeBaseUrl(url: string): string {
  const parsed = new URL(url);
  parsed.pathname = parsed.pathname.replace(/\/+$/, '');
  parsed.search = '';
  parsed.hash = '';
  return parsed.toString().replace(/\/$/, '');
}

function normalizeAbsoluteTargetUrl(url: string): string {
  const parsed = new URL(url);
  parsed.pathname = parsed.pathname.replace(/\/+$/, '');
  parsed.hash = '';
  return parsed.toString();
}

function getRoute(target: AdminTarget): AdminRouteDefinition {
  const route = ADMIN_ROUTES[target];
  if (!route) {
    throw new Error(`Unknown admin target: ${target}`);
  }
  return route;
}

function buildAdminPath(target: AdminTarget, context?: AdminTargetContext): string {
  const route = getRoute(target);
  if (typeof route.path === 'function') {
    if (!context) {
      throw new Error(`Context required for ${target}`);
    }
    return route.path(context);
  }
  return route.path;
}

function getExplicitTargetUrl(target: AdminTarget): string | undefined {
  if (target !== 'licenseExtend') {
    return undefined;
  }

  const explicit = trimEnv(process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL);
  if (!explicit) {
    return undefined;
  }

  return normalizeAbsoluteTargetUrl(explicit);
}

export function getAdminBaseUrl(): string {
  const explicit = trimEnv(process.env.EXPO_PUBLIC_ADMIN_BASE_URL);
  if (explicit) {
    return normalizeBaseUrl(explicit);
  }

  if (process.env.NODE_ENV !== 'production') {
    return 'http://admin.regkasse.local:3000';
  }

  return 'https://admin.regkasse.at';
}

export function buildAdminUrl(target: AdminTarget, context?: AdminTargetContext): string {
  const explicitTargetUrl = getExplicitTargetUrl(target);
  const baseUrl = explicitTargetUrl ?? getAdminBaseUrl();
  const path = explicitTargetUrl ? '' : buildAdminPath(target, context);

  const url = new URL(baseUrl);
  if (path) {
    url.pathname = path.startsWith('/') ? path : `/${path}`;
  }

  if (target === 'licenseExtend') {
    url.searchParams.set('intent', context?.intent ?? 'extend');
    const machineHash = context?.machineHash?.trim();
    if (machineHash) {
      url.searchParams.set('machineHash', machineHash);
    }
  }

  const returnTo = context?.returnTo?.trim();
  if (returnTo) {
    url.searchParams.set('returnTo', returnTo);
  }

  return url.toString();
}

export function requiresTenant(target: AdminTarget): boolean {
  return getRoute(target).requiresTenant;
}

export function allowedOnPlatformHost(target: AdminTarget): boolean {
  const route = getRoute(target);
  return route.allowedWithoutTenant || route.allowedForSuperAdmin;
}

export function isAdminTargetAvailable(target: AdminTarget, context?: AdminTargetContext): boolean {
  try {
    return Boolean(buildAdminUrl(target, context));
  } catch {
    return false;
  }
}
