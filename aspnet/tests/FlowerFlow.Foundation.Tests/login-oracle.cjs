const path=require('node:path');const root=path.resolve(__dirname,'../../..');
const {memoryDb}=require(path.join(root,'tests/helpers/login-memory-db'));
const {createLoginProtection}=require(path.join(root,'flower.com/backend/accounts/login-protection'));
const {createLoginHandler}=require(path.join(root,'flower.com/backend/accounts/login'));
const escalation=[...Array.from({length:4},()=>({})),{}];
for(const minutes of [15,30,60,120,240]){
 escalation.push({advance:minutes*60000-1,valid:true});
 escalation.push({advance:1});
}
escalation.push({advance:86400000,valid:true});escalation.push({version:1001,valid:true});
const scenarios=[
 {name:'validation and method',steps:[{method:'GET'},{body:{}},{body:{email:'bad',password:'x'}},{body:{email:'a@b.c',password:123}},{body:{email:'a@b.c',password:''}}]},
 {name:'escalation reset and password version',steps:escalation},
 {name:'case normalization and success clears history',steps:[{}, {body:{email:' CUSTOMER@EXAMPLE.TEST ',password:'x'}},{valid:true},{},{valid:true}]},
 {name:'outage preserves failures and releases lease',steps:[{},{},{},{},{outage:true},{outage:true},{}]},
 {name:'preflight and lookup failures',steps:[{ready:false},{lookupError:true},{}]},
 {name:'mint failure after successful password check',steps:[{valid:true,mintError:true},{valid:true}]}
];
(async()=>{
 const output=[];
 for(const scenario of scenarios){
  const db=memoryDb();let now=1800000000000,version=1000,current={};let readyCalls=0,lookups=0,verified=0,minted=0;
  const protection=createLoginProtection(db,{now:()=>now});
  const provider={async assertReady(){readyCalls++;if(current.ready===false)throw new Error('provider-private');},async lookup(email){lookups++;if(current.lookupError)throw new Error('provider-private');return{key:email,uid:email,passwordUpdatedAt:version};},async verifyPassword(){verified++;if(current.outage)throw new Error('provider-private');return !!current.valid;}};
  const handler=createLoginHandler({provider,protection,auth:{async createCustomToken(){minted++;if(current.mintError)throw new Error('provider-private');return 'session-token';}}});
  const steps=[];
  for(const operation of scenario.steps){
   current=operation;now+=operation.advance||0;version=operation.version??version;
   const headers={};let status=200,body;
   await handler({method:operation.method||'POST',body:operation.body??{email:'customer@example.test',password:'private-password'},headers:{}},{setHeader(k,v){headers[k]=v;},status(value){status=value;return this;},json(value){body=value;return this;}});
   const states={};for(const [key,state]of db.records){states[key]={...state,leaseId:state.leaseId?'<lease>':''};}
   steps.push({operation,now,version,expected:{status,body,cache:headers['Cache-Control']??null,allow:headers.Allow??null,retry:headers['Retry-After']??null,readyCalls,lookups,verified,minted,states}});
  }
  output.push({name:scenario.name,steps});
 }
 process.stdout.write(JSON.stringify(output));
})().catch(error=>{console.error(error);process.exitCode=1;});
