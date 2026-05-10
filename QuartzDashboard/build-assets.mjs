import { build } from 'esbuild';

await build({
  entryPoints: ['wwwroot/src/main.js'],
  outfile: 'wwwroot/app.min.js',
  bundle: true,
  format: 'iife',
  minify: true,
  sourcemap: false,
  target: ['es2020'],
});

await build({
  entryPoints: ['wwwroot/charts.js'],
  outfile: 'wwwroot/charts.min.js',
  bundle: false,
  minify: true,
  sourcemap: false,
  target: ['es2020'],
});

await build({
  entryPoints: ['wwwroot/app.css'],
  outfile: 'wwwroot/app.min.css',
  bundle: true,
  minify: true,
  sourcemap: false,
});

console.log('Assets minified successfully');
