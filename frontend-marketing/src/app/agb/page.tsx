import type { Metadata } from 'next';
import ComingSoonPage from '@/components/ComingSoonPage';
import { legalMetadata } from '@/seo/metadata';

export const metadata: Metadata = legalMetadata('AGB', '/agb');

export default function AgbPage() {
  return <ComingSoonPage title="AGB" />;
}
