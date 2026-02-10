import { defineConfig } from 'orval';

export default defineConfig({
    kasse: {
        input: {
            target: '../backend/KasseAPI_Final/KasseAPI_Final/swagger.json',
        },
        output: {
            mode: 'tags-split',
            target: 'src/api/generated/endpoints.ts',
            schemas: 'src/api/generated/model',
            client: 'react-query',
            prettier: true,
            override: {
                mutator: {
                    path: 'src/api/http.ts',
                    name: 'customInstance',
                },
            },
        },
    },
});
