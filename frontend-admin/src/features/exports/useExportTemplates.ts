'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import {
  deleteExportTemplate,
  getExportTemplateById,
  listAllExportTemplates,
  loadLastUsedExportTemplate,
  markExportTemplateUsed,
  saveExportTemplate,
} from '@/features/exports/exportTemplatesStorage';
import type { ExportTemplate } from '@/features/exports/exportTemplateTypes';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { usePermissions } from '@/shared/auth/usePermissions';
import { getExportTypeById } from '@/features/exports/exportTypeCatalog';

export function useExportTemplates() {
  const { user } = useAuth();
  const { tenant } = useTenant();
  const { hasPermission } = usePermissions();
  const userId = user?.id ?? 'anon';
  const tenantId = tenant?.id ?? 'default';

  const [templates, setTemplates] = useState<ExportTemplate[]>([]);
  const [lastUsedMeta, setLastUsedMeta] = useState<{ templateId: string; usedAt: string } | null>(
    null
  );
  const [hydrated, setHydrated] = useState(false);

  const reload = useCallback(() => {
    setTemplates(listAllExportTemplates({ userId, tenantId }));
    setLastUsedMeta(loadLastUsedExportTemplate(userId));
  }, [userId, tenantId]);

  useEffect(() => {
    reload();
    setHydrated(true);
  }, [reload]);

  const visibleTemplates = useMemo(() => {
    return templates.filter((tmpl) => {
      const typeId =
        tmpl.config.kind === 'dep-export'
          ? 'dep-export'
          : tmpl.config.kind === 'tagesbericht'
            ? 'tagesbericht'
            : 'backup';
      const def = getExportTypeById(typeId);
      if (!def) return true;
      return hasPermission(def.permission);
    });
  }, [templates, hasPermission]);

  const lastUsed = useMemo(() => {
    if (!lastUsedMeta?.templateId) return null;
    return visibleTemplates.find((t) => t.id === lastUsedMeta.templateId) ?? null;
  }, [lastUsedMeta, visibleTemplates]);

  const upsert = useCallback(
    (template: ExportTemplate) => {
      saveExportTemplate(template, { userId, tenantId });
      reload();
    },
    [userId, tenantId, reload]
  );

  const remove = useCallback(
    (template: ExportTemplate) => {
      if (template.isPreset) return;
      deleteExportTemplate(template.id, {
        userId,
        tenantId,
        shared: template.shared,
      });
      reload();
    },
    [userId, tenantId, reload]
  );

  const markUsed = useCallback(
    (templateId: string) => {
      markExportTemplateUsed(userId, templateId);
      setLastUsedMeta({ templateId, usedAt: new Date().toISOString() });
    },
    [userId]
  );

  const getById = useCallback(
    (id: string) => getExportTemplateById(id, { userId, tenantId }),
    [userId, tenantId]
  );

  return {
    hydrated,
    templates: visibleTemplates,
    lastUsed,
    lastUsedAt: lastUsedMeta?.usedAt ?? null,
    upsert,
    remove,
    markUsed,
    getById,
    reload,
    userId,
    userName: user?.userName ?? user?.email ?? null,
  };
}
