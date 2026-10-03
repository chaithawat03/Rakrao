// @vitest-environment jsdom
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import AuthHome from './AuthHome';
import type { MeResponse } from './api/client';
import type {
  AuthGateway,
  AuthPrincipal,
  PhoneChallenge,
} from './auth/gateway';

afterEach(() => {
  cleanup();
  window.history.replaceState(null, '', '/');
});

class FakeAuth implements AuthGateway {
  private onChange: ((principal: AuthPrincipal | null) => void) | null = null;
  private onError: ((error: Error) => void) | null = null;
  google = vi.fn(async () => {});
  signOut = vi.fn(async () => {
    this.emit(null);
  });
  challenge: PhoneChallenge = { confirm: vi.fn(async () => {}) };
  sendPhoneCode = vi.fn(async () => this.challenge);

  observe(
    onChange: (principal: AuthPrincipal | null) => void,
    onError?: (error: Error) => void,
  ): () => void {
    this.onChange = onChange;
    this.onError = onError || null;
    onChange(null);
    return () => {
      this.onChange = null;
      this.onError = null;
    };
  }

  emit(principal: AuthPrincipal | null) {
    this.onChange?.(principal);
  }
  emitError() {
    this.onError?.(new Error('internal auth failure'));
  }
  signInWithGoogle = () => this.google();
}

const me: MeResponse = {
  id: 'user-1',
  displayName: 'Test User',
  onboardingState: 'NEW_MEMBER',
  families: [],
};
const family = {
  id: 'family-1',
  name: 'Our family',
  description: 'A private space',
  version: 1,
  roles: ['CREATOR', 'FAMILY_ADMIN'],
  capabilities: ['READ_FAMILY', 'EDIT_FAMILY', 'MANAGE_ROLES'],
};
const principal = {
  uid: 'firebase-1',
  getIdToken: async () => 'verified-id-token',
};

