const path = require('node:path');
const {createRequire} = require('node:module');
const root = path.resolve(__dirname, '../../..');
const {loadModule, response} = require(path.join(root, 'tests/helpers/api-fixture'));
const {Timestamp} = createRequire(path.join(root, 'flower.com/package.json'))('firebase-admin/firestore');
const seed = [
  {id:'z-doc', fields:{title:'Z', status:'active', price:123000, quantity:3, tags:['one'], custom:{keep:true}}},
  {id:'a-doc', fields:{id:null, title:'A', status:'hidden'}},
  {id:'archive', fields:{status:'archived', nullable:null}},
  {id:'vn-flower-01', fields:{title:'B? hoa', image:'/images/flower-01.webp?old=1', images:['/img/p/GEN/lgwt/17.jpg', './new.webp', null], createdAt:{$timestamp:'2026-11-16T03:04:05.123Z'}, nested:{at:{$timestamp:'2026-11-16T03:04:05.123Z'}}}},
  {id:'numeric', fields:{id:0, title:'?', image:'/images/flower-02.webp'}},
  {id:'unknown', fields:{id:'unknown', title:'?', image:'/images/flower-02.webp'}},
  {id:'vn-flower-02', fields:{title:'Keep', image:'./new.webp', images:['/images/flower-01.webp']}},
  {id:'fallback', fields:{id:'vn-flower-03', title:'?', image:'https://example.org/img/p/GEN/lgwt/22.JPG#x', images:[]}}
];
const cases = [
  {name:'public'}, {name:'public ignores bearer', tokenError:true, authorization:'Bearer bad'},
  {name:'manage true is public', query:{manage:'true'}}, {name:'manage repeated is public', query:{manage:['1','1']}},
  {name:'manage missing token', query:{manage:'1'}},
  {name:'manage revoked', query:{manage:'1'}, authorization:'Bearer bad', tokenError:true},
  {name:'manage customer', query:{manage:'1'}, authorization:'Bearer customer', roleExists:true},
  {name:'manage role removed', query:{manage:'1'}, authorization:'Bearer admin', admin:true},
  {name:'manage admin', query:{manage:'1'}, authorization:'Bearer admin', admin:true, roleExists:true},
  {name:'manage owner', query:{manage:'1'}, authorization:'Bearer owner', admin:true, claimRole:'owner', roleExists:true},
  {name:'manage string admin', query:{manage:'1'}, authorization:'Bearer admin', admin:'true', roleExists:true},
  {name:'role outage', query:{manage:'1'}, authorization:'Bearer admin', admin:true, roleError:true},
  {name:'customer role outage', query:{manage:'1'}, authorization:'Bearer customer', roleError:true},
  {name:'configuration missing', configError:true},
  {name:'config before missing token', configError:true, query:{manage:'1'}},
  {name:'product outage', readError:true},
  {name:'post', method:'POST'}, {name:'delete', method:'DELETE'}, {name:'options', method:'OPTIONS'},
  {name:'unknown route', query:{route:'other'}}, {name:'empty route', query:{route:''}},
  {name:'repeated route', query:{route:['x','x']}}, {name:'empty products', empty:true},
  {name:'uppercase manage is public',query:{Manage:'1'}},
  {name:'uppercase route ignored',query:{Route:'x'}},
  {name:'case variants not merged',query:{manage:'1',Manage:'false'},authorization:'Bearer admin',admin:true,roleExists:true},
  {name:'uppercase route not merged',query:{Route:'x',route:'other'}},
  {name:'route dispatch before method',method:'POST',query:{route:'other'}},
  {name:'tab bearer',query:{manage:'1'},authorization:'bEaReR\ttoken',admin:true,roleExists:true}
];
function revive(value) {
  if (value && value.$timestamp) return Timestamp.fromDate(new Date(value.$timestamp));
  if (Array.isArray(value)) return value.map(revive);
  if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([k,v])=>[k,revive(v)]));
  return value;
}
(async()=>{
  const output=[];
  for(const input of cases) {
    let roleReads=0, productReads=0, tokenReads=0;
    const docs = (input.empty?[]:seed).map(doc=>({id:doc.id,data:()=>revive(doc.fields)}));
    const db = {collection(name){
      if(name==='products') return {async get(){productReads++; if(input.readError) throw new Error('private-detail'); return {docs};}};
      if(name==='adminRoles') return {doc(uid){if(uid!=='user-a') throw new Error('wrong uid'); return {async get(){roleReads++; if(input.roleError) throw new Error('private-detail'); return {exists:!!input.roleExists};}};}};
      throw new Error('unexpected collection');
    }};
    const dependency={
      getAdminFirestore(){if(input.configError)throw new Error('private-detail');return db;},
      getAdminAuth(){return {async verifyIdToken(token,revoked){tokenReads++;if(!revoked)throw new Error('revocation disabled');if(input.tokenError)throw new Error('private-detail');return {uid:'user-a',admin:input.admin,role:input.claimRole};}};},
      sendError:(res,status,code,error)=>res.status(status).json({code,error})
    };
    const shared=loadModule('flower.com/backend/shared/firebase-admin.js',{'firebase-admin/app':{},'firebase-admin/auth':{},'firebase-admin/firestore':{}});
    dependency.verifyRequest=shared.verifyRequest;
    const handler=loadModule('flower.com/backend/catalog/products.js',{'../shared/firebase-admin':dependency,'./flower-prices':()=>{throw new Error('outside this migration');}});
    const res=response();
    await handler({method:input.method||'GET',query:input.query||{},headers:{authorization:input.authorization}},res);
    output.push({...input,seed:input.empty?[]:seed,expected:{status:res.statusCode,body:res.body,cache:res.headers['Cache-Control']??null,allow:res.headers.Allow??null,roleReads,productReads,tokenReads}});
  }
  process.stdout.write(JSON.stringify(output));
})().catch(e=>{console.error(e);process.exitCode=1;});
