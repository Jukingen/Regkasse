export type PublicTenantProfile = {
  slug: string;
  displayName: string;
  description?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  logoUrl?: string | null;
  primaryColor: string;
  accentColor: string;
  /** Web/App only — never gates POS. */
  acceptingOnlineOrders: boolean;
  restaurantIsOpen: boolean;
  orderStatusMessage: string;
};

/** Dedicated website status (`GET /api/sites/{slug}/status`) — customer surfaces only. */
export type WebsiteStatus = {
  isOpen: boolean;
  canOrder: boolean;
  message: string;
  openTime?: string | null;
  closeTime?: string | null;
  /** True when today has a WorkingHours.SpecialDays override. */
  isSpecial?: boolean;
};

/** Today's special-day override (`GET /api/sites/{slug}/status/special`). */
export type WebsiteSpecialDay = {
  isSpecial: boolean;
  isClosed: boolean;
  message?: string | null;
  openTime?: string | null;
  closeTime?: string | null;
  date?: string | null;
};

export type PublicTenantMenuItem = {
  id: string;
  name: string;
  categoryId?: string | null;
  categoryName?: string | null;
  price: number;
  imageUrl?: string | null;
  description?: string | null;
};

export type PublicTenantCategory = {
  id: string;
  name: string;
  color?: string | null;
  sortOrder: number;
};

export type PublicTenantMenu = {
  slug: string;
  currency: string;
  categories: PublicTenantCategory[];
  items: PublicTenantMenuItem[];
};

function apiBase(): string {
  const raw = process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5184';
  return raw.replace(/\/$/, '');
}

async function getJson<T>(
  path: string,
  init?: { cache?: RequestCache; next?: { revalidate?: number } },
): Promise<T | null> {
  try {
    const res = await fetch(`${apiBase()}${path}`, {
      cache: init?.cache,
      next: init?.next ?? (init?.cache ? undefined : { revalidate: 60 }),
    });
    if (res.status === 404) return null;
    if (!res.ok) return null;
    return (await res.json()) as T;
  } catch {
    return null;
  }
}

type TenantApi = {
  slug?: string;
  displayName?: string;
  description?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  logoUrl?: string | null;
  primaryColor?: string;
  accentColor?: string;
  acceptingOnlineOrders?: boolean;
  AcceptingOnlineOrders?: boolean;
  restaurantIsOpen?: boolean;
  RestaurantIsOpen?: boolean;
  orderStatusMessage?: string;
  OrderStatusMessage?: string;
};

type MenuApi = {
  slug?: string;
  currency?: string;
  categories?: Array<{
    id?: string;
    name?: string;
    color?: string | null;
    sortOrder?: number;
  }>;
  items?: Array<{
    id?: string;
    name?: string;
    categoryId?: string | null;
    categoryName?: string | null;
    price?: number;
    imageUrl?: string | null;
    description?: string | null;
  }>;
};

export async function fetchPublicTenant(slug: string): Promise<PublicTenantProfile | null> {
  const dto = await getJson<TenantApi>(`/api/public/tenants/${encodeURIComponent(slug)}`);
  if (!dto?.slug) return null;
  const accepting =
    dto.acceptingOnlineOrders === true || dto.AcceptingOnlineOrders === true;
  return {
    slug: dto.slug,
    displayName: dto.displayName ?? dto.slug,
    description: dto.description,
    phone: dto.phone,
    email: dto.email,
    address: dto.address,
    logoUrl: dto.logoUrl,
    primaryColor: dto.primaryColor ?? '#0f172a',
    accentColor: dto.accentColor ?? '#38bdf8',
    acceptingOnlineOrders: accepting,
    restaurantIsOpen: dto.restaurantIsOpen === true || dto.RestaurantIsOpen === true,
    orderStatusMessage:
      dto.orderStatusMessage?.trim() ||
      dto.OrderStatusMessage?.trim() ||
      (accepting ? 'Online-Bestellung möglich' : 'Heute geschlossen'),
  };
}

