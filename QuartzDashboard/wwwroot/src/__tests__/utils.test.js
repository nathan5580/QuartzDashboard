import { describe, it, expect } from 'vitest';
import { createUtilsSection } from '../utils.js';

// mergeArrayInPlace and formatDuration live on the object returned by createUtilsSection.
// They use `this`, so we bind them to a minimal context object.
function makeCtx() {
  const methods = createUtilsSection();
  const ctx = Object.assign({}, methods);
  // Bind all methods so `this` resolves correctly when called as plain functions.
  for (const key of Object.keys(ctx)) {
    if (typeof ctx[key] === 'function') ctx[key] = ctx[key].bind(ctx);
  }
  return ctx;
}

// ---------------------------------------------------------------------------
// mergeArrayInPlace
// ---------------------------------------------------------------------------

describe('mergeArrayInPlace', () => {
  const key = item => item.id;

  it('adds new items from incoming', () => {
    const ctx = makeCtx();
    const existing = [];
    ctx.mergeArrayInPlace(existing, [{ id: 1, name: 'a' }], key);
    expect(existing).toHaveLength(1);
    expect(existing[0].id).toBe(1);
  });

  it('updates existing items in place (object identity preserved)', () => {
    const ctx = makeCtx();
    const original = { id: 1, name: 'old' };
    const existing = [original];
    ctx.mergeArrayInPlace(existing, [{ id: 1, name: 'new' }], key);
    expect(existing).toHaveLength(1);
    expect(existing[0]).toBe(original);
    expect(existing[0].name).toBe('new');
  });

  it('removes items not in incoming', () => {
    const ctx = makeCtx();
    const existing = [{ id: 1 }, { id: 2 }];
    ctx.mergeArrayInPlace(existing, [{ id: 1 }], key);
    expect(existing).toHaveLength(1);
    expect(existing[0].id).toBe(1);
  });

  it('handles empty existing array', () => {
    const ctx = makeCtx();
    const existing = [];
    ctx.mergeArrayInPlace(existing, [{ id: 1 }, { id: 2 }], key);
    expect(existing).toHaveLength(2);
  });

  it('handles empty incoming array (clears existing)', () => {
    const ctx = makeCtx();
    const existing = [{ id: 1 }, { id: 2 }];
    ctx.mergeArrayInPlace(existing, [], key);
    expect(existing).toHaveLength(0);
  });

  it('handles duplicate keys in incoming (last wins)', () => {
    const ctx = makeCtx();
    const existing = [];
    // Two items with the same id — the second one is pushed because the first
    // is consumed from the map when the first match is found; the second stays.
    ctx.mergeArrayInPlace(existing, [{ id: 1, v: 'first' }, { id: 1, v: 'second' }], key);
    // Map construction deduplicates: last entry wins, so only one survives.
    expect(existing).toHaveLength(1);
    expect(existing[0].v).toBe('second');
  });
});

// ---------------------------------------------------------------------------
// formatDuration
// ---------------------------------------------------------------------------

describe('formatDuration', () => {
  it('returns empty string for null', () => {
    const ctx = makeCtx();
    expect(ctx.formatDuration(null)).toBe('');
  });

  it('returns empty string for undefined', () => {
    const ctx = makeCtx();
    expect(ctx.formatDuration(undefined)).toBe('');
  });

  it('handles zero', () => {
    const ctx = makeCtx();
    // 0 is falsy — the guard `if (!d) return ''` catches it.
    expect(ctx.formatDuration(0)).toBe('');
  });

  it('formats milliseconds under 100 with one decimal', () => {
    const ctx = makeCtx();
    expect(ctx.formatDuration(50)).toBe('50.0ms');
  });

  it('formats milliseconds at 100 and above with no decimal', () => {
    const ctx = makeCtx();
    expect(ctx.formatDuration(100)).toBe('100ms');
  });

  it('formats seconds', () => {
    const ctx = makeCtx();
    expect(ctx.formatDuration(1500)).toBe('1.50s');
  });

  it('formats minutes and seconds', () => {
    const ctx = makeCtx();
    expect(ctx.formatDuration(65000)).toBe('1m 5s');
  });

  it('parses .NET TimeSpan string hh:mm:ss into hours and minutes', () => {
    const ctx = makeCtx();
    expect(ctx.formatDuration('1:30:45')).toBe('1h 30m');
  });

  it('parses .NET TimeSpan with only minutes and seconds', () => {
    const ctx = makeCtx();
    expect(ctx.formatDuration('0:02:30')).toBe('2m 30s');
  });

  it('parses .NET TimeSpan with fractional seconds', () => {
    const ctx = makeCtx();
    // 0:00:00.500 = 500ms
    expect(ctx.formatDuration('0:00:00.500')).toBe('500ms');
  });

  it('parses .NET TimeSpan with days', () => {
    const ctx = makeCtx();
    expect(ctx.formatDuration('1.02:00:00')).toBe('1d 2h');
  });
});
