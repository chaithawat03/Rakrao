import type { FormEventHandler } from 'react';

interface FamilyEditorProps {
  name: string;
  description: string;
  busy: boolean;
  error: string | null;
  onNameChange: (value: string) => void;
  onDescriptionChange: (value: string) => void;
  onSubmit: FormEventHandler<HTMLFormElement>;
  onCancel: () => void;
}

export function FamilyEditor({
  name,
  description,
  busy,
  error,
  onNameChange,
  onDescriptionChange,
  onSubmit,
  onCancel,
}: FamilyEditorProps) {
  return (
    <form className="family-form family-editor" onSubmit={onSubmit}>
      <label htmlFor="edit-family-name">Edit family name</label>
      <input
        id="edit-family-name"
        required
        maxLength={160}
        value={name}
        onChange={(event) => onNameChange(event.target.value)}
      />
      <label htmlFor="edit-family-description">Edit description</label>
      <textarea
        id="edit-family-description"
        maxLength={2000}
        value={description}
        onChange={(event) => onDescriptionChange(event.target.value)}
      />
      {error && (
        <p role="alert" className="auth-error">
          {error}
        </p>
      )}
      <div className="form-actions">
        <button type="submit" disabled={busy}>
          Save family
        </button>
        <button type="button" onClick={onCancel} disabled={busy}>
          Cancel
        </button>
      </div>
    </form>
  );
}
