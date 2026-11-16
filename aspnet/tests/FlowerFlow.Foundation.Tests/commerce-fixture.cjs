// Execute real CJS backend modules with only Firebase/clock/random boundaries replaced.
const fs=require('node:fs'),path=require('node:path'),{Module,createRequire}=require('node:module');
const root=path.resolve(__dirname,'../../..'),backend=path.join(root,'flower.com/backend');
const OriginalDate=Date;let clock=OriginalDate.parse('2026-11-16T03:00:00.123Z');
global.Date=class extends OriginalDate{constructor(...args){super(...(args.length?args:[clock]));}static now(){return clock;}};
console.info=(...args)=>console.error(...args);
class Timestamp{
 constructor(seconds,nanos){this._seconds=seconds;this._nanoseconds=nanos;}
 static fromMillis(value){const seconds=Math.floor(value/1000);return new Timestamp(seconds,Math.floor((value-seconds*1000)*1e6));}
 static fromDate(value){return Timestamp.fromMillis(value.getTime());}static now(){return Timestamp.fromMillis(clock);}
 toMillis(){return this._seconds*1000+this._nanoseconds/1e6;}toDate(){return new Date(this.toMillis());}
 toJSON(){return{_seconds:this._seconds,_nanoseconds:this._nanoseconds};}
}
const server=Symbol('server-time'),deleted=Symbol('delete-field');
const FieldValue={serverTimestamp:()=>server,delete:()=>deleted,increment:value=>({$increment:value})};
function clone(value){if(value instanceof Timestamp)return new Timestamp(value._seconds,value._nanoseconds);if(Buffer.isBuffer(value))return Buffer.from(value);if(Array.isArray(value))return value.map(clone);if(value&&typeof value==='object')return Object.fromEntries(Object.entries(value).map(([k,v])=>[k,clone(v)]));return value;}
function decode(value){if(value&&value.$timestamp!==undefined)return Timestamp.fromMillis(typeof value.$timestamp==='number'?value.$timestamp:OriginalDate.parse(value.$timestamp));if(value&&value.$bytes)return Buffer.from(value.$bytes,'base64');if(Array.isArray(value))return value.map(decode);if(value&&typeof value==='object')return Object.fromEntries(Object.entries(value).map(([k,v])=>[k,decode(v)]));return value;}
function persisted(value){if(value===undefined)throw new Error('Undefined Firestore field');if(value===server)return Timestamp.now();if(Array.isArray(value))return value.map(persisted);if(value instanceof Timestamp||Buffer.isBuffer(value))return clone(value);if(value&&typeof value==='object')return Object.fromEntries(Object.entries(value).filter(([,v])=>v!==deleted).map(([k,v])=>[k,persisted(v)]));return value;}
function comparable(value){return value instanceof Timestamp?value.toMillis():value;}
function fixture(seed={},now='2026-11-16T03:00:00.123Z'){
 clock=OriginalDate.parse(now);process.env.FIREBASE_SERVICE_ACCOUNT_JSON='test-commerce-secret';
 process.env.CRON_SECRET='test-cron';process.env.VNPAY_TMN_CODE='test-tmn';process.env.VNPAY_HASH_SECRET='test-vnpay-hash';process.env.VNPAY_ENV='sandbox';process.env.PUBLIC_APP_URL='https://flower.example.test';
 const records=new Map(Object.entries(seed).map(([key,value])=>[key,decode(value)]));let ids=0,random=0,queue=Promise.resolve();
 const snap=ref=>({id:ref.id,ref,exists:records.has(ref.path),data:()=>clone(records.get(ref.path)),get:field=>clone(records.get(ref.path)?.[field])});
 const ref=key=>({path:key,id:key.split('/').at(-1),get:async()=>snap(ref(key)),create:async data=>{if(records.has(key))throw Object.assign(new Error('Already exists'),{code:6});records.set(key,persisted(data));},set:async(data,options)=>{records.set(key,options?.merge?{...records.get(key),...persisted(data)}:persisted(data));},delete:async()=>{records.delete(key);}});
 function query(collection,filters=[],order=null,limit=Infinity){return{
  doc(id){return ref(collection+'/'+(id===undefined?'auto-'+(++ids):id));},
  where(field,operator,value){return query(collection,[...filters,{field,operator,value}],order,limit);},
  orderBy(field,direction='asc'){return query(collection,filters,{field,direction},limit);},limit(value){return query(collection,filters,order,value);},
  async get(){let docs=[...records.keys()].filter(key=>key.startsWith(collection+'/')&&!key.slice(collection.length+1).includes('/')).map(key=>snap(ref(key)));
   docs=docs.filter(doc=>filters.every(({field,operator,value})=>{const actual=doc.data()?.[field];if(actual===undefined)return false;const a=comparable(actual),b=comparable(value);return operator==='=='?a===b:operator==='<='?a<=b:operator==='<'?a<b:operator==='>='?a>=b:operator==='>'?a>b:operator==='in'?value.includes(actual):false;}));
   if(order)docs=docs.filter(doc=>doc.data()?.[order.field]!==undefined).sort((a,b)=>{const x=comparable(a.data()[order.field]),y=comparable(b.data()[order.field]);return(x<y?-1:x>y?1:0)*(order.direction==='desc'?-1:1);});
   docs=docs.slice(0,limit);return{docs,size:docs.length,empty:docs.length===0};}
 };}
 const db={records,collection:name=>query(name),getAll:async(...refs)=>refs.map(snap),batch(){const pending=[];return{set:(r,d)=>pending.push(()=>records.set(r.path,persisted(d))),delete:r=>pending.push(()=>records.delete(r.path)),async commit(){pending.forEach(work=>work());}};},
  runTransaction(work){const run=queue.then(async()=>{const pending=[];const result=await work({get:async r=>snap(r),getAll:async(...refs)=>refs.map(snap),
   set:(r,data,options)=>pending.push(()=>records.set(r.path,options?.merge?{...records.get(r.path),...persisted(data)}:persisted(data))),
   create:(r,data)=>pending.push(()=>{if(records.has(r.path))throw Object.assign(new Error('Already exists'),{code:6});records.set(r.path,persisted(data));}),
   update:(r,data)=>pending.push(()=>{if(!records.has(r.path))throw new Error('Missing update target');const next={...records.get(r.path)};for(const[k,v]of Object.entries(data)){if(v===deleted)delete next[k];else if(v&&v.$increment!==undefined)next[k]=(Number(next[k])||0)+v.$increment;else next[k]=persisted(v);}records.set(r.path,next);}),delete:r=>pending.push(()=>records.delete(r.path))});
   const before=new Map([...records].map(([k,v])=>[k,clone(v)]));try{pending.forEach(work=>work());}catch(error){records.clear();for(const[k,v]of before)records.set(k,v);throw error;}return result;});queue=run.catch(()=>{});return run;}
 };
 const sharedPath=path.join(backend,'shared/firebase-admin.js');const modules=new Map();
 const auth={async verifyIdToken(token,revoked){if(!revoked)throw new Error('Revoked check disabled');if(token==='revoked')throw Object.assign(new Error('Revoked'),{code:'auth/id-token-revoked'});if(token==='admin')return{uid:'admin',email:'admin@example.test',admin:true};if(token==='owner')return{uid:'owner',email:'owner@example.test',admin:true,role:'owner'};if(token==='customer')return{uid:'customer',email:'customer@example.test'};throw Object.assign(new Error('Invalid'),{code:'auth/invalid-id-token'});}};
 function load(filename){if(modules.has(filename))return modules.get(filename).exports;
  const module=new Module(filename);module.filename=filename;modules.set(filename,module);const native=createRequire(filename);
  module.require=name=>{if(name==='firebase-admin/firestore')return{FieldValue,Timestamp};if(name==='node:crypto')return{...native(name),randomBytes:size=>Buffer.alloc(size,++random)};
   const resolved=name.startsWith('.')?path.resolve(path.dirname(filename),name.endsWith('.js')?name:name+'.js'):null;
   if(resolved===sharedPath)return{...loadShared(),getAdminAuth:()=>auth,getAdminFirestore:()=>db};
   if(resolved&&resolved.startsWith(backend+path.sep))return load(resolved);return native(name);};
  module._compile(fs.readFileSync(filename,'utf8'),filename);return module.exports;
 }
 function loadShared(){const file=new Module(sharedPath);file.filename=sharedPath;file.require=()=>({});file._compile(fs.readFileSync(sharedPath,'utf8'),sharedPath);return file.exports;}
 return{records,db,load:relative=>load(path.join(backend,relative)),advance:milliseconds=>{clock+=milliseconds;},async call(relative,request={}){
  const headers={};let status=200,body;const res={setHeader(k,v){headers[k]=v;},status(v){status=v;return this;},json(v){body=v;return this;}};
  await load(path.join(backend,relative))({method:'POST',body:{},query:{},...request,headers:{'x-forwarded-for':'203.0.113.9',...request.headers}},res);
  return{status,body,cache:headers['Cache-Control']??null,allow:headers.Allow??null,retry:headers['Retry-After']??null};
 }};
}
module.exports={fixture,Timestamp};
