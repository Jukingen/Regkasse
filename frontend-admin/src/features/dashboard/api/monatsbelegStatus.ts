/**
 * Re-exports Orval RKSV Monatsbeleg types/clients (OpenAPI: /api/rksv/monatsbeleg/*).
 * @see backend/swagger.json — regenerate via `node scripts/generate-backend-openapi.mjs` + `npm run generate:api`
 */
export type {
    MissingMonth,
    MonatsbelegRegisterStatusItemDto,
    MonatsbelegStatusDto,
} from '@/api/generated/model';
export {
    getApiRksvMonatsbelegStatusCashRegisterId as getMonatsbelegStatus,
    getApiRksvMonatsbelegStatusOverview as getMonatsbelegStatusOverview,
    getGetApiRksvMonatsbelegStatusOverviewQueryKey,
} from '@/api/generated/rksv/rksv';
