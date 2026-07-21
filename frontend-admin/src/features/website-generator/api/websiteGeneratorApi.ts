import { AXIOS_INSTANCE, customInstance } from '@/lib/axios';

export type WebsiteTemplate = {
  id: string;
  name: string;
  description: string;
  previewImage: string;
};

export type AppType = 'Pwa' | 'Native' | 0 | 1;

export type GenerateWebsiteResponse = {
  succeeded: boolean;
  code?: string | null;
  error?: string | null;
  url?: string | null;
  templateId?: string | null;
  templateName?: string | null;
};

export type GenerateMobileAppResponse = {
  succeeded: boolean;
  code?: string | null;
  error?: string | null;
  url?: string | null;
  appType?: AppType | null;
};

export type PreviewWebsiteInput = {
  templateId?: string;
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

export type PreviewWebsiteResponse = {
  succeeded: boolean;
  code?: string | null;
  error?: string | null;
  html?: string | null;
  css?: string | null;
  js?: string | null;
  templateId?: string | null;
  templateName?: string | null;
  logoUrl?: string | null;
  menuItemCount?: number;
  categoryCount?: number;
};

type WebsiteTemplateApi = {
  id?: string;
  Id?: string;
  name?: string;
  Name?: string;
  description?: string;
  Description?: string;
  previewImage?: string;
  PreviewImage?: string;
};

type GenerateWebsiteApi = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string | null;
  Code?: string | null;
  error?: string | null;
  Error?: string | null;
  url?: string | null;
  Url?: string | null;
  templateId?: string | null;
  TemplateId?: string | null;
  templateName?: string | null;
  TemplateName?: string | null;
};

type GenerateMobileApi = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string | null;
  Code?: string | null;
  error?: string | null;
  Error?: string | null;
  url?: string | null;
  Url?: string | null;
  appType?: AppType | null;
  AppType?: AppType | null;
};

type PreviewWebsiteApi = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string | null;
  Code?: string | null;
  error?: string | null;
  Error?: string | null;
  html?: string | null;
  Html?: string | null;
  css?: string | null;
  Css?: string | null;
  js?: string | null;
  Js?: string | null;
  templateId?: string | null;
  TemplateId?: string | null;
  templateName?: string | null;
  TemplateName?: string | null;
  logoUrl?: string | null;
  LogoUrl?: string | null;
  menuItemCount?: number;
  MenuItemCount?: number;
  categoryCount?: number;
  CategoryCount?: number;
};

function mapTemplate(dto: WebsiteTemplateApi): WebsiteTemplate {
  return {
    id: dto.id ?? dto.Id ?? '',
    name: dto.name ?? dto.Name ?? '',
    description: dto.description ?? dto.Description ?? '',
    previewImage: dto.previewImage ?? dto.PreviewImage ?? '',
  };
}

function mapWebsite(dto: GenerateWebsiteApi): GenerateWebsiteResponse {
  return {
    succeeded: dto.succeeded ?? dto.Succeeded ?? false,
    code: dto.code ?? dto.Code,
    error: dto.error ?? dto.Error,
    url: dto.url ?? dto.Url,
    templateId: dto.templateId ?? dto.TemplateId,
    templateName: dto.templateName ?? dto.TemplateName,
  };
}

function mapMobile(dto: GenerateMobileApi): GenerateMobileAppResponse {
  return {
    succeeded: dto.succeeded ?? dto.Succeeded ?? false,
    code: dto.code ?? dto.Code,
    error: dto.error ?? dto.Error,
    url: dto.url ?? dto.Url,
    appType: dto.appType ?? dto.AppType,
  };
}

function mapPreview(dto: PreviewWebsiteApi): PreviewWebsiteResponse {
  return {
    succeeded: dto.succeeded ?? dto.Succeeded ?? false,
    code: dto.code ?? dto.Code,
    error: dto.error ?? dto.Error,
    html: dto.html ?? dto.Html,
    css: dto.css ?? dto.Css,
    js: dto.js ?? dto.Js,
    templateId: dto.templateId ?? dto.TemplateId,
    templateName: dto.templateName ?? dto.TemplateName,
    logoUrl: dto.logoUrl ?? dto.LogoUrl,
    menuItemCount: dto.menuItemCount ?? dto.MenuItemCount ?? 0,
    categoryCount: dto.categoryCount ?? dto.CategoryCount ?? 0,
  };
}