type WebsiteStatusApi = {
  isOpen?: boolean;
  IsOpen?: boolean;
  canOrder?: boolean;
  CanOrder?: boolean;
  message?: string;
  Message?: string;
  openTime?: string | null;
  OpenTime?: string | null;
  closeTime?: string | null;
  CloseTime?: string | null;
  isSpecial?: boolean;
  IsSpecial?: boolean;
};

type WebsiteSpecialDayApi = {
  isSpecial?: boolean;
  IsSpecial?: boolean;
  isClosed?: boolean;
  IsClosed?: boolean;
  message?: string | null;
  Message?: string | null;
  openTime?: string | null;
  OpenTime?: string | null;
  closeTime?: string | null;
  CloseTime?: string | null;
  date?: string | null;
  Date?: string | null;
};

function mapWebsiteStatus(dto: WebsiteStatusApi | null): WebsiteStatus | null {
  if (!dto) return null;
  const canOrder = dto.canOrder === true || dto.CanOrder === true;
  return {
    isOpen: dto.isOpen === true || dto.IsOpen === true,
    canOrder,
    message:
      dto.message?.trim() ||
      dto.Message?.trim() ||
      (canOrder ? 'Online-Bestellung möglich' : 'Heute geschlossen'),
    openTime: dto.openTime ?? dto.OpenTime ?? null,
    closeTime: dto.closeTime ?? dto.CloseTime ?? null,
    isSpecial: dto.isSpecial === true || dto.IsSpecial === true,
  };
}

function mapWebsiteSpecialDay(dto: WebsiteSpecialDayApi | null): WebsiteSpecialDay | null {
  if (!dto) return null;
  return {
    isSpecial: dto.isSpecial === true || dto.IsSpecial === true,
    isClosed: dto.isClosed === true || dto.IsClosed === true,
    message: dto.message?.trim() || dto.Message?.trim() || null,
    openTime: dto.openTime ?? dto.OpenTime ?? null,
    closeTime: dto.closeTime ?? dto.CloseTime ?? null,
    date: dto.date ?? dto.Date ?? null,
  };
}

/** Live working-hours status for tenant websites / apps (never used by POS/FA). */
export async function fetchWebsiteStatus(slug: string): Promise<WebsiteStatus | null> {
  const dto = await getJson<WebsiteStatusApi>(
    `/api/sites/${encodeURIComponent(slug)}/status`,
  );
  return mapWebsiteStatus(dto);
}

/** Client-side poll (no Next data cache) — website/app only. */
export async function fetchWebsiteStatusLive(slug: string): Promise<WebsiteStatus | null> {
  const dto = await getJson<WebsiteStatusApi>(
    `/api/sites/${encodeURIComponent(slug)}/status`,
    { cache: 'no-store' },
  );
  return mapWebsiteStatus(dto);
}

/** Today's special-day override (WorkingHours.SpecialDays JSON). */
export async function fetchWebsiteSpecialDayLive(
  slug: string,
): Promise<WebsiteSpecialDay | null> {
  const dto = await getJson<WebsiteSpecialDayApi>(
    `/api/sites/${encodeURIComponent(slug)}/status/special`,
    { cache: 'no-store' },
  );
  return mapWebsiteSpecialDay(dto);
}

export async function fetchPublicMenu(slug: string): Promise<PublicTenantMenu | null> {
  const dto = await getJson<MenuApi>(`/api/public/tenants/${encodeURIComponent(slug)}/menu`);
  if (!dto?.slug) return null;
  return {
    slug: dto.slug,
    currency: dto.currency ?? 'EUR',
    categories: (dto.categories ?? []).map((c) => ({
      id: c.id ?? '',
      name: c.name ?? '',
      color: c.color,
      sortOrder: c.sortOrder ?? 0,
    })),
    items: (dto.items ?? []).map((i) => ({
      id: i.id ?? '',
      name: i.name ?? '',
      categoryId: i.categoryId,
      categoryName: i.categoryName,
      price: i.price ?? 0,
      imageUrl: i.imageUrl,
      description: i.description,
    })),
  };
}
