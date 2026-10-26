import type { Metadata } from 'next';
import { absoluteUrl, siteUrl } from '@/seo/site';

const title = 'Regkasse — Die Kasse für jede Branche';
const description =
  'RKSV-konforme Registrierkasse für Gastronomie, Handel, Handwerk und mehr. Mandantenfähig, in Minuten startklar.';

export function buildMetadata(): Metadata {
  const base = siteUrl();
  return {
    metadataBase: new URL(`${base}/`),
    title: {
      default: title,
      template: '%s — Regkasse',
    },
    description,
    alternates: { canonical: '/' },
    openGraph: {
      title,
      description,
      url: absoluteUrl('/'),
      siteName: 'Regkasse',
      locale: 'de_AT',
      type: 'website',
      images: [{ url: '/og-image.png', width: 1200, height: 630, alt: title }],
    },
    twitter: {
      card: 'summary_large_image',
      title,
      description,
      images: ['/og-image.png'],
    },
    robots: { index: true, follow: true },
  };
}

export function legalMetadata(pageTitle: string, path: string): Metadata {
  return {
    title: pageTitle,
    alternates: { canonical: path },
    robots: { index: true, follow: true },
  };
}

export function organizationJsonLd(): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'Organization',
    name: 'Regkasse',
    url: absoluteUrl('/'),
    logo: absoluteUrl('/og-image.png'),
  };
}

export function productJsonLd(): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: 'Regkasse',
    description,
    brand: { '@type': 'Brand', name: 'Regkasse' },
    offers: [
      {
        '@type': 'Offer',
        name: 'Starter',
        price: '29',
        priceCurrency: 'EUR',
        url: absoluteUrl('/#preise'),
      },
      {
        '@type': 'Offer',
        name: 'Professional',
        price: '79',
        priceCurrency: 'EUR',
        url: absoluteUrl('/#preise'),
      },
    ],
  };
}

export function jsonLdScript(): string {
  return JSON.stringify([organizationJsonLd(), productJsonLd()]).replace(/</g, '\\u003c');
}
