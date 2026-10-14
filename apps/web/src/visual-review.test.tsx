// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { VisualReview } from './visual-review';

afterEach(cleanup);

describe('VisualReview', () => {
  it.each([
    'signed-out',
    'phone-verification',
    'editable',
    'editing',
    'readonly',
    'multiple',
    'empty',
  ] as const)('renders the %s scenario', (scenario) => {
    render(<VisualReview scenario={scenario} />);
    expect(screen.getByTestId(`visual-${scenario}`)).toBeTruthy();
  });

  it('exposes only the controls appropriate to each authenticated state', () => {
    const { rerender } = render(<VisualReview scenario="multiple" />);
    expect(
      screen.getByRole('combobox', { name: 'Current family' }),
    ).toBeTruthy();

    rerender(<VisualReview scenario="readonly" />);
    expect(screen.queryByRole('button', { name: 'Manage family' })).toBeNull();

    rerender(<VisualReview scenario="editing" />);
    expect(screen.getByLabelText('Edit family name')).toBeTruthy();

    rerender(<VisualReview scenario="empty" />);
    expect(
      screen.getByText('Create your family').closest('details')?.open,
    ).toBe(true);

    rerender(<VisualReview scenario="phone-verification" />);
    expect(screen.getByLabelText('Verification code')).toBeTruthy();
  });
});
