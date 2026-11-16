const path=require('node:path');const root=path.resolve(__dirname,'../../..');
const {loadModule,response}=require(path.join(root,'tests/helpers/api-fixture'));
console.info=(...args)=>console.error(...args);
const users=[
 {uid:'owner-1',email:'owner@example.test',displayName:'Owner',disabled:false,customClaims:{admin:true,role:'owner',custom:1},providerData:[{providerId:'password'}],metadata:{creationTime:'Mon, 16 Nov 2026 00:00:00 GMT',lastSignInTime:''}},
 {uid:'admin-1',email:'admin@example.test',displayName:'Admin',disabled:false,customClaims:{admin:true,role:'admin',custom:false},providerData:[{providerId:'google.com'}],metadata:{creationTime:'',lastSignInTime:''}},
 {uid:'customer-1',email:'',displayName:'',disabled:true,customClaims:{custom:'keep'},providerData:[],metadata:{creationTime:'',lastSignInTime:''}}
];
const entries=[{id:'audit-1',fields:{actorUid:'actor',actorEmail:'owner@example.test',action:'test.update',resourceType:'test',resourceId:'a',details:{keep:true},createdAt:{$timestamp:'2026-11-16T01:02:03.123Z'}}},
 {id:'audit-2',fields:{details:[],createdAt:'legacy-string'}}];
