#!/usr/bin/env python3
"""Offline safety tests. All gcloud/Firebase invocations are local fixture programs."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

HERE = Path(__file__).resolve().parent
MOCK = r'''#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
args=sys.argv[1:]
p=Path(os.environ['FIXTURE_STATE'])
s=json.loads(p.read_text())
with open(os.environ['FIXTURE_CALLS'],'a') as f: f.write(json.dumps([Path(sys.argv[0]).name]+args)+'\n')
def out(value): print(json.dumps(value)); sys.exit(0)
def flag(name,default=None): return next((a.split('=',1)[1] for a in args if a.startswith(name+'=')),default)
def error(): print('PERMISSION_DENIED fixture',file=sys.stderr); sys.exit(1)
def save(): p.write_text(json.dumps(s)); out({})
cmd=' '.join(args)
if s.get('deny') and cmd.startswith(s['deny']): error()
if args[:3]==['config','get-value','project']: print(s.get('project','rakrao-dev')); sys.exit(0)
if args[:3]==['config','get-value','auth/impersonate_service_account']: print('(unset)'); sys.exit(0)
if args[:2]==['projects','describe']: print(s.get('number','723651832622')); sys.exit(0)
if Path(sys.argv[0]).name=='firebase':
 if args[0]=='hosting:sites:list': out({'status':'success','result':{'sites':[{'name':'projects/rakrao-dev/sites/rakrao-dev'}]}})
 if args[0]=='deploy':
  with open(os.environ['FIXTURE_WRITES'],'a') as f: f.write(cmd+'\n')
  assert args[1:3]==['--only','firestore']; out({})
 error()
if args[:3]==['services','list','--enabled']: out([{'config':{'name':a}} for a in s['apis']])
if args[:3]==['sql','instances','list']: out(s.get('sql',[]))
if args[:3]==['firestore','databases','list']: out(s.get('db',[]))
if args[:3]==['firestore','locations','list']: out([{'locationId':'asia-southeast1'}])
if args[:3]==['artifacts','repositories','list']: out(s.get('repos',[]))
if args[:3]==['run','services','list']: out(s.get('runs',[]))
if args[:3]==['run','services','describe']: out(s['runs'][0])
if args[:3]==['iam','service-accounts','list']: out(s.get('accounts',[]))
if args[:4]==['iam','service-accounts','keys','list']: out([])
if args[:3]==['iam','roles','list']: out(s.get('roles',[]))
if args[:3]==['iam','roles','describe']: out(s['roles'][0])
if args[:3]==['iam','workload-identity-pools','list']: out(s.get('pools',[]))
if args[:4]==['iam','workload-identity-pools','providers','list']: out(s.get('providers',[]))
if args[:4]==['iam','workload-identity-pools','providers','describe']: out(s['providers'][0])
if args[:2]==['projects','get-iam-policy']: out(s.get('project_iam',{'bindings':[]}))
if 'get-iam-policy' in args: out({'bindings':[]})
if args[:4]==['firestore','indexes','composite','list']:
 wanted=json.loads((Path(__file__).resolve().parents[1]/'firestore.indexes.json').read_text())
 out([dict(x,state='READY',name='projects/rakrao-dev/databases/(default)/collectionGroups/'+x['collectionGroup']+'/indexes/'+str(i)) for i,x in enumerate(wanted['indexes'])])
if any(a in args for a in ['create','create-oidc','deploy','add-iam-policy-binding','set-cleanup-policies','enable','disable']):
 with open(os.environ['FIXTURE_WRITES'],'a') as f: f.write(cmd+'\n')
 if args[:3]==['firestore','databases','create']: s['db']=[{'name':'projects/rakrao-dev/databases/(default)','type':'FIRESTORE_NATIVE','locationId':'asia-southeast1','databaseEdition':'STANDARD'}]
 if args[:3]==['artifacts','repositories','create']: s['repos']=[{'name':'projects/rakrao-dev/locations/asia-southeast1/repositories/rakrao-staging','format':'DOCKER','mode':'STANDARD_REPOSITORY'}]
 if args[:3]==['iam','service-accounts','create']: s.setdefault('accounts',[]).append({'email':args[3]+'@rakrao-dev.iam.gserviceaccount.com'})
 if args[:3]==['iam','roles','create']: s['roles']=[{'name':'projects/rakrao-dev/roles/'+args[3],'stage':flag('--stage'),'includedPermissions':flag('--permissions').split(',')}]
 if args[:3]==['iam','workload-identity-pools','create']: s['pools']=[{'name':'projects/723651832622/locations/global/workloadIdentityPools/rakrao-staging','state':'ACTIVE'}]
 if args[:4]==['iam','workload-identity-pools','providers','create-oidc']: s['providers']=[{'name':'projects/723651832622/locations/global/workloadIdentityPools/rakrao-staging/providers/github-staging','state':'ACTIVE','oidc':{'issuerUri':flag('--issuer-uri')},'attributeCondition':flag('--attribute-condition'),'attributeMapping':dict(a.split('=',1) for a in flag('--attribute-mapping').split(','))}]
 if args[:2]==['run','deploy']: s['runs']=[{'metadata':{'name':'rakrao-api-staging','annotations':{'run.googleapis.com/minScale':'0','run.googleapis.com/maxScale':'1'}},'spec':{'template':{'spec':{'serviceAccountName':'rakrao-api-runtime@rakrao-dev.iam.gserviceaccount.com','containerConcurrency':20,'timeoutSeconds':60,'containers':[{'resources':{'limits':{'cpu':'1','memory':'512Mi'}},'ports':[{'containerPort':8080}]}]}}}}]
 if args[:3]==['artifacts','repositories','set-cleanup-policies']: s['repos'][0]['cleanupPolicies']={x['name']:dict(id=x['name'],action=x['action']['type'].upper(),**({'condition':dict(x['condition'],tagState=x['condition']['tagState'].upper())} if 'condition' in x else {'mostRecentVersions':x['mostRecentVersions']})) for x in json.loads(Path(flag('--policy')).read_text())}
 if args[:2]==['services','enable']: s['apis']+= [x for x in args[2:] if not x.startswith('--')]
 if args[:2]==['services','disable']: s['apis'].remove('sqladmin.googleapis.com')
 save()
print('UNMOCKED COMMAND '+cmd,file=sys.stderr); sys.exit(2)
'''
APIS = ['serviceusage', 'cloudresourcemanager', 'run', 'firestore', 'artifactregistry', 'iam', 'iamcredentials', 'sts', 'firebase', 'firebasehosting', 'firebaserules', 'identitytoolkit', 'securetoken', 'logging', 'monitoring', 'sqladmin']
APPLY = ['--apply', '--reviewed-location-and-pricing', '--reviewed-iam']

class BootstrapSafety(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        for name in ['infra/staging', 'infra/firebase/node_modules/.bin', 'bin']:
            (self.root / name).mkdir(parents=True)
        for name in ['bootstrap-rakrao-dev.sh', 'cleanup-policy.json']:
            shutil.copy(HERE / name, self.root / 'infra/staging' / name)
        shutil.copy(HERE.parents[1] / 'firestore.indexes.json', self.root)
        for target in ['bin/gcloud', 'infra/firebase/node_modules/.bin/firebase']:
            f = self.root / target
            f.write_text(MOCK)
            f.chmod(0o755)
        self.state = {'apis': [a + '.googleapis.com' for a in APIS]}
        self.write_state()
        self.env = {k: v for k, v in os.environ.items() if k not in ['GOOGLE_APPLICATION_CREDENTIALS','CLOUDSDK_AUTH_CREDENTIAL_FILE_OVERRIDE','CLOUDSDK_AUTH_ACCESS_TOKEN','CLOUDSDK_AUTH_IMPERSONATE_SERVICE_ACCOUNT','FIREBASE_TOKEN','FIRESTORE_EMULATOR_HOST','FIREBASE_AUTH_EMULATOR_HOST']}
        self.env.update(PATH=str(self.root / 'bin') + ':' + os.environ['PATH'], DEVSHELL_PROJECT_ID='rakrao-dev', FIXTURE_STATE=str(self.root / 'state.json'), FIXTURE_CALLS=str(self.root / 'calls'), FIXTURE_WRITES=str(self.root / 'writes'))

    def write_state(self):
        (self.root / 'state.json').write_text(json.dumps(self.state))

    def run_script(self, args=()):
        result = subprocess.run(['bash', str(self.root / 'infra/staging/bootstrap-rakrao-dev.sh'), *args], env=self.env, capture_output=True, text=True, timeout=20)
        return result

    def assert_no_writes(self):
        self.assertFalse((self.root / 'writes').exists(), 'rejected preflight made cloud writes')

    def rejected(self, args=APPLY):
        result = self.run_script(args)
        self.assertNotEqual(result.returncode, 0, result.stdout)
        self.assert_no_writes()
        return result

    def test_default_is_read_only(self):
        result = self.run_script()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('PLAN ONLY', result.stdout)
        self.assert_no_writes()

    def test_wrong_project_and_number(self):
        for field, bad in [('project','other-project'),('number','1')]:
            with self.subTest(field=field):
                self.state[field] = bad
                self.write_state()
                self.rejected()
                self.state.pop(field)

    def test_inventory_permission_errors_never_mean_absent(self):
        for prefix in ['services list','sql instances list','firestore databases list','artifacts repositories list','run services list','iam service-accounts list','iam workload-identity-pools list','hosting:sites:list','projects get-iam-policy']:
            with self.subTest(prefix=prefix):
                self.state['deny'] = prefix
                self.write_state()
                self.rejected()

    def test_incompatible_firestore(self):
        for mode, region in [('DATASTORE_MODE','asia-southeast1'),('FIRESTORE_NATIVE','us-central1')]:
            self.state['db'] = [{'name':'projects/rakrao-dev/databases/(default)','type':mode,'locationId':region}]
            self.write_state()
            self.rejected()

    def test_sql_nonzero(self):
        self.state['sql'] = [{'name':'legacy-sql'}]
        self.write_state()
        self.rejected()

    def test_sql_disabled_is_unknown(self):
        self.state['apis'].remove('sqladmin.googleapis.com')
        self.write_state()
        self.assertIn('zero instances is UNKNOWN', self.rejected().stderr)

    def test_missing_review_gates(self):
        for args in [['--apply'],['--apply','--reviewed-iam'],['--apply','--reviewed-location-and-pricing'], APPLY+['--disable-unused-sql-api']]:
            self.rejected(args)

    def test_no_cloud_shell(self):
        self.env.pop('DEVSHELL_PROJECT_ID')
        self.rejected()

    def test_copied_credentials_rejected(self):
        self.env['GOOGLE_APPLICATION_CREDENTIALS'] = '/not/a/real/credential.json'
        self.rejected()

    def test_untrusted_extra_provider_rejected(self):
        self.state['pools'] = [{'name':'projects/723651832622/locations/global/workloadIdentityPools/rakrao-staging','state':'ACTIVE'}]
        self.state['providers'] = [{'name':'projects/723651832622/locations/global/workloadIdentityPools/rakrao-staging/providers/untrusted'}]
        self.write_state()
        self.rejected()

    def test_missing_api_stops_without_automatic_enabling(self):
        self.state['apis'].remove('run.googleapis.com')
        self.write_state()
        self.rejected()

    def test_api_mode_only_enables_reviewed_missing_apis_then_exits(self):
        self.state['apis'].remove('run.googleapis.com')
        self.write_state()
        result = self.run_script(['--enable-reviewed-apis','--reviewed-location-and-pricing','--reviewed-iam'])
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((self.root/'writes').read_text(), 'services enable run.googleapis.com --quiet --project=rakrao-dev\n')

    def test_apply_tight_trust_and_rerun_no_duplicate_creation(self):
        result = self.run_script(APPLY)
        self.assertEqual(result.returncode, 0, result.stderr)
        writes = (self.root/'writes').read_text()
        state = json.loads((self.root/'state.json').read_text())
        provider = state['providers'][0]
        for required in ["assertion.repository_id == '1401374063'", "assertion.repository_owner_id == '29879036'", "assertion.ref == 'refs/heads/main'", "assertion.sub == 'repo:chaithawat03/Rakrao:environment:staging'", "assertion.workflow_ref == 'chaithawat03/Rakrao/.github/workflows/deploy-staging.yml@refs/heads/main'", "assertion.event_name == 'workflow_dispatch'"]:
            self.assertIn(required, provider['attributeCondition'])
        self.assertEqual(writes.count('--role=roles/iam.workloadIdentityUser'),4)
        self.assertEqual(writes.count('/attribute.repository_id/1401374063'),4)
        for forbidden in ['roles/owner','roles/editor','roles/iam.serviceAccountTokenCreator','roles/iam.serviceAccountKeyAdmin','--force','services disable']:
            self.assertNotIn(forbidden,writes)
        self.assertIn('--min=0 --max=1',writes)
        self.assertIn('--only firestore',writes)
        (self.root/'writes').unlink()
        result = self.run_script(APPLY)
        self.assertEqual(result.returncode,0,result.stderr)
        writes = (self.root/'writes').read_text()
        self.assertNotIn(' create ', writes)
        self.assertNotIn('create-oidc',writes)
        self.assertNotIn('run deploy',writes)

    def test_runtime_unconditional_or_different_database_grant_rejected(self):
        for condition in [None, {'expression':'true','title':'broad'}, {'expression':"resource.name == 'projects/rakrao-dev/databases/other'",'title':'other'}]:
            with self.subTest(condition=condition):
                binding={'role':'roles/datastore.user','members':['serviceAccount:rakrao-api-runtime@rakrao-dev.iam.gserviceaccount.com']}
                if condition is not None:
                    binding['condition']=condition
                self.state['project_iam']={'bindings':[binding]}
                self.write_state()
                self.rejected()

    def test_runtime_approved_condition_allows_apply_and_rerun(self):
        self.state['project_iam']={'bindings':[{
            'role':'roles/datastore.user',
            'members':['serviceAccount:rakrao-api-runtime@rakrao-dev.iam.gserviceaccount.com'],
            'condition':{'expression':"resource.name == 'projects/rakrao-dev/databases/(default)'",'title':'rakrao-staging-default-database'}
        }]}
        self.write_state()
        for _ in range(2):
            result=self.run_script(APPLY)
            self.assertEqual(result.returncode,0,result.stderr)

    def test_existing_provider_drift_stops_before_writes(self):
        result=self.run_script(APPLY)
        self.assertEqual(result.returncode,0,result.stderr)
        self.state=json.loads((self.root/'state.json').read_text())
        self.state['providers'][0]['attributeCondition']='true'
        self.write_state()
        (self.root/'writes').unlink()
        self.rejected()

if __name__ == '__main__':
    unittest.main(verbosity=2)
