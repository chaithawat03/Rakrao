const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL || '/api';

export const apiBaseUrl = configuredBaseUrl.replace(/\/$/, '');

export async function apiGet<T>(path: `/${string}`): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) {
    throw new Error(`API request failed with status ${response.status}`);
  }
  return (await response.json()) as T;
}

export interface MeResponse {
  id: string;
  displayName: string | null;
  onboardingState: 'NEW_MEMBER';
  families: unknown[];
}

export async function provisionMe(idToken: string): Promise<MeResponse> {
  const response = await fetch(`${apiBaseUrl}/v1/me`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      Authorization: `Bearer ${idToken}`,
    },
  });
  if (!response.ok) {
    throw new Error(`Account setup failed with status ${response.status}`);
  }
  return (await response.json()) as MeResponse;
}
