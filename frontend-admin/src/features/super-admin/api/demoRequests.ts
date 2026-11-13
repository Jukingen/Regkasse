import { AXIOS_INSTANCE } from '@/lib/axios';

export type DemoRequestListItem = {
  id: string;
  name: string;
  email: string;
  company?: string | null;
  industry: string;
  status: string;
  createdAtUtc: string;
  message?: string | null;
};

export type DemoRequestList = {
  items: DemoRequestListItem[];
  total: number;
  page: number;
  pageSize: number;
};

export async function listDemoRequests(page = 1, pageSize = 20): Promise<DemoRequestList> {
  const { data } = await AXIOS_INSTANCE.get<DemoRequestList>('/api/admin/demo-requests', {
    params: { page, pageSize },
  });
  return data;
}
