import { defineConfig } from 'orval';

/**
 * Orval: backend `swagger.json` → generated React Query clients (tags-split).
 * Legacy product/category paths are stripped in `scripts/orval-strip-legacy-paths.cjs`; those APIs stay in
 * `src/api/admin/*` (manual clients). Prefer generated endpoints for anything present in the OpenAPI spec.
 */
export default defineConfig({
    kasse: {
        input: {
            target: '../backend/swagger.json',
            override: {
                transformer: './scripts/orval-strip-legacy-paths.cjs',
            },
        },
        output: {
            mode: 'tags-split',
            target: 'src/api/generated/endpoints.ts',
            schemas: 'src/api/generated/model',
            client: 'react-query',
            prettier: true,
            override: {
                mutator: {
                    path: 'src/lib/axios.ts',
                    name: 'customInstance',
                },
            },
        },
    },
});
