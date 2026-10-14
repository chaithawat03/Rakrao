import type { FormEventHandler } from 'react';

interface CreateFamilyFormProps {
  name: string;
  description: string;
  busy: boolean;
  error: string | null;
  priority: 'primary' | 'secondary';
  onNameChange: (value: string) => void;
  onDescriptionChange: (value: string) => void;
  onSubmit: FormEventHandler<HTMLFormElement>;
}

export function CreateFamilyForm({
  name,
  description,
  busy,
  error,
  priority,
  onNameChange,
  onDescriptionChange,
  onSubmit,
}: CreateFamilyFormProps) {
  return (
    <details
      className={`create-family create-family--${priority}`}
      open={priority === 'primary' || undefined}
    >
      <summary>
        {priority === 'primary'
          ? 'Create your family'
          : 'Create another family'}
      </summary>
      <form className="family-form" onSubmit={onSubmit}>
        <label htmlFor="family-name">Family name</label>
        <input
          id="family-name"
          required
          maxLength={160}
          value={name}
          onChange={(event) => onNameChange(event.target.value)}
        />
        <label htmlFor="family-description">Description</label>
        <textarea
          id="family-description"
          maxLength={2000}
          value={description}
          onChange={(event) => onDescriptionChange(event.target.value)}
        />
        {error && (
          <p role="alert" className="auth-error">
            {error}
          </p>
        )}
        <button type="submit" disabled={busy}>
          Create family
        </button>
      </form>
    </details>
  );
}
