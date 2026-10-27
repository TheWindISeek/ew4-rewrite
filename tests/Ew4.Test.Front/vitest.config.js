import {defineConfig} from 'vitest/config';
import {fileURLToPath} from 'node:url';
import path from 'node:path';

const root = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  test: {
    include: ['*.test.js'],
    exclude: ['e2e/**'],
    root,
  },
});
