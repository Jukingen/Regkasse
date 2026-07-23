/**
 * RKSV hub landing: task groups and links. Keep hrefs aligned with `src/features/rksv/rksvAdminMenuModel.ts`.
 * Türkçe: Görev grupları ve bağlantılar; menü modeli ile senkron tutulmalı.
 */

export type RksvHubLinkDef = { href: string; labelKey: string };

export type RksvHubGroupDef = {
  id: string;
  titleKey: string;
  descriptionKey: string;
  links: RksvHubLinkDef[];
};

export const RKSV_HUB_GROUPS: RksvHubGroupDef[] = [
  {
    id: 'daily',
    titleKey: 'rksvHub.groups.daily.title',
    descriptionKey: 'rksvHub.groups.daily.description',
    links: [
      { href: '/rksv', labelKey: 'rksvHub.link.hub' },
      { href: '/rksv/finanz-online-outbox', labelKey: 'rksvHub.link.outbox' },
      { href: '/rksv/finanz-online-queue', labelKey: 'rksvHub.link.queueLegacy' },
    ],
  },
  {
    id: 'investigation',
    titleKey: 'rksvHub.groups.investigation.title',
    descriptionKey: 'rksvHub.groups.investigation.description',
    links: [
      { href: '/rksv/incident', labelKey: 'rksvHub.link.incident' },
      { href: '/rksv/replay-batch', labelKey: 'rksvHub.link.replayBatch' },
      { href: '/rksv/payload-hash-conflicts', labelKey: 'rksvHub.link.payloadHash' },
      { href: '/rksv/verifications', labelKey: 'rksvHub.link.verifications' },
    ],
  },
  {
    id: 'diagnostics',
    titleKey: 'rksvHub.groups.diagnostics.title',
    descriptionKey: 'rksvHub.groups.diagnostics.description',
    links: [
      { href: '/rksv/fiscal-export-diagnostics', labelKey: 'rksvHub.link.fiscalExport' },
      { href: '/admin/rksv/dep-export', labelKey: 'rksvHub.link.depExport' },
      { href: '/rksv/integrity', labelKey: 'rksvHub.link.integrity' },
      { href: '/rksv/compliance', labelKey: 'rksvHub.link.compliance' },
      { href: '/rksv/signature-chain', labelKey: 'rksvHub.link.signatureChain' },
      { href: '/rksv/offline-intent-coverage', labelKey: 'rksvHub.link.offlineCoverage' },
      { href: '/rksv/offline-orders', labelKey: 'rksvHub.link.offlineOrders' },
      { href: '/admin/tse/offline-transactions', labelKey: 'rksvHub.link.offlineTransactions' },
      { href: '/admin/rksv/signature-verify', labelKey: 'rksvHub.link.signatureVerify' },
      { href: '/admin/audit/fiscal-exports', labelKey: 'rksvHub.link.fiscalExportAudit' },
      { href: '/admin/download-history', labelKey: 'rksvHub.link.downloadHistory' },
      { href: '/admin/download-history/analytics', labelKey: 'rksvHub.link.downloadAnalytics' },
    ],
  },
  {
    id: 'config',
    titleKey: 'rksvHub.groups.config.title',
    descriptionKey: 'rksvHub.groups.config.description',
    links: [
      { href: '/rksv/status', labelKey: 'rksvHub.link.status' },
      { href: '/rksv/cmc-certificate', labelKey: 'rksvHub.link.cmcCertificate' },
      { href: '/rksv/finanz-online-operations', labelKey: 'rksvHub.link.finanzOnlineOps' },
    ],
  },
];
