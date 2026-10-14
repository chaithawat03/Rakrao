interface AccountMenuProps {
  displayName: string | null;
  busy: boolean;
  onSignOut: () => void;
}

export function AccountMenu({
  displayName,
  busy,
  onSignOut,
}: AccountMenuProps) {
  return (
    <div className="account-menu">
      <span>{displayName || 'Your RAKRAO account'}</span>
      <button type="button" onClick={onSignOut} disabled={busy}>
        Sign out
      </button>
    </div>
  );
}
