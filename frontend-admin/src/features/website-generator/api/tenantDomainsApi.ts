import { AXIOS_INSTANCE, customInstance } from '@/lib/axios';

export type TenantDomain = {
  id: string;
  tenantId: string;
  domain: string;
  subdomain: string;
  isVerified: boolean;
  verificationToken?: string | null;
  verifiedAt?: string | null;
  isActive: boolean;
  isPrimary: boolean;
  createdAt: string;
};

export type TenantDomainPublishResult = {
  succeeded: boolean;
  code?: string | null;
  error?: string | null;
  url?: string | null;
  customDomain?: string | null;
};

type DomainApi = {
  id?: string;
  Id?: string;
  tenantId?: string;
  TenantId?: string;
  domain?: string;
  Domain?: string;
  subdomain?: string;
  Subdomain?: string;
  isVerified?: boolean;
  IsVerified?: boolean;
  verificationToken?: string | null;
  VerificationToken?: string | null;
  verifiedAt?: string | null;
  VerifiedAt?: string | null;
  isActive?: boolean;
  IsActive?: boolean;
  isPrimary?: boolean;
  IsPrimary?: boolean;
  createdAt?: string;
  CreatedAt?: string;
};

type PublishApi = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string | null;
  Code?: string | null;
  error?: string | null;
  Error?: string | null;
  url?: string | null;
  Url?: string | null;
  customDomain?: string | null;
  CustomDomain?: string | null;
};

function mapDomain(dto: DomainApi): TenantDomain {
  return {
    id: dto.id ?? dto.Id ?? '',
    tenantId: dto.tenantId ?? dto.TenantId ?? '',
    domain: dto.domain ?? dto.Domain ?? '',
    subdomain: dto.subdomain ?? dto.Subdomain ?? '',
    isVerified: dto.isVerified ?? dto.IsVerified ?? false,
    verificationToken: dto.verificationToken ?? dto.VerificationToken,
    verifiedAt: dto.verifiedAt ?? dto.VerifiedAt,
    isActive: dto.isActive ?? dto.IsActive ?? true,
    isPrimary: dto.isPrimary ?? dto.IsPrimary ?? false,
    createdAt: dto.createdAt ?? dto.CreatedAt ?? '',
  };
}

function mapPublish(dto: PublishApi): TenantDomainPublishResult {
  return {
    succeeded: dto.succeeded ?? dto.Succeeded ?? false,
    code: dto.code ?? dto.Code,
    error: dto.error ?? dto.Error,
    url: dto.url ?? dto.Url,
    customDomain: dto.customDomain ?? dto.CustomDomain,
  };
}

function withTenantId(tenantId?: string): { tenantId?: string } {
  return tenantId ? { tenantId } : {};
}

export async function fetchTenantDomains(tenantId?: string): Promise<TenantDomain[]> {
  const res = await customInstance<DomainApi[]>({
    url: '/api/admin/tenant-domains',
    method: 'GET',
    params: withTenantId(tenantId),
  });
  return (res ?? []).map(mapDomain).filter((d) => d.id.length > 0);
}

export async function addTenantDomain(domain: string, tenantId?: string): Promise<TenantDomain> {
  const res = await customInstance<DomainApi>({
    url: '/api/admin/tenant-domains',
    method: 'POST',
    data: { domain, ...withTenantId(tenantId) },
  });
  return mapDomain(res ?? {});
}

export async function verifyTenantDomain(
  id: string,
  token: string,
  tenantId?: string
): Promise<TenantDomain> {
  const res = await customInstance<DomainApi>({
    url: `/api/admin/tenant-domains/${id}/verify`,
    method: 'POST',
    params: withTenantId(tenantId),
    data: { token, ...withTenantId(tenantId) },
  });
  return mapDomain(res ?? {});
}

export async function setTenantDomainWebsiteEnabled(
  id: string,
  enabled: boolean,
  tenantId?: string
): Promise<TenantDomain> {
  const res = await customInstance<DomainApi>({
    url: `/api/admin/tenant-domains/${id}/website-enabled`,
    method: 'PUT',
    params: withTenantId(tenantId),
    data: { enabled, ...withTenantId(tenantId) },
  });
  return mapDomain(res ?? {});
}

export async function removeTenantDomain(id: string, tenantId?: string): Promise<TenantDomain> {
  const res = await customInstance<DomainApi>({
    url: `/api/admin/tenant-domains/${id}`,
    method: 'DELETE',
    params: withTenantId(tenantId),
  });
  return mapDomain(res ?? {});
}

export async function publishTenantSite(
  templateId?: string,
  tenantId?: string
): Promise<TenantDomainPublishResult> {
  const res = await customInstance<PublishApi>({
    url: '/api/admin/tenant-domains/publish',
    method: 'POST',
    data: { templateId, ...withTenantId(tenantId) },
  });
  return mapPublish(res ?? {});
}

/** Downloads a static website ZIP for the tenant custom domain. */
export async function downloadTenantWebsitePackage(opts?: {
  domain?: string;
  templateId?: string;
  tenantId?: string;
}): Promise<{ fileName: string }> {
  const res = await AXIOS_INSTANCE.post<Blob>(
    '/api/admin/tenant-domains/generate-package',
    {
      domain: opts?.domain,
      templateId: opts?.templateId ?? 'modern',
      ...withTenantId(opts?.tenantId),
    },
    { responseType: 'blob' }
  );

  const disposition = res.headers['content-disposition'] as string | undefined;
  const match = disposition?.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/i);
  const rawName = match?.[1]?.replace(/['"]/g, '').trim();
  const fileName = rawName && rawName.length > 0 ? rawName : 'website-package.zip';

  const url = URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
  return { fileName };
}