describe('authentication shell', () => {
  it('shows signed-out Google and phone entry points', async () => {
    const auth = new FakeAuth();
    render(<AuthHome auth={auth} provisionMe={vi.fn()} />);
    expect(
      await screen.findByRole('button', { name: /continue with google/i }),
    ).toBeTruthy();
    expect(screen.getByLabelText(/phone number/i)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /sign out/i })).toBeNull();
  });

  it('provisions through the API after a Firebase session appears', async () => {
    const auth = new FakeAuth();
    const provisionMe = vi.fn(async () => me);
    render(<AuthHome auth={auth} provisionMe={provisionMe} />);
    await act(async () => {
      auth.emit(principal);
    });
    expect(await screen.findByText(/account ready/i)).toBeTruthy();
    expect(screen.getByText('Test User')).toBeTruthy();
    expect(provisionMe).toHaveBeenCalledWith('verified-id-token');
  });

  it('shows an error when sign-in fails', async () => {
    const auth = new FakeAuth();
    auth.google.mockRejectedValueOnce(new Error('popup closed'));
    render(<AuthHome auth={auth} provisionMe={vi.fn()} />);
    fireEvent.click(
      await screen.findByRole('button', { name: /continue with google/i }),
    );
    expect(await screen.findByRole('alert')).toBeTruthy();
    expect(screen.queryByText('popup closed')).toBeNull();
  });

  it('lets an authenticated user sign out when API bootstrap fails', async () => {
    const auth = new FakeAuth();
    render(
      <AuthHome
        auth={auth}
        provisionMe={vi.fn(async () => {
          throw new Error('api unavailable');
        })}
      />,
    );
    await act(async () => {
      auth.emit(principal);
    });
    expect(await screen.findByRole('alert')).toBeTruthy();
    expect(screen.getByRole('button', { name: /sign out/i })).toBeTruthy();
    expect(screen.queryByText('api unavailable')).toBeNull();
  });

  it('clears application data when the session observer fails', async () => {
    const auth = new FakeAuth();
    render(<AuthHome auth={auth} provisionMe={vi.fn(async () => me)} />);
    await act(async () => {
      auth.emit(principal);
    });
    expect(await screen.findByText(/account ready/i)).toBeTruthy();
    act(() => auth.emitError());
    expect(screen.getByRole('alert')).toBeTruthy();
    expect(screen.queryByText(/account ready/i)).toBeNull();
  });

  it('sends and confirms a phone code using the auth gateway', async () => {
    const auth = new FakeAuth();
    render(<AuthHome auth={auth} provisionMe={vi.fn()} />);
    fireEvent.change(await screen.findByLabelText(/phone number/i), {
      target: { value: '+16505554567' },
    });
    fireEvent.click(screen.getByRole('button', { name: /send code/i }));
    expect(await screen.findByLabelText(/verification code/i)).toBeTruthy();
    expect(auth.sendPhoneCode).toHaveBeenCalledWith(
      '+16505554567',
      'recaptcha-container',
    );
    fireEvent.change(screen.getByLabelText(/verification code/i), {
      target: { value: '123456' },
    });
    fireEvent.click(screen.getByRole('button', { name: /verify code/i }));
    await waitFor(() =>
      expect(auth.challenge.confirm).toHaveBeenCalledWith('123456'),
    );
  });

  it('clears application state after sign-out', async () => {
    const auth = new FakeAuth();
    render(<AuthHome auth={auth} provisionMe={vi.fn(async () => me)} />);
    await act(async () => {
      auth.emit(principal);
    });
    fireEvent.click(await screen.findByRole('button', { name: /sign out/i }));
    expect(
      await screen.findByRole('button', { name: /continue with google/i }),
    ).toBeTruthy();
    expect(screen.queryByText(/account ready/i)).toBeNull();
  });

  it('creates a family and selects it for the signed-in user', async () => {
    const auth = new FakeAuth();
    const createFamily = vi.fn(async () => family);
    render(
      <AuthHome
        auth={auth}
        provisionMe={vi.fn(async () => me)}
        createFamily={createFamily}
      />,
    );
    await act(async () => auth.emit(principal));
    fireEvent.change(await screen.findByLabelText(/family name/i), {
      target: { value: 'Our family' },
    });
    fireEvent.change(screen.getByLabelText(/description/i), {
      target: { value: 'A private space' },
    });
    fireEvent.click(screen.getByRole('button', { name: /create family/i }));
    await waitFor(() =>
      expect(createFamily).toHaveBeenCalledWith(
        'verified-id-token',
        {
          name: 'Our family',
          description: 'A private space',
        },
        expect.any(String),
      ),
    );
    expect(
      await screen.findByRole('heading', { name: 'Our family' }),
    ).toBeTruthy();
  });

  it('switches only among returned families and shows edit controls from capabilities', async () => {
    const auth = new FakeAuth();
    const another = {
      id: 'family-2',
      name: 'Another family',
      description: null,
      version: 1,
      roles: ['FAMILY_MEMBER'],
      capabilities: ['READ_FAMILY'],
    };
    render(
      <AuthHome
        auth={auth}
        provisionMe={vi.fn(async () => ({
          ...me,
          onboardingState: 'ACTIVE_MEMBER' as const,
          families: [family, another],
        }))}
      />,
    );
    await act(async () => auth.emit(principal));
    expect(
      await screen.findByRole('button', { name: /edit family/i }),
    ).toBeTruthy();
    fireEvent.change(screen.getByLabelText(/current family/i), {
      target: { value: 'family-2' },
    });
    expect(
      screen.getByRole('heading', { name: 'Another family' }),
    ).toBeTruthy();
    expect(screen.queryByRole('button', { name: /edit family/i })).toBeNull();
  });

  it('updates family details only through the selected family control', async () => {
    const auth = new FakeAuth();
    const updateFamily = vi.fn(async () => ({
      ...family,
      name: 'New family name',
    }));
    render(
      <AuthHome
        auth={auth}
        provisionMe={vi.fn(async () => ({
          ...me,
          onboardingState: 'ACTIVE_MEMBER' as const,
          families: [family],
        }))}
        updateFamily={updateFamily}
      />,
    );
    await act(async () => auth.emit(principal));
    fireEvent.click(
      await screen.findByRole('button', { name: /edit family/i }),
    );
    fireEvent.change(screen.getByLabelText(/edit family name/i), {
      target: { value: 'New family name' },
    });
    fireEvent.click(screen.getByRole('button', { name: /save family/i }));
    await waitFor(() =>
      expect(updateFamily).toHaveBeenCalledWith(
        'verified-id-token',
        'family-1',
        1,
        { name: 'New family name', description: 'A private space' },
      ),
    );
    expect(
      await screen.findByRole('heading', { name: 'New family name' }),
    ).toBeTruthy();
  });

  it('reuses the family creation key after a recoverable failure', async () => {
    const auth = new FakeAuth();
    const createFamily = vi
      .fn()
      .mockRejectedValueOnce(new Error('network'))
      .mockResolvedValueOnce(family);
    render(
      <AuthHome
        auth={auth}
        provisionMe={vi.fn(async () => me)}
        createFamily={createFamily}
      />,
    );
    await act(async () => auth.emit(principal));
    fireEvent.change(await screen.findByLabelText(/family name/i), {
      target: { value: 'Our family' },
    });
    fireEvent.click(screen.getByRole('button', { name: /create family/i }));
    await screen.findByRole('alert');
    fireEvent.click(screen.getByRole('button', { name: /create family/i }));
    await screen.findByRole('heading', { name: 'Our family' });
    expect(createFamily).toHaveBeenCalledTimes(2);
    expect(createFamily.mock.calls[0][2]).toBe(createFamily.mock.calls[1][2]);
  });

  it('clears a pending family creation when the session changes', async () => {
    const auth = new FakeAuth();
    let finishCreation!: (value: typeof family) => void;
    const createFamily = vi.fn(
      () =>
        new Promise<typeof family>((resolve) => {
          finishCreation = resolve;
        }),
    );
    render(
      <AuthHome
        auth={auth}
        provisionMe={vi.fn(async () => me)}
        createFamily={createFamily}
      />,
    );
    await act(async () => auth.emit(principal));
    fireEvent.change(await screen.findByLabelText(/family name/i), {
      target: { value: 'Our family' },
    });
    fireEvent.click(screen.getByRole('button', { name: /create family/i }));
    await waitFor(() => expect(createFamily).toHaveBeenCalledOnce());
    act(() => auth.emit(null));
    expect(
      screen.getByRole('button', { name: /continue with google/i }),
    ).not.toHaveProperty('disabled', true);
    await act(async () => finishCreation(family));
    expect(screen.queryByRole('heading', { name: 'Our family' })).toBeNull();
  });

  it('replaces an unavailable family URL with an authorized selection', async () => {
    window.history.replaceState(null, '', '/?family=unavailable');
    const auth = new FakeAuth();
    render(
      <AuthHome
        auth={auth}
        provisionMe={vi.fn(async () => ({
          ...me,
          onboardingState: 'ACTIVE_MEMBER' as const,
          families: [family],
        }))}
      />,
    );
    await act(async () => auth.emit(principal));
    expect(
      await screen.findByRole('heading', { name: 'Our family' }),
    ).toBeTruthy();
    expect(new URLSearchParams(window.location.search).get('family')).toBe(
      'family-1',
    );
  });

  it('explains a stale family edit without showing a technical error', async () => {
    const auth = new FakeAuth();
    render(
      <AuthHome
        auth={auth}
        provisionMe={vi.fn(async () => ({
          ...me,
          onboardingState: 'ACTIVE_MEMBER' as const,
          families: [family],
        }))}
        updateFamily={vi.fn(async () => {
          throw new Error('Family update failed with status 409');
        })}
      />,
    );
    await act(async () => auth.emit(principal));
    fireEvent.click(
      await screen.findByRole('button', { name: /edit family/i }),
    );
    fireEvent.click(screen.getByRole('button', { name: /save family/i }));
    expect(
      await screen.findByText(/family details changed elsewhere/i),
    ).toBeTruthy();
    expect(screen.queryByText(/status 409/i)).toBeNull();
  });
});
