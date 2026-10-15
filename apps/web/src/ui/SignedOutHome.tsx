import type { FormEventHandler } from 'react';
import { productCopy } from '../content';
import { BrandMark } from './BrandMark';

interface SignedOutHomeProps {
  loading: boolean;
  busy: boolean;
  error: string | null;
  phoneNumber: string;
  code: string;
  challengePending: boolean;
  onPhoneNumberChange: (value: string) => void;
  onCodeChange: (value: string) => void;
  onGoogle: () => void;
  onSendCode: FormEventHandler<HTMLFormElement>;
  onVerifyCode: FormEventHandler<HTMLFormElement>;
}

export function SignedOutHome({
  loading,
  busy,
  error,
  phoneNumber,
  code,
  challengePending,
  onPhoneNumberChange,
  onCodeChange,
  onGoogle,
  onSendCode,
  onVerifyCode,
}: SignedOutHomeProps) {
  return (
    <main className="page-shell public-home">
      <section className="public-introduction" aria-label="RAKRAO introduction">
        <BrandMark variant="public" />
        <p className="tagline">{productCopy.tagline}</p>
        <p className="foundation-note">
          A private space for your family stories and shared history.
        </p>
      </section>

      <section className="auth-panel" aria-labelledby="sign-in-title">
        <h2 id="sign-in-title">Sign in to your family space</h2>
        {error && (
          <p role="alert" className="auth-error">
            {error}
          </p>
        )}
        {loading ? (
          <p className="foundation-note" role="status">
            Connecting your account…
          </p>
        ) : (
          <>
            <button type="button" onClick={onGoogle} disabled={busy}>
              Continue with Google
            </button>
            <div className="auth-divider">or use your phone</div>
            <form onSubmit={onSendCode}>
              <label htmlFor="phone-number">Phone number</label>
              <input
                id="phone-number"
                type="tel"
                autoComplete="tel"
                placeholder="+66…"
                required
                value={phoneNumber}
                onChange={(event) => onPhoneNumberChange(event.target.value)}
              />
              <div id="recaptcha-container" />
              <button type="submit" disabled={busy}>
                Send code
              </button>
            </form>
            {challengePending && (
              <form onSubmit={onVerifyCode}>
                <label htmlFor="verification-code">Verification code</label>
                <input
                  id="verification-code"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  required
                  value={code}
                  onChange={(event) => onCodeChange(event.target.value)}
                />
                <button type="submit" disabled={busy}>
                  Verify code
                </button>
              </form>
            )}
          </>
        )}
      </section>
    </main>
  );
}
