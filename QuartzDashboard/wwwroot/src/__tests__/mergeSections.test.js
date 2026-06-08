import { describe, it, expect } from 'vitest';
import { mergeSections } from '../utils.js';

describe('mergeSections', () => {
  it('returns an empty object when no sources are provided', () => {
    const result = mergeSections();
    expect(result).toEqual({});
    expect(Object.keys(result)).toHaveLength(0);
  });

  it('copies all own properties from a single source', () => {
    const source = { a: 1, b: 'hello', c: () => 42 };
    const result = mergeSections(source);
    expect(result.a).toBe(1);
    expect(result.b).toBe('hello');
    expect(result.c()).toBe(42);
  });

  it('merges properties from multiple sources', () => {
    const a = { x: 1, shared: 'from-a' };
    const b = { y: 2, shared: 'from-b' };
    const result = mergeSections(a, b);
    expect(result.x).toBe(1);
    expect(result.y).toBe(2);
    expect(result.shared).toBe('from-b');
  });

  it('later sources override earlier ones', () => {
    const a = { count: 1, name: 'a' };
    const b = { count: 2 };
    const c = { count: 3 };
    const result = mergeSections(a, b, c);
    expect(result.count).toBe(3);
    expect(result.name).toBe('a');
  });

  it('preserves methods (functions) from combined sections', () => {
    const a = { greet() { return 'hi from a'; } };
    const b = { farewell() { return 'bye from b'; } };
    const result = mergeSections(a, b);
    expect(result.greet()).toBe('hi from a');
    expect(result.farewell()).toBe('bye from b');
  });

  it('ignores null and undefined sources', () => {
    const a = { x: 1 };
    const result = mergeSections(null, a, undefined, { y: 2 });
    expect(result.x).toBe(1);
    expect(result.y).toBe(2);
  });

  it('returns a plain object distinct from any source', () => {
    const a = { x: 1 };
    const result = mergeSections(a);
    expect(result).not.toBe(a);
    result.x = 2;
    expect(a.x).toBe(1);
  });

  it('preserves getters and setters', () => {
    let internal = 0;
    const source = {
      get value() { return internal; },
      set value(v) { internal = v; },
    };
    const result = mergeSections(source);
    result.value = 42;
    expect(result.value).toBe(42);
    expect(internal).toBe(42);
  });

  it('handles deeply nested objects by reference', () => {
    const nested = { deep: { value: 42 } };
    const a = { data: nested };
    const result = mergeSections(a);
    expect(result.data).toBe(nested);
  });
});
