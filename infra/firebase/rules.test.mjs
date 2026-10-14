import { readFile } from 'node:fs/promises';
import { after, before, test } from 'node:test';
import { initializeTestEnvironment, assertFails } from '@firebase/rules-unit-testing';
import { doc, getDoc, setDoc, deleteDoc } from 'firebase/firestore';

let environment;
before(async () => {
  const host = process.env.FIRESTORE_EMULATOR_HOST;
  if (!host) throw new Error('FIRESTORE_EMULATOR_HOST is required; rules tests must not use a live database.');
  const [hostname, port] = host.split(':');
  environment = await initializeTestEnvironment({
    projectId: 'demo-rakrao-rules',
    firestore: {
      host: hostname,
      port: Number(port),
      rules: await readFile(new URL('../../firestore.rules', import.meta.url), 'utf8'),
    },
  });
});
after(async () => { await environment?.cleanup(); });

for (const collection of [
  'users', 'firebase_uid_mappings', 'families', 'family_memberships',
  'role_assignments', 'active_role_keys', 'family_guards',
  'family_creation_limits', 'family_creation_requests', 'audit_events',
]) {
  test(`browser clients cannot read or mutate ${collection}, even with Firebase authentication`, async () => {
    await environment.withSecurityRulesDisabled(async (context) => {
      await setDoc(doc(context.firestore(), collection, 'protected'), { marker: 'server-owned' });
    });
    for (const context of [environment.unauthenticatedContext(), environment.authenticatedContext('firebase-user')]) {
      const reference = doc(context.firestore(), collection, 'protected');
      await assertFails(getDoc(reference));
      await assertFails(setDoc(reference, { marker: 'browser-overwrite' }));
      await assertFails(deleteDoc(reference));
      await assertFails(setDoc(doc(context.firestore(), collection, 'new'), { marker: 'browser-create' }));
    }
  });
}
