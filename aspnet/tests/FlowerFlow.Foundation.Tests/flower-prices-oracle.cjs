const path=require('node:path');
const root=path.resolve(__dirname,'../../..');
const {loadModule,response}=require(path.join(root,'tests/helpers/api-fixture'));
const OriginalDate=Date;
global.Date=class extends OriginalDate {
  constructor(...args){super(...(args.length?args:['2026-11-16T18:30:00.123Z']));}
  static now(){return new OriginalDate('2026-11-16T18:30:00.123Z').getTime();}
};
const seed=[
 {id:'rose',fields:{title:'Hoa hồng',saleType:'flower',price:12000,unit:'cành',image:'rose.webp',priceUpdatedByEmail:'previous@example.test'}},
 {id:'a',fields:{title:'Hoa Á',saleType:'flower',price:73.99,priceChangedAt:{$timestamp:'2026-11-15T02:03:04.123Z'}}},
 {id:'b',fields:{title:'Hoa A',saleType:'flower',price:'12000',priceUpdatedAt:'2026-11-10'}},
 {id:'d',fields:{title:'Đào',saleType:'flower',price:null}},
 {id:'z',fields:{title:'Bó hoa',saleType:'ready_made',price:200000}}
];
const history=[
 {id:'h1',fields:{flowerId:'rose',oldPrice:10000,newPrice:11000,updatedAt:{$timestamp:'2026-11-13T12:00:00.000Z'},updatedBy:'old-owner'}},
 {id:'h2',fields:{flowerId:'rose',oldPrice:'11000',newPrice:12000,updatedAt:'2026-11-15T12:00:00.000Z',updatedByEmail:'owner@example.test'}},
 {id:'other',fields:{flowerId:'a',oldPrice:1,newPrice:2,updatedAt:'2026-11-16T12:00:00.000Z'}}
];
const cases=[
 {name:'list owner'}, {name:'history owner',query:{flowerId:'rose'}},
 {name:'missing bearer',authorization:''},{name:'revoked token',tokenError:true},
 {name:'admin not owner',claimRole:'admin'},{name:'owner without admin',admin:false},{name:'admin string',admin:'true'},
 {name:'owner removed',roleExists:false},{name:'owner role changed',documentRole:'admin'},
 {name:'auth configuration',authConfigError:true},{name:'store configuration',storeConfigError:true},
 {name:'role outage',roleError:true},{name:'list outage',readError:true},
 {name:'method before auth',method:'POST',authConfigError:true},
 {name:'empty id',query:{flowerId:''}},{name:'slash id',query:{flowerId:'a/b'}},{name:'repeated id',query:{flowerId:['rose','rose']}},
 {name:'history missing',query:{flowerId:'missing'}},{name:'history bouquet',query:{flowerId:'z'}},
 {name:'uppercase id ignored',query:{FlowerId:'rose'}},
 {name:'update',method:'PATCH',body:{flowerId:'rose',price:15000}},
 {name:'no change',method:'PATCH',body:{flowerId:'rose',price:12000}},
 {name:'legacy USD update',method:'PATCH',body:{flowerId:'a',price:2000000}},
 {name:'legacy USD no change',method:'PATCH',body:{flowerId:'a',price:1920000}},
 {name:'invalid current',method:'PATCH',body:{flowerId:'d',price:12000}},
 {name:'missing update',method:'PATCH',body:{flowerId:'missing',price:15000}},
 {name:'bouquet update',method:'PATCH',body:{flowerId:'z',price:15000}},
 {name:'string price',method:'PATCH',body:{flowerId:'rose',price:'15000'}},
 {name:'fraction price',method:'PATCH',body:{flowerId:'rose',price:15000.5}},
 {name:'minimum price',method:'PATCH',body:{flowerId:'rose',price:1000}},
 {name:'maximum price',method:'PATCH',body:{flowerId:'rose',price:1000000000}},
 {name:'price too low',method:'PATCH',body:{flowerId:'rose',price:999}},
 {name:'price too high',method:'PATCH',body:{flowerId:'rose',price:1000000001}},
 {name:'boolean price',method:'PATCH',body:{flowerId:'rose',price:true}},
 {name:'trimmed id rejected',method:'PATCH',body:{flowerId:' rose',price:15000}},
 {name:'empty body',method:'PATCH',body:{}},{name:'null body',method:'PATCH',body:null},
 {name:'transaction outage',method:'PATCH',body:{flowerId:'rose',price:15000},transactionError:true},
 {name:'email fallback',method:'PATCH',body:{flowerId:'rose',price:15000},email:''},
 {name:'email truncated',method:'PATCH',body:{flowerId:'rose',price:15000},email:'e'.repeat(330)},
 {name:'BOM id rejected',query:{flowerId:'\ufeffrose'}},
 {name:'control id rejected',method:'PATCH',body:{flowerId:'a\u007fb',price:15000}},
 {name:'ID length 129 rejected',query:{flowerId:'a'.repeat(129)}},
 {name:'ID length 128 accepted',query:{flowerId:'a'.repeat(128)}},
 {name:'backslash ID accepted',query:{flowerId:'a\\b'}},
 {name:'NEL is not JS whitespace',query:{flowerId:'\u0085rose'}}
];
function revive(value){
 if(value&&value.$timestamp)return {toDate:()=>new Date(value.$timestamp)};
 if(Array.isArray(value))return value.map(revive);
 if(value&&typeof value==='object')return Object.fromEntries(Object.entries(value).map(([k,v])=>[k,revive(v)]));
 return value;
}
(async()=>{
 const output=[];
 for(const input of cases){
  let roleReads=0,tokenReads=0;const writes=[];
  const token={uid:'owner-a',admin:input.admin??true,role:input.claimRole??'owner',email:input.email??'owner@example.test'};
  const db={collection(collection){
   const docs=collection==='products'?seed:history;
   const snapshot=doc=>({id:doc?.id,exists:!!doc,data:()=>revive(doc?.fields)});
   return {
    where(field,op,value){return {async get(){if(input.readError)throw new Error('provider-private');return {docs:docs.filter(d=>d.fields[field]===value).map(snapshot)};}};},
    doc(id=collection==='priceHistory'?'history-entry':'audit-entry'){
     return {collection,id,async get(){if(collection==='adminRoles'){roleReads++;if(input.roleError)throw new Error('provider-private');return {exists:input.roleExists!==false,data:()=>({role:input.documentRole??'owner'})};}
      if(input.readError)throw new Error('provider-private');return snapshot(docs.find(d=>d.id===id));}};
    }
   };
  },async runTransaction(work){
   if(input.transactionError)throw new Error('provider-private');
   const pending=[];
   const result=await work({get:ref=>ref.get(),update:(ref,data)=>pending.push({collection:ref.collection,id:ref.id,operation:'update',data}),create:(ref,data)=>pending.push({collection:ref.collection,id:ref.id,operation:'create',data})});
   writes.push(...pending);return result;
  }};
  const shared=loadModule('flower.com/backend/shared/firebase-admin.js',{'firebase-admin/app':{},'firebase-admin/auth':{},'firebase-admin/firestore':{}});
  const firestore={FieldValue:{serverTimestamp:()=>'$serverTimestamp'}};
  const audit=loadModule('flower.com/backend/shared/admin-audit.js',{'firebase-admin/firestore':firestore});
  const handler=loadModule('flower.com/backend/catalog/flower-prices.js',{
   'firebase-admin/firestore':firestore,'../shared/admin-audit':audit,
   '../shared/firebase-admin':{
    getAdminAuth(){if(input.authConfigError)throw new Error('provider-private');return {async verifyIdToken(raw,revoked){tokenReads++;if(!revoked)throw new Error('revocation disabled');if(input.tokenError)throw new Error('provider-private');return token;}};},
    getAdminFirestore(){if(input.storeConfigError)throw new Error('provider-private');return db;},
    verifyRequest:shared.verifyRequest,sendError:(res,status,code,error)=>res.status(status).json({code,error})
   }
  });
  const res=response();
  await handler({method:input.method||'GET',query:input.query||{},body:input.body,headers:{authorization:input.authorization??'Bearer owner'}},res);
  output.push({...input,seed,history,token,expected:{status:res.statusCode,body:res.body,cache:res.headers['Cache-Control']??null,allow:res.headers.Allow??null,roleReads,tokenReads,writes}});
 }
 process.stdout.write(JSON.stringify(output));
})().catch(e=>{console.error(e);process.exitCode=1;});
