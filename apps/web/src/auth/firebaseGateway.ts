import { getApp, getApps, initializeApp } from 'firebase/app';
import {
  connectAuthEmulator,
  getAuth,
  GoogleAuthProvider,
  onAuthStateChanged,
  RecaptchaVerifier,
  signInWithPhoneNumber,
  signInWithPopup,
  signOut,
} from 'firebase/auth';
import type { Auth } from 'firebase/auth';
import type { AuthGateway, PhoneChallenge } from './gateway';

let connectedToEmulator = false;

function authClient(): Auth {
  const apiKey = import.meta.env.VITE_FIREBASE_API_KEY;
  const authDomain = import.meta.env.VITE_FIREBASE_AUTH_DOMAIN;
  const projectId = import.meta.env.VITE_FIREBASE_PROJECT_ID;
  const appId = import.meta.env.VITE_FIREBASE_APP_ID;
  if (!apiKey || !authDomain || !projectId || !appId) {
    throw new Error('Firebase web configuration is incomplete.');
  }

  const app = getApps().some((candidate) => candidate.name === 'rakrao')
    ? getApp('rakrao')
    : initializeApp({ apiKey, authDomain, projectId, appId }, 'rakrao');
  const auth = getAuth(app);
  const emulatorUrl = import.meta.env.VITE_FIREBASE_AUTH_EMULATOR_URL;
  if (import.meta.env.DEV && emulatorUrl && !connectedToEmulator) {
    connectAuthEmulator(auth, emulatorUrl, { disableWarnings: true });
    connectedToEmulator = true;
  }
  return auth;
}

export const firebaseGateway: AuthGateway = {
  observe(onChange, onError) {
    return onAuthStateChanged(authClient(), onChange, onError);
  },
  async signInWithGoogle() {
    await signInWithPopup(authClient(), new GoogleAuthProvider());
  },
  async sendPhoneCode(phoneNumber, containerId): Promise<PhoneChallenge> {
    const verifier = new RecaptchaVerifier(authClient(), containerId, {
      size: 'normal',
    });
    try {
      const confirmation = await signInWithPhoneNumber(
        authClient(),
        phoneNumber,
        verifier,
      );
      return {
        async confirm(code) {
          try {
            await confirmation.confirm(code);
          } finally {
            verifier.clear();
          }
        },
      };
    } catch (error) {
      verifier.clear();
      throw error;
    }
  },
  async signOut() {
    await signOut(authClient());
  },
};
