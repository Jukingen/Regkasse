import { customInstance } from '@/lib/axios';

export type IndustryTemplateSlotDto = {
  key: string;
  displayName: string;
  systemRole: string;
  recommendedPackageSlugs: string[];
  seedStarterUser: boolean;
};

export type IndustryTemplateDto = {
  id: string;
  name: string;
  description: string;
  suggestedDemoImportProfileId?: string | null;
  slots: IndustryTemplateSlotDto[];
};

export type TenantIndustryTemplateDto = {
  tenantId: string;
  industryTemplateId?: string | null;
  template?: IndustryTemplateDto | null;
};

export type SetTenantIndustryTemplateRequest = {
  industryTemplateId?: string | null;
  seedMissingStarters?: boolean;
};

export type SetTenantIndustryTemplateResult = {
  industryTemplateId?: string | null;
  startersCreated: number;
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' ? (value as Record<string, unknown>) : {};
}

function asStringList(value: unknown): string[] {
  return Array.isArray(value) ? value.map((v) => String(v)).filter(Boolean) : [];
}

function mapSlot(raw: unknown): IndustryTemplateSlotDto {
  const row = asRecord(raw);
  return {
    key: String(row.key ?? row.Key ?? ''),
    displayName: String(row.displayName ?? row.DisplayName ?? ''),
    systemRole: String(row.systemRole ?? row.SystemRole ?? ''),
    recommendedPackageSlugs: asStringList(
      row.recommendedPackageSlugs ?? row.RecommendedPackageSlugs
    ),
    seedStarterUser: Boolean(row.seedStarterUser ?? row.SeedStarterUser ?? false),
  };
}

function mapTemplate(raw: unknown): IndustryTemplateDto | null {
  const row = asRecord(raw);
  const id = String(row.id ?? row.Id ?? '');
  if (!id) return null;
  const slotsRaw = row.slots ?? row.Slots ?? [];
  return {
    id,
    name: String(row.name ?? row.Name ?? id),
    description: String(row.description ?? row.Description ?? ''),
    suggestedDemoImportProfileId: (row.suggestedDemoImportProfileId ??
      row.SuggestedDemoImportProfileId ??
      null) as string | null,
    slots: (Array.isArray(slotsRaw) ? slotsRaw : []).map(mapSlot),
  };
}

export async function listIndustryTemplates(): Promise<IndustryTemplateDto[]> {
  const res = await customInstance<unknown[]>({
    url: '/api/admin/industry-templates',
    method: 'GET',
  });
  return (Array.isArray(res) ? res : [])
    .map(mapTemplate)
    .filter((t): t is IndustryTemplateDto => t !== null);
}

export async function getIndustryTemplate(id: string): Promise<IndustryTemplateDto> {
  const res = await customInstance<unknown>({
    url: `/api/admin/industry-templates/${encodeURIComponent(id)}`,
    method: 'GET',
  });
  const mapped = mapTemplate(res);
  if (!mapped) throw new Error('Industry template not found');
  return mapped;
}

export async function getTenantIndustryTemplate(
  tenantId: string
): Promise<TenantIndustryTemplateDto> {
  const res = await customInstance<Record<string, unknown>>({
    url: `/api/admin/industry-templates/tenants/${tenantId}`,
    method: 'GET',
  });
  const templateRaw = res.template ?? res.Template ?? null;
  return {
    tenantId: String(res.tenantId ?? res.TenantId ?? tenantId),
    industryTemplateId: (res.industryTemplateId ?? res.IndustryTemplateId ?? null) as string | null,
    template: templateRaw ? mapTemplate(templateRaw) : null,
  };
}

export async function setTenantIndustryTemplate(
  tenantId: string,
  body: SetTenantIndustryTemplateRequest
): Promise<SetTenantIndustryTemplateResult> {
  const res = await customInstance<Record<string, unknown>>({
    url: `/api/admin/industry-templates/tenants/${tenantId}`,
    method: 'PUT',
    data: body,
  });
  return {
    industryTemplateId: (res.industryTemplateId ?? res.IndustryTemplateId ?? null) as string | null,
    startersCreated: Number(res.startersCreated ?? res.StartersCreated ?? 0),
  };
}
