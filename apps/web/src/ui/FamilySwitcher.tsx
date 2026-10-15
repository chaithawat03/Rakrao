import type { FamilySummary } from '../api/client';

interface FamilySwitcherProps {
  families: FamilySummary[];
  selectedFamilyId: string | null;
  onSelectFamily: (familyId: string) => void;
}

export function FamilySwitcher({
  families,
  selectedFamilyId,
  onSelectFamily,
}: FamilySwitcherProps) {
  if (families.length === 0) return null;

  if (families.length === 1) {
    return (
      <div className="family-context">
        <span className="family-context__label">Current family</span>
        <strong>{families[0].name}</strong>
      </div>
    );
  }

  return (
    <div className="family-context">
      <label htmlFor="current-family">Current family</label>
      <select
        id="current-family"
        value={selectedFamilyId ?? ''}
        onChange={(event) => onSelectFamily(event.target.value)}
      >
        {families.map((family) => (
          <option key={family.id} value={family.id}>
            {family.name}
          </option>
        ))}
      </select>
    </div>
  );
}
