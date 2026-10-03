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
  onboardingState: 'NEW_MEMBER' | 'ACTIVE_MEMBER';
  families: FamilySummary[];
}

export interface FamilySummary {
  id: string;
  name: string;
  description: string | null;
  version: number;
  roles: string[];
  capabilities: string[];
}

export interface FamilyInput {
  name: string;
  description: string;
}

export async function createFamily(
  idToken: string,
  input: FamilyInput,
  idempotencyKey: string = crypto.randomUUID(),
): Promise<FamilySummary> {
  const response = await fetch(`${apiBaseUrl}/v1/families`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: `Bearer ${idToken}`,
      'Idempotency-Key': idempotencyKey,
    },
    body: JSON.stringify(input),
  });
  if (!response.ok)
    throw new Error(`Family creation failed with status ${response.status}`);
  return (await response.json()) as FamilySummary;
}

export async function updateFamily(
  idToken: string,
  familyId: string,
  version: number,
  input: FamilyInput,
): Promise<FamilySummary> {
  const response = await fetch(
    `${apiBaseUrl}/v1/families/${encodeURIComponent(familyId)}`,
    {
      method: 'PATCH',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        Authorization: `Bearer ${idToken}`,
        'If-Match': `"${version}"`,
      },
      body: JSON.stringify(input),
    },
  );
  if (!response.ok)
    throw new Error(`Family update failed with status ${response.status}`);
  return (await response.json()) as FamilySummary;
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
