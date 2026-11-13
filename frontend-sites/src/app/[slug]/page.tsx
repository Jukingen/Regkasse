/**
 * Shared dynamic tenant website (one Next.js deploy for all tenants).
 * Route: /[slug] — SSR catalog + client TenantWebsite (working-hours order gate).
 * Website/app only — never POS/FA.
 */

import { notFound } from 'next/navigation';
import { TenantWebsite } from '@/components/TenantWebsite';
import { fetchPublicMenu, fetchPublicTenant } from '@/lib/publicApi';

type PageProps = {
  params: Promise<{ slug: string }>;
};

export async function generateMetadata({ params }: PageProps) {
  const { slug } = await params;
  const tenant = await fetchPublicTenant(slug);
  if (!tenant) return { title: 'Nicht gefunden' };
  return {
    title: tenant.displayName,
    description: tenant.description ?? `${tenant.displayName} — Speisekarte`,
  };
}

export default async function TenantWebsitePage({ params }: PageProps) {
  const { slug } = await params;
  const [tenant, menu] = await Promise.all([
    fetchPublicTenant(slug),
    fetchPublicMenu(slug),
  ]);

  if (!tenant || !menu) {
    notFound();
  }

  return <TenantWebsite slug={slug} tenant={tenant} menu={menu} />;
}
