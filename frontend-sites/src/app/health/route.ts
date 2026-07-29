/** Sites uptime probe (public). Used by Docker HEALTHCHECK / Prometheus blackbox. */
export const dynamic = 'force-dynamic';

export async function GET() {
  return Response.json(
    {
      status: 'ok',
      service: 'frontend-sites',
      ts: new Date().toISOString(),
    },
    {
      status: 200,
      headers: { 'Cache-Control': 'no-store' },
    },
  );
}
