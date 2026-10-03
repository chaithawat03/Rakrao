import { afterEach, describe, expect, it, vi } from 'vitest';
import { createFamily, provisionMe } from './client';

afterEach(() => vi.unstubAllGlobals());

describe('authenticated API bootstrap', () => {
  it('sends the ID token only in the authorization header', async () => {
    let requestUrl = '';
    let authorization = '';
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, options: RequestInit) => {
        requestUrl = url;
        authorization = new Headers(options.headers).get('Authorization') || '';
        return new Response(
          JSON.stringify({
            id: 'user-1',
            displayName: null,
            onboardingState: 'NEW_MEMBER',
            families: [],
          }),
          {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
          },
        );
      }),
    );

    const user = await provisionMe('private-test-token');

    expect(requestUrl).toBe('/api/v1/me');
    expect(authorization).toBe('Bearer private-test-token');
    expect(user.id).toBe('user-1');
  });

  it('rejects a failed bootstrap without exposing the ID token in the error', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(null, { status: 401 })),
    );
    await expect(provisionMe('private-test-token')).rejects.toThrow(
      'Account setup failed with status 401',
    );
  });
});

describe('family API', () => {
  it('sends family creation with the token in the authorization header', async () => {
    const fetcher = vi.fn(
      async () =>
        new Response(
          JSON.stringify({
            id: 'family-1',
            name: 'Our family',
            description: null,
            roles: ['CREATOR'],
            capabilities: ['READ_FAMILY'],
          }),
          {
            status: 201,
            headers: { 'Content-Type': 'application/json' },
          },
        ),
    );
    vi.stubGlobal('fetch', fetcher);

    const family = await createFamily(
      'private-test-token',
      { name: 'Our family', description: '' },
      'stable-create-key',
    );

    expect(family.id).toBe('family-1');
    expect(fetcher).toHaveBeenCalledWith(
      '/api/v1/families',
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({
          Authorization: 'Bearer private-test-token',
          'Idempotency-Key': 'stable-create-key',
        }),
        body: JSON.stringify({ name: 'Our family', description: '' }),
      }),
    );
  });
});
