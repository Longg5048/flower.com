const path=require('node:path');
const root=path.resolve(__dirname,'../../..');
const {loadModule,response}=require(path.join(root,'tests/helpers/api-fixture'));
const zone={id:'a',province:'Hà Nội',district:'Ba Đình',fee:10000,enabled:true};
const disabled={id:'b',province:'Hà Nội',district:'Đống Đa',fee:20000,enabled:false};
const tiers=[{maxStems:10,fee:15000},{maxStems:50,fee:30000}];
const zones={zones:[zone,disabled,{id:'bad',fee:-1,enabled:true}]};
const cases=[
 {name:'public zones',kind:'zones'},{name:'manage zones',kind:'zones',query:{manage:'1'}},
 {name:'manage true public',kind:'zones',query:{manage:'true'}},{name:'manage repeated public',kind:'zones',query:{manage:['1','1']}},
 {name:'case sensitive manage',kind:'zones',query:{Manage:'1'}},
 {name:'missing zones',kind:'zones',missing:true},{name:'malformed zones',kind:'zones',document:{zones:{}}},
 {name:'zones config',kind:'zones',configError:true},{name:'config before method',kind:'zones',configError:true,method:'DELETE'},
 {name:'zones unsupported method',kind:'zones',method:'DELETE'},
 {name:'zones missing token',kind:'zones',query:{manage:'1'},authorization:''},
 {name:'zones string admin',kind:'zones',query:{manage:'1'},admin:'true'},
 {name:'zones role removed',kind:'zones',query:{manage:'1'},roleMissing:true},
 {name:'zones role outage',kind:'zones',query:{manage:'1'},roleError:true},
 {name:'zones auth config',kind:'zones',query:{manage:'1'},authError:true},
 {name:'zones read outage',kind:'zones',readError:true},
 {name:'zones bad action',kind:'zones',method:'POST',body:{}},
 {name:'zones upsert',kind:'zones',method:'POST',body:{action:'upsert',zone:{...zone,fee:15000}}},
 {name:'zones insert',kind:'zones',method:'POST',body:{action:'upsert',zone:{id:'c',province:' TP Hồ Chí Minh ',district:' Quận 1 ',fee:0,enabled:true}}},
 {name:'zones duplicate disabled',kind:'zones',method:'POST',body:{action:'upsert',zone:{...disabled,id:'c',enabled:true}}},
 {name:'zones duplicate case',kind:'zones',method:'POST',body:{action:'upsert',zone:{...zone,id:'c',province:'HÀ NỘI',district:'BA ĐÌNH'}}},
 {name:'zones delete',kind:'zones',method:'POST',body:{action:'delete',id:'a'}},
 {name:'zones delete missing',kind:'zones',method:'POST',body:{action:'delete',id:'missing'}},
 {name:'zones invalid fee',kind:'zones',method:'POST',body:{action:'upsert',zone:{...zone,fee:'12000'}}},
 {name:'zones invalid enabled',kind:'zones',method:'POST',body:{action:'upsert',zone:{...zone,enabled:'true'}}},
 {name:'zones transaction outage',kind:'zones',method:'POST',body:{action:'delete',id:'a'},transactionError:true},
 {name:'unknown route',kind:'zones',query:{route:'x'}},{name:'empty route',kind:'zones',query:{route:''}},
 {name:'wrapping public',kind:'wrapping'},{name:'wrapping manage',kind:'wrapping',query:{manage:'1'}},
 {name:'wrapping missing',kind:'wrapping',missing:true},{name:'wrapping malformed',kind:'wrapping',document:{tiers:[{maxStems:10,fee:1000}]}},
 {name:'wrapping config',kind:'wrapping',configError:true},{name:'wrapping auth config',kind:'wrapping',query:{manage:'1'},authError:true},
 {name:'wrapping missing token',kind:'wrapping',query:{manage:'1'},authorization:''},
 {name:'wrapping revoked token',kind:'wrapping',query:{manage:'1'},tokenError:true},
 {name:'wrapping no role',kind:'wrapping',query:{manage:'1'},roleMissing:true},
 {name:'wrapping read outage',kind:'wrapping',readError:true},{name:'wrapping unsupported',kind:'wrapping',method:'PATCH'},
 {name:'wrapping save',kind:'wrapping',method:'POST',body:{tiers:[{maxStems:20,fee:0},{maxStems:50,fee:50000}]}},
 {name:'wrapping same still audits',kind:'wrapping',method:'POST',body:{tiers}},
 {name:'wrapping empty tiers',kind:'wrapping',method:'POST',body:{tiers:[]}},
 {name:'wrapping incomplete tiers',kind:'wrapping',method:'POST',body:{tiers:[{maxStems:49,fee:10}]}},
 {name:'wrapping unordered tiers',kind:'wrapping',method:'POST',body:{tiers:[{maxStems:50,fee:10},{maxStems:10,fee:10}]}},
 {name:'wrapping string fee',kind:'wrapping',method:'POST',body:{tiers:[{maxStems:50,fee:'1000'}]}},
 {name:'wrapping negative fee',kind:'wrapping',method:'POST',body:{tiers:[{maxStems:50,fee:-1}]}},
 {name:'wrapping transaction outage',kind:'wrapping',method:'POST',body:{tiers},transactionError:true}
];
(async()=>{
 const output=[];
 for(const input of cases){
  const document=Object.hasOwn(input,'document')?input.document:input.kind==='zones'?zones:{tiers};
  let tokenReads=0,roleReads=0,metadataReads=0;const writes=[];
  const snapshot=()=>({data:()=>input.missing?undefined:document});
  const db={collection(collection){return {doc(id=collection==='adminAudit'?'audit-entry':undefined){return {collection,id,async get(){
   if(collection==='adminRoles'){roleReads++;if(input.roleError)throw new Error('provider-private');return {exists:!input.roleMissing,data:()=>({role:'admin'})};}
   metadataReads++;if(input.readError)throw new Error('provider-private');return snapshot();
  }};}};},async runTransaction(work){if(input.transactionError)throw new Error('provider-private');const pending=[];const result=await work({get:ref=>ref.get(),set:(ref,data)=>pending.push({collection:ref.collection,id:ref.id,operation:'set',data}),create:(ref,data)=>pending.push({collection:ref.collection,id:ref.id,operation:'create',data})});writes.push(...pending);return result;}};
  const firestore={FieldValue:{serverTimestamp:()=>'$serverTimestamp'}};
  const shared=loadModule('flower.com/backend/shared/firebase-admin.js',{'firebase-admin/app':{},'firebase-admin/auth':{},'firebase-admin/firestore':{}});
  const dependencies={
   'firebase-admin/firestore':firestore,'../shared/admin-audit':loadModule('flower.com/backend/shared/admin-audit.js',{'firebase-admin/firestore':firestore}),
   '../shared/firebase-admin':{getAdminFirestore(){if(input.configError)throw new Error('provider-private');return db;},getAdminAuth(){if(input.authError)throw new Error('provider-private');return {async verifyIdToken(raw,revoked){tokenReads++;if(!revoked)throw new Error('revocation disabled');if(input.tokenError)throw new Error('provider-private');return {uid:'user-a',admin:input.admin??true,email:'admin@example.test'};}};},verifyAdminRequest:shared.verifyAdminRequest,sendError:(res,status,code,error)=>res.status(status).json({code,error})}
  };
  const wrapping=loadModule('flower.com/backend/shop-settings/bouquet-wrapping.js',dependencies);
  const handler=input.kind==='wrapping'?wrapping:loadModule('flower.com/backend/shop-settings/delivery-zones.js',{...dependencies,'./bouquet-wrapping':wrapping});
  const res=response();await handler({method:input.method||'GET',query:input.query||{},body:input.body,headers:{authorization:input.authorization??'Bearer admin'}},res);
  output.push({...input,document,expected:{status:res.statusCode,body:res.body,cache:res.headers['Cache-Control']??null,allow:res.headers.Allow??null,tokenReads,roleReads,metadataReads,writes}});
 }
 process.stdout.write(JSON.stringify(output));
})().catch(error=>{console.error(error);process.exitCode=1;});