const cases=[
 {name:'admin list',kind:'admin'}, {name:'admin pagination',kind:'admin',body:{pageToken:'page-a'}},
 {name:'admin bad pagination',kind:'admin',body:{pageToken:1}}, {name:'admin long pagination',kind:'admin',body:{pageToken:'x'.repeat(2049)}},
 {name:'admin listing fails',kind:'admin',listError:true}, {name:'admin missing token',kind:'admin',authorization:''},
 {name:'admin revoked token',kind:'admin',tokenError:true}, {name:'admin role removed',kind:'admin',roleMissing:true},
 {name:'admin claim false',kind:'admin',admin:false}, {name:'admin auth configuration',kind:'admin',authError:true},
 {name:'admin store configuration',kind:'admin',storeError:true}, {name:'admin role outage',kind:'admin',roleError:true},
 {name:'method before configuration',kind:'admin',method:'GET',authError:true},
 {name:'unknown routed API',kind:'admin',query:{route:'other'}},
 {name:'owner list',kind:'owner',body:{action:'list'}}, {name:'owner backfill outage best effort',kind:'owner',body:{action:'list'},backfillError:true},
 {name:'owner wrong claims',kind:'owner',body:{action:'list'},claimRole:'admin'},
 {name:'owner mirror no longer owner',kind:'owner',body:{action:'list'},documentRole:'admin'},
 {name:'owner bad action',kind:'owner',body:{}}, {name:'owner invalid UID',kind:'owner',body:{action:'grant-admin',uid:''}},
 {name:'owner self change',kind:'owner',body:{action:'remove-admin',uid:'actor'}},
 {name:'owner protected target',kind:'owner',body:{action:'grant-admin',uid:'owner-1'}},
 {name:'grant admin',kind:'owner',body:{action:'grant-admin',uid:'customer-1'}},
 {name:'remove admin',kind:'owner',body:{action:'remove-admin',uid:'admin-1'}},
 {name:'remove customer',kind:'owner',body:{action:'remove-admin',uid:'customer-1'}},
 {name:'user missing',kind:'owner',body:{action:'grant-admin',uid:'missing'}},
 {name:'claims failure',kind:'owner',body:{action:'grant-admin',uid:'customer-1'},claimsError:true},
 {name:'mirror failure after claims',kind:'owner',body:{action:'grant-admin',uid:'customer-1'},mirrorError:true},
 {name:'revoke failure after claims and mirror',kind:'owner',body:{action:'grant-admin',uid:'customer-1'},revokeError:true},
 {name:'audit failure after role and revoke',kind:'owner',body:{action:'grant-admin',uid:'customer-1'},auditError:true},
 {name:'audit entries',kind:'audit'}, {name:'audit read outage',kind:'audit',auditReadError:true},
 {name:'audit method before config',kind:'audit',method:'POST',authError:true}, {name:'audit configuration',kind:'audit',authError:true},
 {name:'audit denied',kind:'audit',admin:false}
];
function revive(fields){return Object.fromEntries(Object.entries(fields).map(([key,value])=>[key,value&&value.$timestamp?{toDate:()=>new Date(value.$timestamp)}:value]));}
(async()=>{
 const output=[];
 for(const input of cases){
  let tokenReads=0,roleReads=0;const events=[],claims=[],audits=[],mirrors=[];
  const token={uid:'actor',email:'owner@example.test',admin:input.admin??true,role:input.claimRole??'owner'};
  const db={collection(name){if(name==='adminRoles')return {doc(){return {async get(){roleReads++;if(input.roleError)throw new Error('provider-private');return {exists:!input.roleMissing,data:()=>({role:input.documentRole??'owner'})};}};}};
   if(name==='adminAudit')return {orderBy(field,direction){if(field!=='createdAt'||direction!=='desc')throw new Error('query mismatch');return {limit(size){if(size!==100)throw new Error('limit mismatch');return {async get(){events.push('audit-read');if(input.auditReadError)throw new Error('provider-private');return {docs:entries.map(entry=>({id:entry.id,data:()=>revive(entry.fields)}))};}};}};}};
   throw new Error('unexpected collection');
  }};
  const auth={async verifyIdToken(raw,revoked){tokenReads++;if(!revoked)throw new Error('revocation disabled');if(input.tokenError)throw new Error('provider-private');return token;},
   async listUsers(size,pageToken){events.push('list:'+(pageToken||''));if(size!==100)throw new Error('page mismatch');if(input.listError)throw new Error('provider-private');return {users:structuredClone(users),pageToken:'next-page'};},
   async getUser(uid){events.push('get:'+uid);const user=users.find(user=>user.uid===uid);if(!user){const error=new Error('missing');error.code='auth/user-not-found';throw error;}return structuredClone(user);},
   async setCustomUserClaims(uid,value){events.push('claims:'+uid);if(input.claimsError)throw new Error('provider-private');claims.push({uid,claims:value});},
   async revokeRefreshTokens(uid){events.push('revoke:'+uid);if(input.revokeError)throw new Error('provider-private');}
  };
  const shared=loadModule('flower.com/backend/shared/firebase-admin.js',{'firebase-admin/app':{},'firebase-admin/auth':{},'firebase-admin/firestore':{}});
  const common={'../shared/firebase-admin':{getAdminAuth(){if(input.authError)throw new Error('provider-private');return auth;},getAdminFirestore(){if(input.storeError)throw new Error('provider-private');return db;},verifyAdminRequest:shared.verifyAdminRequest,verifyOwnerRequest:shared.verifyOwnerRequest,sendError:(res,status,code,error)=>res.status(status).json({code,error})}};
  const audit=loadModule('flower.com/backend/admin/audit.js',common);
  const handler=input.kind==='audit'?audit:input.kind==='admin'?loadModule('flower.com/backend/admin/users.js',{...common,'./audit':audit}):loadModule('flower.com/backend/admin/owner-users.js',{
   ...common,'./roles':{async syncAdminRole(db,user,admin){events.push('mirror:'+user.uid);if(input.mirrorError)throw new Error('provider-private');mirrors.push({uid:user.uid,admin,claims:user.customClaims});},async syncAdminRoles(db,users){events.push('backfill');if(input.backfillError)throw new Error('provider-private');}},
   '../shared/admin-audit':{async recordAdminAudit(db,actor,action,resourceType,resourceId,details){events.push('audit-write');if(input.auditError)throw new Error('provider-private');audits.push({actorUid:actor.uid,action,resourceType,resourceId,details});}}
  });
  const res=response();await handler({method:input.method??(input.kind==='audit'?'GET':'POST'),body:input.body,query:input.query||{},headers:{authorization:input.authorization??'Bearer actor'}},res);
  output.push({...input,users,entries,expected:{status:res.statusCode,body:res.body,cache:res.headers['Cache-Control']??null,allow:res.headers.Allow??null,tokenReads,roleReads,events,claims,mirrors,audits}});
 }
 process.stdout.write(JSON.stringify(output));
})().catch(error=>{console.error(error);process.exitCode=1;});
