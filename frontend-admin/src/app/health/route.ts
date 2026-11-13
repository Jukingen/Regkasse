import { NextResponse } from 'next/server';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

/**
 * Lightweight uptime probe (public).
 * Prefer this URL for external uptime monitors (UptimeRobot, Pingdom, k8s probe).
 */
export async function GET() {
  return NextResponse.json(
    {
      status: 'ok',
      service: 'frontend-admin',
      ts: new Date().toISOString(),
    },
    {
      status: 200,
      headers: {
        'Cache-Control': 'no-store',
      },
    },
  );
}
