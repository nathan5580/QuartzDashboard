const { defineConfig } = require('vitest/config');

module.exports = defineConfig({
  test: {
    environment: 'node',
    include: ['wwwroot/src/__tests__/**/*.test.js'],
  },
  esbuild: {
    target: 'node18',
  },
});
