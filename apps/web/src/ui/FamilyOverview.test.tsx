// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from '@testing-library/react';
import type { ComponentProps } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { FamilySummary } from '../api/client';
import { CreateFamilyForm } from './CreateFamilyForm';
import { FamilyOverview } from './FamilyOverview';

afterEach(cleanup);

const editableFamily: FamilySummary = {
  id: 'family-1',
  name: 'ครอบครัวใจดี',
  description: 'A history carried across generations.',
  version: 3,
  roles: ['FAMILY_ADMIN'],
  capabilities: ['READ_FAMILY', 'EDIT_FAMILY'],
};

function renderOverview(
  family: FamilySummary = editableFamily,
  overrides: Partial<ComponentProps<typeof FamilyOverview>> = {},
) {
  const props: ComponentProps<typeof FamilyOverview> = {
    family,
    editing: false,
    editName: family.name,
    editDescription: family.description ?? '',
    busy: false,
    error: null,
    onStartEdit: vi.fn(),
    onCancelEdit: vi.fn(),
    onEditNameChange: vi.fn(),
    onEditDescriptionChange: vi.fn(),
    onSubmitEdit: vi.fn(),
    ...overrides,
  };

  render(<FamilyOverview {...props} />);
  return props;
}

describe('FamilyOverview', () => {
  it('uses the Family identity and an honest non-interactive archive state', () => {
    renderOverview();

    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe(
      editableFamily.name,
    );
    expect(screen.getByText(editableFamily.description!)).toBeTruthy();
    const archive = screen.getByRole('region', { name: 'Family archive' });
    expect(within(archive).queryByRole('button')).toBeNull();
    expect(within(archive).queryByRole('link')).toBeNull();
    expect(archive.textContent).not.toMatch(/\b\d+\b|activity/i);
  });

  it('uses neutral copy for a missing description and gates management by capability', () => {
    const readonlyFamily = {
      ...editableFamily,
      description: null,
      capabilities: ['READ_FAMILY'],
    };
    renderOverview(readonlyFamily);

    expect(
      screen.getByText("A private space for this family's shared history."),
    ).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Manage family' })).toBeNull();
  });

  it('opens the controlled editor with existing limits and actions', () => {
    const onStartEdit = vi.fn();
    const onCancelEdit = vi.fn();
    const onSubmitEdit = vi.fn();
    const { rerender } = render(
      <FamilyOverview
        family={editableFamily}
        editing={false}
        editName={editableFamily.name}
        editDescription={editableFamily.description!}
        busy={false}
        error={null}
        onStartEdit={onStartEdit}
        onCancelEdit={onCancelEdit}
        onEditNameChange={vi.fn()}
        onEditDescriptionChange={vi.fn()}
        onSubmitEdit={onSubmitEdit}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Manage family' }));
    expect(onStartEdit).toHaveBeenCalledTimes(1);

    rerender(
      <FamilyOverview
        family={editableFamily}
        editing
        editName={editableFamily.name}
        editDescription={editableFamily.description!}
        busy={false}
        error="Family update failed. Please try again."
        onStartEdit={onStartEdit}
        onCancelEdit={onCancelEdit}
        onEditNameChange={vi.fn()}
        onEditDescriptionChange={vi.fn()}
        onSubmitEdit={onSubmitEdit}
      />,
    );
    expect(
      screen.getByLabelText('Edit family name').getAttribute('maxlength'),
    ).toBe('160');
    expect(
      screen.getByLabelText('Edit description').getAttribute('maxlength'),
    ).toBe('2000');
    expect(screen.getByRole('alert')).toBeTruthy();
    fireEvent.submit(
      screen.getByLabelText('Edit family name').closest('form')!,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(onSubmitEdit).toHaveBeenCalledTimes(1);
    expect(onCancelEdit).toHaveBeenCalledTimes(1);
  });
});

describe('CreateFamilyForm', () => {
  it('starts secondary creation collapsed and primary creation open', () => {
    const commonProps: Omit<
      ComponentProps<typeof CreateFamilyForm>,
      'priority'
    > = {
      name: '',
      description: '',
      busy: false,
      error: null,
      onNameChange: vi.fn(),
      onDescriptionChange: vi.fn(),
      onSubmit: vi.fn(),
    };
    const { rerender } = render(
      <CreateFamilyForm {...commonProps} priority="secondary" />,
    );
    expect(
      screen.getByText('Create another family').closest('details')?.open,
    ).toBe(false);

    rerender(<CreateFamilyForm {...commonProps} priority="primary" />);
    expect(
      screen.getByText('Create your family').closest('details')?.open,
    ).toBe(true);
    expect(screen.getByLabelText('Family name')).toBeTruthy();
  });
});
