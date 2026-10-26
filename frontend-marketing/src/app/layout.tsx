import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'Regkasse',
  description: 'Die Kasse für jede Branche. RKSV-konform, mandantenfähig, in Minuten startklar.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="de">
      <body>{children}</body>
    </html>
  );
}
