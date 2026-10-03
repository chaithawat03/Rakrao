import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import {
  createFamily as defaultCreateFamily,
  provisionMe as defaultProvisionMe,
  updateFamily as defaultUpdateFamily,
} from './api/client';
import type { FamilyInput, FamilySummary, MeResponse } from './api/client';
import { firebaseGateway } from './auth/firebaseGateway';
import type {
  AuthGateway,
  AuthPrincipal,
  PhoneChallenge,
} from './auth/gateway';
import { productCopy } from './content';

interface AuthHomeProps {
  auth?: AuthGateway;
  provisionMe?: (idToken: string) => Promise<MeResponse>;
  createFamily?: (
    idToken: string,
    input: FamilyInput,
    idempotencyKey: string,
  ) => Promise<FamilySummary>;
  updateFamily?: (
    idToken: string,
    familyId: string,
    version: number,
    input: FamilyInput,
  ) => Promise<FamilySummary>;
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
  createFamily = defaultCreateFamily,
  updateFamily = defaultUpdateFamily,
}: AuthHomeProps) {
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [signedIn, setSignedIn] = useState(false);
  const [me, setMe] = useState<MeResponse | null>(null);
  const [principal, setPrincipal] = useState<AuthPrincipal | null>(null);
  const [selectedFamilyId, setSelectedFamilyId] = useState<string | null>(null);
  const [familyName, setFamilyName] = useState('');
  const [familyDescription, setFamilyDescription] = useState('');
  const [editingFamily, setEditingFamily] = useState(false);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [phoneNumber, setPhoneNumber] = useState('');
  const [code, setCode] = useState('');
  const [challenge, setChallenge] = useState<PhoneChallenge | null>(null);
  const generation = useRef(0);
  const creationAttempt = useRef<{ input: string; key: string } | null>(null);

  useEffect(() => {
    let unsubscribe = () => {};
    try {
      unsubscribe = auth.observe(
        (principal) => {
          const current = ++generation.current;
          setSignedIn(Boolean(principal));
          setPrincipal(principal);
          setMe(null);
          setSelectedFamilyId(null);
          setBusy(false);
          creationAttempt.current = null;
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
              if (generation.current === current) {
                setMe(user);
                const requested = new URLSearchParams(
                  window.location.search,
                ).get('family');
                selectFamily(
                  user.families.find((family) => family.id === requested)?.id ||
                    user.families[0]?.id ||
                    null,
                );
              }
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
          setPrincipal(null);
          setMe(null);
          setSelectedFamilyId(null);
          setBusy(false);
          creationAttempt.current = null;
          setChallenge(null);
          setError('Authentication session could not be loaded.');
          setLoading(false);
        },
      );
    } catch {
      generation.current += 1;
      setSignedIn(false);
      setPrincipal(null);
      setMe(null);
      setSelectedFamilyId(null);
      setBusy(false);
      creationAttempt.current = null;
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

  async function handleCreateFamily(event: FormEvent) {
    event.preventDefault();
    if (!principal || !me) return;
    const current = generation.current;
    setBusy(true);
    setError(null);
    try {
      const idToken = await principal.getIdToken();
      const input = {
        name: familyName.trim(),
        description: familyDescription.trim(),
      };
      const fingerprint = JSON.stringify(input);
      if (creationAttempt.current?.input !== fingerprint)
        creationAttempt.current = {
          input: fingerprint,
          key: crypto.randomUUID(),
        };
      const created = await createFamily(
        idToken,
        input,
        creationAttempt.current.key,
      );
      if (generation.current !== current) return;
      creationAttempt.current = null;
      setMe((existing) =>
        existing
          ? {
              ...existing,
              onboardingState: 'ACTIVE_MEMBER',
              families: [...existing.families, created],
            }
          : existing,
      );
      selectFamily(created.id);
      setFamilyName('');
      setFamilyDescription('');
    } catch {
      if (generation.current === current)
        setError('Family creation failed. Please try again later.');
    } finally {
      if (generation.current === current) setBusy(false);
    }
  }

  function selectFamily(id: string | null) {
    setSelectedFamilyId(id);
    setEditingFamily(false);
    const url = new URL(window.location.href);
    if (id) url.searchParams.set('family', id);
    else url.searchParams.delete('family');
    window.history.replaceState(null, '', url);
  }

  async function handleUpdateFamily(event: FormEvent) {
    event.preventDefault();
    if (!principal || !selectedFamily) return;
    const current = generation.current;
    const familyId = selectedFamily.id;
    setBusy(true);
    setError(null);
    try {
      const token = await principal.getIdToken();
      const updated = await updateFamily(
        token,
        familyId,
        selectedFamily.version,
        {
          name: editName.trim(),
          description: editDescription.trim(),
        },
      );
      if (generation.current !== current) return;
      setMe(
        (existing) =>
          existing && {
            ...existing,
            families: existing.families.map((family) =>
              family.id === familyId ? updated : family,
            ),
          },
      );
      setEditingFamily(false);
    } catch (failure) {
      if (generation.current === current)
        setError(
          failure instanceof Error && failure.message.endsWith('status 409')
            ? 'Family details changed elsewhere. Reload this page before editing again.'
            : 'Family update failed. Please try again.',
        );
    } finally {
      if (generation.current === current) setBusy(false);
    }
  }

  const selectedFamily = me?.families.find(
    (family) => family.id === selectedFamilyId,
  );

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
            {me.families.length === 0 ? (
              <p className="muted">No family memberships yet.</p>
            ) : (
              <div className="family-section">
                <label htmlFor="current-family">Current family</label>
                <select
                  id="current-family"
                  value={selectedFamily?.id || ''}
                  onChange={(event) => selectFamily(event.target.value)}
                >
                  {me.families.map((family) => (
                    <option key={family.id} value={family.id}>
                      {family.name}
                    </option>
                  ))}
                </select>
                {selectedFamily && (
                  <section aria-label="Selected family">
                    <h3>{selectedFamily.name}</h3>
                    {selectedFamily.description && (
                      <p>{selectedFamily.description}</p>
                    )}
                    {selectedFamily.capabilities.includes('EDIT_FAMILY') && (
                      <button
                        type="button"
                        onClick={() => {
                          setEditName(selectedFamily.name);
                          setEditDescription(selectedFamily.description || '');
                          setEditingFamily(true);
                        }}
                      >
                        Edit family
                      </button>
                    )}
                    {editingFamily &&
                      selectedFamily.capabilities.includes('EDIT_FAMILY') && (
                        <form
                          onSubmit={(event) => void handleUpdateFamily(event)}
                        >
                          <label htmlFor="edit-family-name">
                            Edit family name
                          </label>
                          <input
                            id="edit-family-name"
                            required
                            maxLength={160}
                            value={editName}
                            onChange={(event) =>
                              setEditName(event.target.value)
                            }
                          />
                          <label htmlFor="edit-family-description">
                            Edit description
                          </label>
                          <input
                            id="edit-family-description"
                            maxLength={2000}
                            value={editDescription}
                            onChange={(event) =>
                              setEditDescription(event.target.value)
                            }
                          />
                          <button type="submit" disabled={busy}>
                            Save family
                          </button>
                          <button
                            type="button"
                            onClick={() => setEditingFamily(false)}
                          >
                            Cancel
                          </button>
                        </form>
                      )}
                  </section>
                )}
              </div>
            )}
            <form onSubmit={(event) => void handleCreateFamily(event)}>
              <h3>Create a family</h3>
              <label htmlFor="family-name">Family name</label>
              <input
                id="family-name"
                required
                maxLength={160}
                value={familyName}
                onChange={(event) => setFamilyName(event.target.value)}
              />
              <label htmlFor="family-description">Description</label>
              <input
                id="family-description"
                maxLength={2000}
                value={familyDescription}
                onChange={(event) => setFamilyDescription(event.target.value)}
              />
              <button type="submit" disabled={busy}>
                Create family
              </button>
            </form>
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
