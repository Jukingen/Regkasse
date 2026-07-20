import { customInstance } from '@/lib/axios';

export type TenantCustomization = {
  id: string;
  tenantId: string;
  type: 'website' | 'app' | string;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  backgroundColor?: string | null;
  textColor?: string | null;
  fontFamily?: string | null;
  logoUrl?: string | null;
  faviconUrl?: string | null;
  pages: string[];
  features: string[];
  customCss?: string | null;
  customJs?: string | null;
  updatedAt: string;
};

export type UpsertTenantCustomizationInput = {
  type: 'website' | 'app';
  tenantId?: string;
  primaryColor?: string;
  secondaryColor?: string;
  backgroundColor?: string;
  textColor?: string;
  fontFamily?: string;
  logoUrl?: string;
  faviconUrl?: string;
  pages?: string[];
  features?: string[];
  customCss?: string;
  customJs?: string;
};

type Api = {
  id?: string;
  Id?: string;
  tenantId?: string;
  TenantId?: string;
  type?: string;
  Type?: string;
  primaryColor?: string | null;
  PrimaryColor?: string | null;
  secondaryColor?: string | null;
  SecondaryColor?: string | null;
  backgroundColor?: string | null;
  BackgroundColor?: string | null;
  textColor?: string | null;
  TextColor?: string | null;
  fontFamily?: string | null;
  FontFamily?: string | null;
  logoUrl?: string | null;
  LogoUrl?: string | null;
  faviconUrl?: string | null;
  FaviconUrl?: string | null;
  pages?: string[];
  Pages?: string[];
  features?: string[];
  Features?: string[];
  customCss?: string | null;
  CustomCss?: string | null;
  customJs?: string | null;
  CustomJs?: string | null;
  updatedAt?: string;
  UpdatedAt?: string;
};

function map(dto: Api): TenantCustomization {
  return {
    id: dto.id ?? dto.Id ?? '',
    tenantId: dto.tenantId ?? dto.TenantId ?? '',
    type: dto.type ?? dto.Type ?? 'website',
    primaryColor: dto.primaryColor ?? dto.PrimaryColor,
    secondaryColor: dto.secondaryColor ?? dto.SecondaryColor,
    backgroundColor: dto.backgroundColor ?? dto.BackgroundColor,
    textColor: dto.textColor ?? dto.TextColor,
    fontFamily: dto.fontFamily ?? dto.FontFamily,
    logoUrl: dto.logoUrl ?? dto.LogoUrl,
    faviconUrl: dto.faviconUrl ?? dto.FaviconUrl,
    pages: dto.pages ?? dto.Pages ?? [],
    features: dto.features ?? dto.Features ?? [],
    customCss: dto.customCss ?? dto.CustomCss,
    customJs: dto.customJs ?? dto.CustomJs,
    updatedAt: dto.updatedAt ?? dto.UpdatedAt ?? '',
  };
}

export async function fetchTenantCustomization(
  type: 'website' | 'app',
  tenantId?: string,
): Promise<TenantCustomization> {
  const res = await customInstance<Api>({
    url: '/api/admin/tenant-customizations',
    method: 'GET',
    params: {
      type,
      ...(tenantId ? { tenantId } : {}),
    },
  });
  return map(res ?? {});
}

export async function upsertTenantCustomization(
  input: UpsertTenantCustomizationInput,
): Promise<TenantCustomization> {
  const res = await customInstance<Api>({
    url: '/api/admin/tenant-customizations',
    method: 'PUT',
    data: input,
  });
  return map(res ?? {});
}
