import { defineConfig } from 'orval';

/**
 * Orval: Backend swagger.json → generated clients (tags-split).
 * Legacy product ve categories path'leri transformer ile spec'ten çıkarılır (FE-Admin src/api/admin/* kullanır).
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
