/**
 * Legacy route /rksv/operations must keep redirecting to the RKSV hub (/rksv) so bookmarks
 * and menu selected keys stay compatible without serving a duplicate hub.
 */
import { describe, expect, it, vi } from 'vitest';

const redirectMock = vi.fn();

vi.mock('next/navigation', () => ({
  redirect: (url: string) => redirectMock(url),
}));

describe('RksvOperationsLegacyRedirectPage', () => {
  it('redirects to /rksv (hub), not /rksv/operations', async () => {
    const { default: RksvOperationsLegacyRedirectPage } = await import('../page');
    RksvOperationsLegacyRedirectPage();
    expect(redirectMock).toHaveBeenCalledTimes(1);
    expect(redirectMock).toHaveBeenCalledWith('/rksv');
    expect(redirectMock).not.toHaveBeenCalledWith('/rksv/operations');
  });
});
