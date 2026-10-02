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

afterEach(cleanup);

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
});
