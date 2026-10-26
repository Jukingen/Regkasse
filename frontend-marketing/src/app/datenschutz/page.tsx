import type { Metadata } from 'next';
import ComingSoonPage from '@/components/ComingSoonPage';
import { legalMetadata } from '@/seo/metadata';

export const metadata: Metadata = legalMetadata('Datenschutz', '/datenschutz');

export default function DatenschutzPage() {
  return <ComingSoonPage title="Datenschutz" />;
}
