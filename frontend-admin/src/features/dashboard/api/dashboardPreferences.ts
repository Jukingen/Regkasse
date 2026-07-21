import type {
  DashboardPreferencesResponse,
  DashboardWidgetCatalogItem,
  SaveDashboardPreferencesRequest,
} from '@/features/dashboard/types';
import { customInstance } from '@/lib/axios';

export const dashboardPreferencesQueryKeys = {
  catalog: ['dashboard', 'widgets'] as const,
  preferences: ['dashboard', 'preferences'] as const,
};

export async function fetchDashboardWidgetCatalog(): Promise<DashboardWidgetCatalogItem[]> {
  return customInstance<DashboardWidgetCatalogItem[]>({
    url: '/api/admin/dashboard/widgets',
    method: 'GET',
  });
}

export async function fetchDashboardPreferences(): Promise<DashboardPreferencesResponse> {
  return customInstance<DashboardPreferencesResponse>({
    url: '/api/admin/dashboard/preferences',
    method: 'GET',
  });
}

export async function saveDashboardPreferences(
  body: SaveDashboardPreferencesRequest
): Promise<DashboardPreferencesResponse> {
  return customInstance<DashboardPreferencesResponse>({
    url: '/api/admin/dashboard/preferences',
    method: 'POST',
    data: body,
  });
}
