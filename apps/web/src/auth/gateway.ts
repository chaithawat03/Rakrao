export interface AuthPrincipal {
  uid: string;
  getIdToken(): Promise<string>;
}

export interface PhoneChallenge {
  confirm(code: string): Promise<void>;
}

export interface AuthGateway {
  observe(
    onChange: (principal: AuthPrincipal | null) => void,
    onError?: (error: Error) => void,
  ): () => void;
  signInWithGoogle(): Promise<void>;
  sendPhoneCode(
    phoneNumber: string,
    containerId: string,
  ): Promise<PhoneChallenge>;
  signOut(): Promise<void>;
}
