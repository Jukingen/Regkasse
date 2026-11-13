/** Prefill for the Super Admin "Demo-Mandant anlegen" wizard. */
export function buildDemoTenantDefaults(now: Date = new Date()): { name: string; slug: string } {
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  const hours = String(now.getHours()).padStart(2, '0');
  const minutes = String(now.getMinutes()).padStart(2, '0');
  return {
    name: `Demo ${year}-${month}-${day}`,
    slug: `demo-${year}${month}${day}${hours}${minutes}`,
  };
}
