// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import type { ComponentProps } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SignedOutHome } from './SignedOutHome';

afterEach(cleanup);

function renderSignedOutHome(
  overrides: Partial<ComponentProps<typeof SignedOutHome>> = {},
) {
  const props: ComponentProps<typeof SignedOutHome> = {
    loading: false,
    busy: false,
    error: null,
    phoneNumber: '',
    code: '',
    challengePending: false,
    onPhoneNumberChange: vi.fn(),
    onCodeChange: vi.fn(),
    onGoogle: vi.fn(),
    onSendCode: vi.fn(),
    onVerifyCode: vi.fn(),
    ...overrides,
  };

  render(<SignedOutHome {...props} />);
  return props;
}

describe('SignedOutHome', () => {
  it('presents the public identity and authentication choices without app navigation', () => {
    renderSignedOutHome();

    const main = screen.getByRole('main');
    expect(main.textContent).toContain('รากเรา');
    expect(main.textContent).toContain('RAKRAO');
    expect(main.textContent).toContain('Discover how we are connected.');
    expect(
      screen.getByRole('heading', { name: 'Sign in to your family space' }),
    ).toBeTruthy();
    expect(
      screen.getByRole('button', { name: 'Continue with Google' }),
    ).toBeTruthy();
    expect(screen.getByLabelText('Phone number')).toBeTruthy();
    expect(screen.queryByRole('navigation')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Sign out' })).toBeNull();
  });

  it('forwards the controlled phone challenge interactions', () => {
    const onPhoneNumberChange = vi.fn();
    const onCodeChange = vi.fn();
    const onSendCode = vi.fn();
    const onVerifyCode = vi.fn();
    renderSignedOutHome({
      phoneNumber: '+16505554567',
      code: '123456',
      challengePending: true,
      onPhoneNumberChange,
      onCodeChange,
      onSendCode,
      onVerifyCode,
    });

    fireEvent.change(screen.getByLabelText('Phone number'), {
      target: { value: '+66812345678' },
    });
    fireEvent.submit(screen.getByLabelText('Phone number').closest('form')!);
    fireEvent.change(screen.getByLabelText('Verification code'), {
      target: { value: '654321' },
    });
    fireEvent.submit(
      screen.getByLabelText('Verification code').closest('form')!,
    );

    expect(onPhoneNumberChange).toHaveBeenCalledWith('+66812345678');
    expect(onCodeChange).toHaveBeenCalledWith('654321');
    expect(onSendCode).toHaveBeenCalledTimes(1);
    expect(onVerifyCode).toHaveBeenCalledTimes(1);
  });
});
