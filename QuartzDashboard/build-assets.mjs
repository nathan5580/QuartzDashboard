import { build } from 'esbuild';
import { readFileSync } from 'node:fs';

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

const appCss = readFileSync('wwwroot/app.css', 'utf8')
  .replace(/^\s*<style>\s*/, '')
  .replace(/\s*<\/style>\s*$/, '');

await build({
  stdin: {
    contents: appCss,
    loader: 'css',
    sourcefile: 'wwwroot/app.css',
  },
  outfile: 'wwwroot/app.min.css',
  bundle: false,
  minify: true,
});

console.log('Assets minified successfully');
