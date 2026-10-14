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
import { AppShell } from './ui/AppShell';
import { CreateFamilyForm } from './ui/CreateFamilyForm';
import { FamilyOverview } from './ui/FamilyOverview';
import { SignedOutHome } from './ui/SignedOutHome';

type FamilyFeedback = {
  scope: 'create' | 'edit';
  message: string;
};

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
  const [familyFeedback, setFamilyFeedback] = useState<FamilyFeedback | null>(
    null,
  );
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
          setFamilyFeedback(null);
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
          setFamilyFeedback(null);
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
      setFamilyFeedback(null);
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
    setFamilyFeedback(null);
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
      setFamilyFeedback(null);
    } catch {
      if (generation.current === current)
        setFamilyFeedback({
          scope: 'create',
          message: 'Family creation failed. Please try again later.',
        });
    } finally {
      if (generation.current === current) setBusy(false);
    }
  }

  function selectFamily(id: string | null) {
    setSelectedFamilyId(id);
    setEditingFamily(false);
    setFamilyFeedback(null);
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
    setFamilyFeedback(null);
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
      setFamilyFeedback(null);
    } catch (failure) {
      if (generation.current === current)
        setFamilyFeedback({
          scope: 'edit',
          message:
            failure instanceof Error && failure.message.endsWith('status 409')
              ? 'Family details changed elsewhere. Reload this page before editing again.'
              : 'Family update failed. Please try again.',
        });
    } finally {
      if (generation.current === current) setBusy(false);
    }
  }

  const selectedFamily = me?.families.find(
    (family) => family.id === selectedFamilyId,
  );

  if (!signedIn) {
    return (
      <SignedOutHome
        loading={loading}
        busy={busy}
        error={error}
        phoneNumber={phoneNumber}
        code={code}
        challengePending={challenge !== null}
        onPhoneNumberChange={setPhoneNumber}
        onCodeChange={setCode}
        onGoogle={() => void handleGoogle()}
        onSendCode={(event) => void handleSendCode(event)}
        onVerifyCode={(event) => void handleVerifyCode(event)}
      />
    );
  }

  return (
    <AppShell
      displayName={me?.displayName ?? null}
      families={me?.families ?? []}
      selectedFamilyId={selectedFamilyId}
      busy={busy}
      error={error}
      onSelectFamily={selectFamily}
      onSignOut={() => void handleSignOut()}
    >
      {loading ? (
        <p className="foundation-note" role="status">
          Loading your family space…
        </p>
      ) : me ? (
        selectedFamily ? (
          <div className="overview-layout">
            <FamilyOverview
              family={selectedFamily}
              editing={editingFamily}
              editName={editName}
              editDescription={editDescription}
              busy={busy}
              error={
                familyFeedback?.scope === 'edit' ? familyFeedback.message : null
              }
              onStartEdit={() => {
                setEditName(selectedFamily.name);
                setEditDescription(selectedFamily.description || '');
                setFamilyFeedback(null);
                setEditingFamily(true);
              }}
              onCancelEdit={() => {
                setEditingFamily(false);
                setFamilyFeedback(null);
              }}
              onEditNameChange={setEditName}
              onEditDescriptionChange={setEditDescription}
              onSubmitEdit={(event) => void handleUpdateFamily(event)}
            />
            <CreateFamilyForm
              name={familyName}
              description={familyDescription}
              busy={busy}
              error={
                familyFeedback?.scope === 'create'
                  ? familyFeedback.message
                  : null
              }
              priority="secondary"
              onNameChange={setFamilyName}
              onDescriptionChange={setFamilyDescription}
              onSubmit={(event) => void handleCreateFamily(event)}
            />
          </div>
        ) : (
          <section className="empty-family-state">
            <p className="eyebrow">Begin your archive</p>
            <h1>Create your family space</h1>
            <p>
              Start with a name and an optional description for your private
              family space.
            </p>
            <CreateFamilyForm
              name={familyName}
              description={familyDescription}
              busy={busy}
              error={
                familyFeedback?.scope === 'create'
                  ? familyFeedback.message
                  : null
              }
              priority="primary"
              onNameChange={setFamilyName}
              onDescriptionChange={setFamilyDescription}
              onSubmit={(event) => void handleCreateFamily(event)}
            />
          </section>
        )
      ) : (
        <section className="auth-panel">
          <h1>Account unavailable</h1>
          <p className="muted">Sign out and try again.</p>
        </section>
      )}
    </AppShell>
  );
}
