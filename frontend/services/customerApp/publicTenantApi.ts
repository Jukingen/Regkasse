import { apiClient } from '../api/config';
import { API_PATHS } from '../api/apiPaths';

export type CustomerTenantProfile = {
  slug: string;
  displayName: string;
  description?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  logoUrl?: string | null;
  primaryColor: string;
  accentColor: string;
};

export type CustomerTenantMenuItem = {
  id: string;
  name: string;
  categoryName?: string | null;
  price: number;
  description?: string | null;
};

export type CustomerTenantMenu = {
  slug: string;
  currency: string;
  items: CustomerTenantMenuItem[];
};

type ProfileApi = {
  slug?: string;
  displayName?: string;
  description?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  logoUrl?: string | null;
  primaryColor?: string;
  accentColor?: string;
};

type MenuApi = {
  slug?: string;
  currency?: string;
  items?: Array<{
    id?: string;
    name?: string;
    categoryName?: string | null;
    price?: number;
    description?: string | null;
  }>;
};

export async function loadTenant(slug: string): Promise<CustomerTenantProfile> {
  const data = await apiClient.get<ProfileApi>(API_PATHS.PUBLIC_TENANTS.BY_SLUG(slug));
  if (!data?.slug) {
    throw Object.assign(new Error('not_found'), { response: { status: 404 } });
  }
  return {
    slug: data.slug,
    displayName: data.displayName ?? data.slug,
    description: data.description,
    phone: data.phone,
    email: data.email,
    address: data.address,
    logoUrl: data.logoUrl,
    primaryColor: data.primaryColor ?? '#0f172a',
    accentColor: data.accentColor ?? '#38bdf8',
  };
}

export async function loadTenantMenu(slug: string): Promise<CustomerTenantMenu> {
  const data = await apiClient.get<MenuApi>(API_PATHS.PUBLIC_TENANTS.MENU(slug));
  return {
    slug: data?.slug ?? slug,
    currency: data?.currency ?? 'EUR',
    items: (data?.items ?? []).map((i) => ({
      id: i.id ?? '',
      name: i.name ?? '',
      categoryName: i.categoryName,
      price: i.price ?? 0,
      description: i.description,
    })),
  };
}
