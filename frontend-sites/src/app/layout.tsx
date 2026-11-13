import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'Regkasse Sites',
  description: 'Multi-tenant restaurant websites',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="de">
      <body>{children}</body>
    </html>
  );
}
