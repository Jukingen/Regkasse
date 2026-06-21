import { defineConfig } from 'vitest/config';
import path from 'path';

export default defineConfig({
    esbuild: {
        jsx: 'automatic',
    },
    test: {
        globals: true,
        environment: 'jsdom',
        setupFiles: ['./src/test/vitest.setup.ts'],
        testTimeout: 15000,
        env: {
            // axios.ts requires a base URL when NODE_ENV is not "development" (e.g. vitest uses "test")
            NEXT_PUBLIC_API_BASE_URL: 'http://127.0.0.1:5184',
        },
    },
    resolve: {
        alias: {
            '@': path.resolve(__dirname, './src'),
        },
    },
});