/** Build a blob: URL for iframe preview from generator HTML/CSS/JS. Caller must revoke. */
export function buildWebsitePreviewBlobUrl(html: string, css: string, js: string): string {
  const withCss = html.replace(
    /<link\s+rel=["']stylesheet["']\s+href=["']styles\.css["']\s*\/?>/i,
    `<style>${css}</style>`
  );
  const withJs = withCss.replace(
    /<script\s+src=["']app\.js["'][^>]*>\s*<\/script>/i,
    `<script>${js}</script>`
  );
  return URL.createObjectURL(new Blob([withJs], { type: 'text/html;charset=utf-8' }));
}

export async function previewWebsite(input: PreviewWebsiteInput): Promise<PreviewWebsiteResponse> {
  const res = await customInstance<PreviewWebsiteApi>({
    url: '/api/admin/website/preview',
    method: 'POST',
    data: {
      templateId: input.templateId ?? 'modern',
      ...(input.tenantId ? { tenantId: input.tenantId } : {}),
      primaryColor: input.primaryColor,
      secondaryColor: input.secondaryColor,
      backgroundColor: input.backgroundColor,
      textColor: input.textColor,
      fontFamily: input.fontFamily,
      logoUrl: input.logoUrl,
      faviconUrl: input.faviconUrl,
      pages: input.pages,
      features: input.features,
      customCss: input.customCss,
      customJs: input.customJs,
    },
  });
  return mapPreview(res ?? {});
}

export async function fetchWebsiteTemplates(): Promise<WebsiteTemplate[]> {
  const res = await customInstance<WebsiteTemplateApi[]>({
    url: '/api/admin/website/templates',
    method: 'GET',
  });
  return (res ?? []).map(mapTemplate).filter((t) => t.id.length > 0);
}

export async function generateWebsite(
  templateId: string,
  tenantId?: string
): Promise<GenerateWebsiteResponse> {
  const res = await customInstance<GenerateWebsiteApi>({
    url: '/api/admin/website/generate',
    method: 'POST',
    data: {
      templateId,
      ...(tenantId ? { tenantId } : {}),
    },
  });
  return mapWebsite(res ?? {});
}

export async function generateMobileApp(
  appType: 'Pwa' | 'Native',
  tenantId?: string
): Promise<GenerateMobileAppResponse> {
  const res = await customInstance<GenerateMobileApi>({
    url: '/api/admin/website/mobile/generate',
    method: 'POST',
    data: {
      appType,
      ...(tenantId ? { tenantId } : {}),
    },
  });
  return mapMobile(res ?? {});
}

/** Downloads a tenant app ZIP (PWA or Native Expo source + instructions). */
export async function downloadTenantAppPackage(opts?: {
  appType?: 'Pwa' | 'Native';
  tenantId?: string;
}): Promise<{ fileName: string }> {
  const res = await AXIOS_INSTANCE.post<Blob>(
    '/api/admin/website/mobile/package',
    {
      appType: opts?.appType ?? 'Native',
      ...(opts?.tenantId ? { tenantId: opts.tenantId } : {}),
    },
    { responseType: 'blob' }
  );

  const disposition = res.headers['content-disposition'] as string | undefined;
  const match = disposition?.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/i);
  const rawName = match?.[1]?.replace(/['"]/g, '').trim();
  const fileName = rawName && rawName.length > 0 ? rawName : 'app-package.zip';

  const url = URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
  return { fileName };
}

export type ServicePricing = {
  serviceId: string;
  name: string;
  type: 'website' | 'app' | string;
  tier: string;
  priceMonthly: number;
  priceYearly: number;
  features: string[];
  currency: string;
};

type ServicePricingApi = {
  serviceId?: string;
  ServiceId?: string;
  name?: string;
  Name?: string;
  type?: string;
  Type?: string;
  tier?: string;
  Tier?: string;
  priceMonthly?: number;
  PriceMonthly?: number;
  priceYearly?: number;
  PriceYearly?: number;
  features?: string[];
  Features?: string[];
  currency?: string;
  Currency?: string;
};

function mapPricing(dto: ServicePricingApi): ServicePricing {
  return {
    serviceId: dto.serviceId ?? dto.ServiceId ?? '',
    name: dto.name ?? dto.Name ?? '',
    type: dto.type ?? dto.Type ?? '',
    tier: dto.tier ?? dto.Tier ?? '',
    priceMonthly: dto.priceMonthly ?? dto.PriceMonthly ?? 0,
    priceYearly: dto.priceYearly ?? dto.PriceYearly ?? 0,
    features: dto.features ?? dto.Features ?? [],
    currency: dto.currency ?? dto.Currency ?? 'EUR',
  };
}

export async function fetchDigitalServicePricing(type?: string): Promise<ServicePricing[]> {
  const res = await customInstance<ServicePricingApi[]>({
    url: '/api/admin/website/pricing',
    method: 'GET',
    params: type ? { type } : undefined,
  });
  return (res ?? []).map(mapPricing).filter((p) => p.serviceId.length > 0);
}
