import type { FormEventHandler } from 'react';
import type { FamilySummary } from '../api/client';
import { FamilyEditor } from './FamilyEditor';

interface FamilyOverviewProps {
  family: FamilySummary;
  editing: boolean;
  editName: string;
  editDescription: string;
  busy: boolean;
  error: string | null;
  onStartEdit: () => void;
  onCancelEdit: () => void;
  onEditNameChange: (value: string) => void;
  onEditDescriptionChange: (value: string) => void;
  onSubmitEdit: FormEventHandler<HTMLFormElement>;
}

export function FamilyOverview({
  family,
  editing,
  editName,
  editDescription,
  busy,
  error,
  onStartEdit,
  onCancelEdit,
  onEditNameChange,
  onEditDescriptionChange,
  onSubmitEdit,
}: FamilyOverviewProps) {
  const canEdit = family.capabilities.includes('EDIT_FAMILY');
  const description =
    family.description || "A private space for this family's shared history.";

  return (
    <article className="family-overview">
      <div className="family-hero">
        <p className="eyebrow">Family overview</p>
        <h1>{family.name}</h1>
        <p className="family-description">{description}</p>
      </div>

      <section className="archive-state" aria-labelledby="family-archive-title">
        <h2 id="family-archive-title">Family archive</h2>
        <p>
          People and relationships will appear here as your family record grows.
        </p>
        <div className="connection-line" aria-hidden="true" />
      </section>

      <aside className="family-about" aria-labelledby="family-about-title">
        <h2 id="family-about-title">About this family</h2>
        {editing && canEdit ? (
          <FamilyEditor
            name={editName}
            description={editDescription}
            busy={busy}
            error={error}
            onNameChange={onEditNameChange}
            onDescriptionChange={onEditDescriptionChange}
            onSubmit={onSubmitEdit}
            onCancel={onCancelEdit}
          />
        ) : (
          canEdit && (
            <button type="button" onClick={onStartEdit} disabled={busy}>
              Manage family
            </button>
          )
        )}
      </aside>
    </article>
  );
}
