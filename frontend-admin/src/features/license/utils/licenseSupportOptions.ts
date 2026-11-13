/** License support contact targets for FA quick-help tiles. */

export const LICENSE_SUPPORT_EMAIL = 'support@regkasse.at';

/** Default Austrian support desk number (display + tel:). Override via env. */
export const LICENSE_SUPPORT_PHONE_DEFAULT = '+43 1 234 5678';

export type LicenseSupportActionId = 'liveChat' | 'ticket' | 'faq' | 'phone';

export type LicenseSupportHrefTarget = {
  href: string;
  kind: 'external' | 'mailto' | 'tel';
};

function trimEnv(value: string | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed && trimmed.length > 0 ? trimmed : null;
}

export function getConfiguredLicenseSupportChatUrl(): string | null {
  return trimEnv(process.env.NEXT_PUBLIC_LICENSE_SUPPORT_CHAT_URL);
}

export function getConfiguredLicenseSupportPhone(): string {
  return trimEnv(process.env.NEXT_PUBLIC_LICENSE_SUPPORT_PHONE) ?? LICENSE_SUPPORT_PHONE_DEFAULT;
}

export function buildLicenseSupportMailto(args: {
  subject: string;
  body: string;
}): string {
  const subject = encodeURIComponent(args.subject);
  const body = encodeURIComponent(args.body);
  return `mailto:${LICENSE_SUPPORT_EMAIL}?subject=${subject}&body=${body}`;
}

export function buildLicenseSupportTelHref(phoneDisplay: string = getConfiguredLicenseSupportPhone()): string {
  const digits = phoneDisplay.replace(/[^\d+]/g, '');
  return `tel:${digits || phoneDisplay.trim()}`;
}

export function resolveLicenseSupportLiveChatTarget(): LicenseSupportHrefTarget {
  const configured = getConfiguredLicenseSupportChatUrl();
  if (configured) {
    if (configured.startsWith('mailto:')) {
      return { href: configured, kind: 'mailto' };
    }
    if (/^https?:\/\//i.test(configured)) {
      return { href: configured, kind: 'external' };
    }
  }
  return {
    href: buildLicenseSupportMailto({
      subject: 'Lizenz-Support Live-Chat Anfrage',
      body: 'Bitte um Rückruf / Chat-Unterstützung zu meiner Regkasse-Lizenz.',
    }),
    kind: 'mailto',
  };
}

export function resolveLicenseSupportTicketTarget(): LicenseSupportHrefTarget {
  return {
    href: buildLicenseSupportMailto({
      subject: 'Lizenz-Support Ticket',
      body: [
        'Betreff: Lizenzproblem',
        '',
        'Mandant / Firma:',
        'Kurzbeschreibung:',
        'Schritte zur Reproduktion:',
        '',
        'Vielen Dank.',
      ].join('\n'),
    }),
    kind: 'mailto',
  };
}

export function resolveLicenseSupportPhoneTarget(): LicenseSupportHrefTarget {
  return {
    href: buildLicenseSupportTelHref(),
    kind: 'tel',
  };
}

export function openLicenseSupportHref(target: LicenseSupportHrefTarget): void {
  if (typeof window === 'undefined') return;
  if (target.kind === 'external') {
    window.open(target.href, '_blank', 'noopener,noreferrer');
    return;
  }
  window.location.assign(target.href);
}
