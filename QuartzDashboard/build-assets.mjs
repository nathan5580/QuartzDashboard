import { build } from 'esbuild';
import { readFileSync, writeFileSync } from 'node:fs';

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

// Bundle tailwind.css + app.css together into app.min.css
const tailwindCss = readFileSync('wwwroot/tailwind.css', 'utf8');
const appCssRaw = readFileSync('wwwroot/app.css', 'utf8')
  .replace(/^\s*<style>\s*/, '')
  .replace(/\s*<\/style>\s*$/, '');
const combinedCss = tailwindCss + '\n' + appCssRaw;

await build({
  stdin: {
    contents: combinedCss,
    loader: 'css',
    sourcefile: 'wwwroot/app.css',
  },
  outfile: 'wwwroot/app.min.css',
  bundle: false,
  minify: true,
});

console.log('Assets minified successfully');
