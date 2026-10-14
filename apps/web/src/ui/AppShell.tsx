import type { ReactNode } from 'react';
import type { FamilySummary } from '../api/client';
import { AccountMenu } from './AccountMenu';
import { BrandMark } from './BrandMark';
import { FamilySwitcher } from './FamilySwitcher';

interface AppShellProps {
  displayName: string | null;
  families: FamilySummary[];
  selectedFamilyId: string | null;
  busy: boolean;
  error: string | null;
  onSelectFamily: (familyId: string) => void;
  onSignOut: () => void;
  children: ReactNode;
}

export function AppShell({
  displayName,
  families,
  selectedFamilyId,
  busy,
  error,
  onSelectFamily,
  onSignOut,
  children,
}: AppShellProps) {
  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>
      <header className="app-header">
        <BrandMark variant="compact" />
        <nav aria-label="Primary navigation">
          <a href="#main-content" aria-current="page">
            Overview
          </a>
        </nav>
        <FamilySwitcher
          families={families}
          selectedFamilyId={selectedFamilyId}
          onSelectFamily={onSelectFamily}
        />
        <AccountMenu
          displayName={displayName}
          busy={busy}
          onSignOut={onSignOut}
        />
      </header>
      <main id="main-content" className="app-main" tabIndex={-1}>
        {error && (
          <p role="alert" className="auth-error">
            {error}
          </p>
        )}
        {children}
      </main>
    </div>
  );
}
