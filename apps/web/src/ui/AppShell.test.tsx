// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import type { ComponentProps } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { FamilySummary } from '../api/client';
import { AppShell } from './AppShell';

afterEach(cleanup);

const firstFamily: FamilySummary = {
  id: 'family-1',
  name: 'ครอบครัวใจดี',
  description: 'Our shared history',
  version: 1,
  roles: ['FAMILY_MEMBER'],
  capabilities: ['READ_FAMILY'],
};

const secondFamily: FamilySummary = {
  ...firstFamily,
  id: 'family-2',
  name: 'Another family',
};

function renderShell(
  families: FamilySummary[],
  overrides: Partial<ComponentProps<typeof AppShell>> = {},
) {
  const props: ComponentProps<typeof AppShell> = {
    displayName: 'Test User',
    families,
    selectedFamilyId: families[0]?.id ?? null,
    busy: false,
    error: null,
    onSelectFamily: vi.fn(),
    onSignOut: vi.fn(),
    children: <h1>Overview content</h1>,
    ...overrides,
  };

  render(<AppShell {...props} />);
  return props;
}

describe('AppShell', () => {
  it('provides the authenticated landmarks, skip link, and only real navigation', () => {
    renderShell([firstFamily]);

    const skipLink = screen.getByRole('link', { name: 'Skip to main content' });
    expect(skipLink.getAttribute('href')).toBe('#main-content');
    expect(screen.getByRole('banner')).toBeTruthy();
    expect(screen.getByRole('navigation')).toBeTruthy();
    expect(
      screen
        .getByRole('link', { name: 'Overview' })
        .getAttribute('aria-current'),
    ).toBe('page');
    expect(screen.getByRole('main').id).toBe('main-content');
    expect(screen.queryByText('People')).toBeNull();
    expect(screen.queryByText('Tree')).toBeNull();
    expect(screen.queryByText('Invitations')).toBeNull();
  });

  it('shows one Family as static context and multiple Families as supplied options', () => {
    renderShell([firstFamily]);
    expect(screen.getByText(firstFamily.name)).toBeTruthy();
    expect(
      screen.queryByRole('combobox', { name: 'Current family' }),
    ).toBeNull();

    cleanup();
    const onSelectFamily = vi.fn();
    renderShell([firstFamily, secondFamily], { onSelectFamily });
    const switcher = screen.getByRole('combobox', { name: 'Current family' });
    expect(
      screen.getAllByRole('option').map((option) => option.textContent),
    ).toEqual([firstFamily.name, secondFamily.name]);

    fireEvent.change(switcher, { target: { value: secondFamily.id } });
    expect(onSelectFamily).toHaveBeenCalledWith(secondFamily.id);
  });

  it('omits Family context for zero Families and keeps Sign out directly reachable', () => {
    const onSignOut = vi.fn();
    renderShell([], { displayName: null, onSignOut });

    expect(screen.queryByText('Current family')).toBeNull();
    expect(screen.getByText('Your RAKRAO account')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));
    expect(onSignOut).toHaveBeenCalledTimes(1);
  });
});
