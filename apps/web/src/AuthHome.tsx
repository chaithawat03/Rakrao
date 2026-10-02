import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { provisionMe as defaultProvisionMe } from './api/client';
import type { MeResponse } from './api/client';
import { firebaseGateway } from './auth/firebaseGateway';
import type { AuthGateway, PhoneChallenge } from './auth/gateway';
import { productCopy } from './content';

interface AuthHomeProps {
  auth?: AuthGateway;
  provisionMe?: (idToken: string) => Promise<MeResponse>;
}

function safeAuthError(error: unknown): string {
  const code =
    typeof error === 'object' && error !== null && 'code' in error
      ? String(error.code)
      : '';
  if (code === 'auth/invalid-phone-number')
    return 'Enter a valid phone number with country code.';
  if (code === 'auth/invalid-verification-code')
    return 'The verification code is incorrect.';
  if (code === 'auth/too-many-requests')
    return 'Too many attempts. Please try again later.';
  if (code === 'auth/popup-closed-by-user')
    return 'Google sign-in was cancelled.';
  return 'Authentication failed. Please try again.';
}

export default function AuthHome({
  auth = firebaseGateway,
  provisionMe = defaultProvisionMe,
}: AuthHomeProps) {
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [signedIn, setSignedIn] = useState(false);
  const [me, setMe] = useState<MeResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [phoneNumber, setPhoneNumber] = useState('');
  const [code, setCode] = useState('');
  const [challenge, setChallenge] = useState<PhoneChallenge | null>(null);
  const generation = useRef(0);

  useEffect(() => {
    let unsubscribe = () => {};
    try {
      unsubscribe = auth.observe(
        (principal) => {
          const current = ++generation.current;
          setSignedIn(Boolean(principal));
          setMe(null);
          setError(null);
          setChallenge(null);
          if (!principal) {
            setLoading(false);
            return;
          }
          setLoading(true);
          void principal
            .getIdToken()
            .then((token) => provisionMe(token))
            .then((user) => {
              if (generation.current === current) setMe(user);
            })
            .catch(() => {
              if (generation.current === current)
                setError('Account setup failed. Please try signing in again.');
            })
            .finally(() => {
              if (generation.current === current) setLoading(false);
            });
        },
        () => {
          generation.current += 1;
          setSignedIn(false);
          setMe(null);
          setChallenge(null);
          setError('Authentication session could not be loaded.');
          setLoading(false);
        },
      );
    } catch {
      generation.current += 1;
      setSignedIn(false);
      setMe(null);
      setError('Firebase authentication is not configured.');
      setLoading(false);
    }
    return () => {
      generation.current += 1;
      unsubscribe();
    };
  }, [auth, provisionMe]);

  async function handleGoogle() {
    setError(null);
    setBusy(true);
    try {
      await auth.signInWithGoogle();
    } catch (failure) {
      setError(safeAuthError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function handleSendCode(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      setChallenge(
        await auth.sendPhoneCode(phoneNumber.trim(), 'recaptcha-container'),
      );
    } catch (failure) {
      setError(safeAuthError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function handleVerifyCode(event: FormEvent) {
    event.preventDefault();
    if (!challenge) return;
    setError(null);
    setBusy(true);
    try {
      await challenge.confirm(code.trim());
    } catch (failure) {
      setError(safeAuthError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function handleSignOut() {
    setError(null);
    setBusy(true);
    try {
      await auth.signOut();
    } catch {
      setError('Sign-out failed. Please try again.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="page-shell">
      <div className="brand-mark" aria-label={productCopy.brand}>
        <span className="brand-symbol" aria-hidden="true">
          ✳
        </span>
        <span>{productCopy.brand}</span>
      </div>
      <section className="welcome-card" aria-labelledby="welcome-title">
        <p className="eyebrow">OUR ROOTS</p>
        <h1 id="welcome-title">{productCopy.name}</h1>
        <p className="tagline">{productCopy.tagline}</p>
        {error && (
          <p role="alert" className="auth-error">
            {error}
          </p>
        )}
        {loading ? (
          <p className="foundation-note">Connecting your account…</p>
        ) : me ? (
          <div className="auth-panel">
            <h2>Account ready</h2>
            <p>{me.displayName || 'Your RAKRAO account'}</p>
            <p className="muted">No family memberships yet.</p>
            <button
              type="button"
              onClick={() => void handleSignOut()}
              disabled={busy}
            >
              Sign out
            </button>
          </div>
        ) : signedIn ? (
          <div className="auth-panel">
            <h2>Account unavailable</h2>
            <p className="muted">Sign out and try again.</p>
            <button
              type="button"
              onClick={() => void handleSignOut()}
              disabled={busy}
            >
              Sign out
            </button>
          </div>
        ) : (
          <div className="auth-panel">
            <h2>Sign in</h2>
            <button
              type="button"
              onClick={() => void handleGoogle()}
              disabled={busy}
            >
              Continue with Google
            </button>
            <div className="auth-divider">or use your phone</div>
            <form onSubmit={(event) => void handleSendCode(event)}>
              <label htmlFor="phone-number">Phone number</label>
              <input
                id="phone-number"
                type="tel"
                autoComplete="tel"
                placeholder="+66…"
                required
                value={phoneNumber}
                onChange={(event) => setPhoneNumber(event.target.value)}
              />
              <div id="recaptcha-container" />
              <button type="submit" disabled={busy}>
                Send code
              </button>
            </form>
            {challenge && (
              <form onSubmit={(event) => void handleVerifyCode(event)}>
                <label htmlFor="verification-code">Verification code</label>
                <input
                  id="verification-code"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  required
                  value={code}
                  onChange={(event) => setCode(event.target.value)}
                />
                <button type="submit" disabled={busy}>
                  Verify code
                </button>
              </form>
            )}
          </div>
        )}
      </section>
    </main>
  );
}
