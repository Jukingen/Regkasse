import { defineConfig } from 'orval';

export default defineConfig({
    kasse: {
        input: {
            target: '../backend/swagger.json',
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
