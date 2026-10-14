import { StrictMode, useEffect, useState } from 'react';
import type { FormEventHandler } from 'react';
import { createRoot } from 'react-dom/client';
import type { FamilySummary } from './api/client';
import './styles.css';
import { AppShell } from './ui/AppShell';
import { CreateFamilyForm } from './ui/CreateFamilyForm';
import { FamilyOverview } from './ui/FamilyOverview';
import { SignedOutHome } from './ui/SignedOutHome';

export type VisualScenario =
  | 'signed-out'
  | 'phone-verification'
  | 'editable'
  | 'editing'
  | 'readonly'
  | 'multiple'
  | 'empty';

const primaryFamily: FamilySummary = {
  id: 'family-1',
  name: 'ครอบครัวศรีสุวรรณที่ร่วมเก็บเรื่องราวจากหลายรุ่น',
  description:
    'A private archive for the stories, memories, and relationships our family chooses to preserve together.',
  version: 4,
  roles: ['FAMILY_ADMIN'],
  capabilities: ['READ_FAMILY', 'EDIT_FAMILY'],
};

const secondFamily: FamilySummary = {
  id: 'family-2',
  name: 'The Chantarangsu family archive',
  description: null,
  version: 2,
  roles: ['FAMILY_MEMBER'],
  capabilities: ['READ_FAMILY'],
};

const preventSubmit: FormEventHandler<HTMLFormElement> = (event) =>
  event.preventDefault();

function AuthenticatedFixture({ scenario }: { scenario: VisualScenario }) {
  const families =
    scenario === 'empty'
      ? []
      : scenario === 'multiple'
        ? [primaryFamily, secondFamily]
        : [
            scenario === 'readonly'
              ? { ...primaryFamily, capabilities: ['READ_FAMILY'] }
              : primaryFamily,
          ];
  const [selectedFamilyId, setSelectedFamilyId] = useState(
    families[0]?.id ?? null,
  );
  const [editing, setEditing] = useState(scenario === 'editing');

  useEffect(() => {
    setSelectedFamilyId(families[0]?.id ?? null);
    setEditing(scenario === 'editing');
  }, [scenario]);

  const selectedFamily = families.find(
    (family) => family.id === selectedFamilyId,
  );

  return (
    <AppShell
      displayName="อรุณี ผู้ดูแลเรื่องราวครอบครัว"
      families={families}
      selectedFamilyId={selectedFamilyId}
      busy={false}
      error={null}
      onSelectFamily={setSelectedFamilyId}
      onSignOut={() => {}}
    >
      {selectedFamily ? (
        <div className="overview-layout">
          <FamilyOverview
            family={selectedFamily}
            editing={editing}
            editName={selectedFamily.name}
            editDescription={selectedFamily.description ?? ''}
            busy={false}
            error={null}
            onStartEdit={() => setEditing(true)}
            onCancelEdit={() => setEditing(false)}
            onEditNameChange={() => {}}
            onEditDescriptionChange={() => {}}
            onSubmitEdit={preventSubmit}
          />
          <CreateFamilyForm
            name=""
            description=""
            busy={false}
            error={null}
            priority="secondary"
            onNameChange={() => {}}
            onDescriptionChange={() => {}}
            onSubmit={preventSubmit}
          />
        </div>
      ) : (
        <section className="empty-family-state">
          <p className="eyebrow">Begin your archive</p>
          <h1>Create your family space</h1>
          <p>
            Start with a name and an optional description for your private
            family space.
          </p>
          <CreateFamilyForm
            name=""
            description=""
            busy={false}
            error={null}
            priority="primary"
            onNameChange={() => {}}
            onDescriptionChange={() => {}}
            onSubmit={preventSubmit}
          />
        </section>
      )}
    </AppShell>
  );
}

export function VisualReview({ scenario }: { scenario: VisualScenario }) {
  const isPublic =
    scenario === 'signed-out' || scenario === 'phone-verification';

  return (
    <div data-testid={`visual-${scenario}`}>
      {isPublic ? (
        <SignedOutHome
          loading={false}
          busy={false}
          error={null}
          phoneNumber="+66 81 234 5678"
          code="123456"
          challengePending={scenario === 'phone-verification'}
          onPhoneNumberChange={() => {}}
          onCodeChange={() => {}}
          onGoogle={() => {}}
          onSendCode={preventSubmit}
          onVerifyCode={preventSubmit}
        />
      ) : (
        <AuthenticatedFixture scenario={scenario} />
      )}
    </div>
  );
}

const scenarios: VisualScenario[] = [
  'signed-out',
  'phone-verification',
  'editable',
  'editing',
  'readonly',
  'multiple',
  'empty',
];
const rootElement = document.getElementById('visual-review-root');

if (rootElement) {
  const requested = new URLSearchParams(window.location.search).get('state');
  const scenario = scenarios.includes(requested as VisualScenario)
    ? (requested as VisualScenario)
    : 'editable';
  createRoot(rootElement).render(
    <StrictMode>
      <VisualReview scenario={scenario} />
    </StrictMode>,
  );
}
