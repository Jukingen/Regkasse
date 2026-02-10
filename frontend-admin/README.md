# Regkasse Admin Panel

Next.js Admin Panel for Regkasse POS System.
Built with Ant Design, TanStack Query, and Orval.

## Prerequisites

- Node.js 18+
- Backend API running (KasseAPI)

## Setup

1. Install dependencies:
   ```bash
   npm install
   ```

2. Environment Setup:
   Copy `.env.example` to `.env.local` and configure:
   ```env
   NEXT_PUBLIC_API_BASE_URL=https://localhost:7082
   ```

3. Generate API Client:
   If Backend Swagger changes, update `orval.config.ts` path if needed and run:
   ```bash
   npm run generate:api
   ```

## Scripts

- `npm run dev`: Start development server (localhost:3000)
- `npm run build`: Build for production
- `npm start`: Start production server
- `npm run lint`: Run ESLint
- `npm run generate:api`: Generate TypeScript client from Swagger

## Project Structure

- `src/app`: Next.js App Router pages
- `src/api/generated`: Orval generated hooks and models
- `src/features`: Feature-specific components
- `src/lib`: Shared providers (AntD, QueryClient)
- `src/theme`: Ant Design theme configuration

## Architecture

- **Auth**: Uses `/api/Auth/login` (Cookie-based or Token-based).
- **Data Fetching**: TanStack Query via Orval generated hooks.
- **UI**: Ant Design v5 with CSS-in-JS registry for SSR.
