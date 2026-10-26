import type { Metadata } from 'next';
import ComingSoonPage from '@/components/ComingSoonPage';
import { legalMetadata } from '@/seo/metadata';

export const metadata: Metadata = legalMetadata('Impressum', '/impressum');

export default function ImpressumPage() {
  return <ComingSoonPage title="Impressum" />;
}
