const path=require('node:path');
const root=path.resolve(__dirname,'../../..');
const {loadModule,response}=require(path.join(root,'tests/helpers/api-fixture'));
const bytes=Buffer.from([0xff,0xd8,0xff,0xc0,0,8,8,0,100,0,100,0,0xff,0xd9]);
const photo='data:image/jpeg;base64,'+bytes.toString('base64');
const productId='a'.repeat(40),reviewId='b'.repeat(40),reviewPhoto=reviewId+'_0';
const cases=[
 {name:'product public',kind:'product',query:{id:productId}},
 {name:'product ignores revoked bearer',kind:'product',query:{id:productId},tokenError:true,authorization:'Bearer token'},
 {name:'product missing id',kind:'product'}, {name:'product uppercase id',kind:'product',query:{id:'A'.repeat(40)}},
 {name:'product repeated id',kind:'product',query:{id:[productId,productId]}},
 {name:'product uppercase query key',kind:'product',query:{ID:productId}},
 {name:'product missing doc',kind:'product',query:{id:productId},photoMissing:true},
 {name:'product store failure',kind:'product',query:{id:productId},storeError:true},
 {name:'product malformed bytes',kind:'product',query:{id:productId},malformedBytes:true},
 {name:'product unsupported method',kind:'product',method:'DELETE'},
 {name:'upload',kind:'product',method:'POST',body:{photo}},
 {name:'duplicate upload',kind:'product',method:'POST',body:{photo},duplicate:true},
 {name:'upload no token',kind:'product',method:'POST',body:{photo},authorization:''},
 {name:'upload revoked token',kind:'product',method:'POST',body:{photo},tokenError:true},
 {name:'upload customer',kind:'product',method:'POST',body:{photo},admin:false},
 {name:'upload string admin',kind:'product',method:'POST',body:{photo},admin:'true'},
 {name:'upload role missing',kind:'product',method:'POST',body:{photo},roleMissing:true},
 {name:'upload customer role outage',kind:'product',method:'POST',body:{photo},admin:false,roleError:true},
 {name:'upload auth configuration',kind:'product',method:'POST',body:{photo},authError:true},
 {name:'upload store configuration',kind:'product',method:'POST',body:{photo},storeError:true},
 {name:'upload create outage',kind:'product',method:'POST',body:{photo},createError:true},
 {name:'upload wrong photo',kind:'product',method:'POST',body:{photo:'data:image/png;base64,AAAA'}},
 {name:'upload missing photo',kind:'product',method:'POST',body:{}},
 {name:'review approved',kind:'review',query:{id:reviewPhoto}},
 {name:'review approved ignores revoked token',kind:'review',query:{id:reviewPhoto},tokenError:true},
 {name:'review missing id',kind:'review'},
 {name:'review index out of range',kind:'review',query:{id:reviewId+'_3'}},
 {name:'review missing document',kind:'review',query:{id:reviewPhoto},reviewMissing:true},
 {name:'review unapproved anonymous',kind:'review',query:{id:reviewPhoto},moderation:'pending',authorization:''},
 {name:'review unapproved wrong scheme',kind:'review',query:{id:reviewPhoto},moderation:'pending',authorization:'Basic token'},
 {name:'review author',kind:'review',query:{id:reviewPhoto},moderation:'pending',author:'user-a',admin:false},
 {name:'review other customer',kind:'review',query:{id:reviewPhoto},moderation:'pending',admin:false},
 {name:'review admin',kind:'review',query:{id:reviewPhoto},moderation:'pending'},
 {name:'review admin role missing',kind:'review',query:{id:reviewPhoto},moderation:'pending',roleMissing:true},
 {name:'review admin string',kind:'review',query:{id:reviewPhoto},moderation:'pending',admin:'true'},
 {name:'review role outage',kind:'review',query:{id:reviewPhoto},moderation:'pending',roleError:true},
 {name:'review revoked token',kind:'review',query:{id:reviewPhoto},moderation:'pending',tokenError:true},
 {name:'review auth configuration',kind:'review',query:{id:reviewPhoto},moderation:'pending',authError:true},
 {name:'review photo not listed',kind:'review',query:{id:reviewPhoto},unlisted:true},
 {name:'review photo IDs malformed',kind:'review',query:{id:reviewPhoto},badPhotoIds:true},
 {name:'review photo missing',kind:'review',query:{id:reviewPhoto},photoMissing:true},
 {name:'review photo belongs elsewhere',kind:'review',query:{id:reviewPhoto},wrongReview:true},
 {name:'review wrong relation before bytes',kind:'review',query:{id:reviewPhoto},wrongReview:true,malformedBytes:true},
 {name:'review malformed bytes',kind:'review',query:{id:reviewPhoto},malformedBytes:true},
 {name:'review store failure',kind:'review',query:{id:reviewPhoto},storeError:true},
 {name:'review unsupported method',kind:'review',method:'POST',authError:true}
];
(async()=>{
 const output=[];
 for(const input of cases){
  let tokenReads=0,roleReads=0,reviewReads=0,photoReads=0,createAttempts=0;const writes=[];
  const auth={async verifyIdToken(raw,revoked){tokenReads++;if(!revoked)throw new Error('revocation disabled');if(input.tokenError)throw new Error('provider-private');return {uid:'user-a',admin:input.admin??true};}};
  const db={collection(collection){return {doc(id){return {async get(){
   if(collection==='adminRoles'){roleReads++;if(input.roleError)throw new Error('provider-private');return {exists:!input.roleMissing};}
   if(collection==='reviews'){reviewReads++;return {id,exists:!input.reviewMissing,data:()=>({moderationStatus:input.moderation??'approved',authorUid:input.author??'other-user',photoIds:input.badPhotoIds?{}:input.unlisted?[]:[reviewPhoto]})};}
   photoReads++;return {exists:!input.photoMissing,data:()=>({reviewId:input.wrongReview?'wrong-review':reviewId,bytes:input.malformedBytes?null:bytes})};
  },async create(data){createAttempts++;if(input.duplicate){const error=new Error('duplicate');error.code=6;throw error;}if(input.createError)throw new Error('provider-private');writes.push({id,bytes:data.bytes.toString('base64'),size:data.size,uploadedBy:data.uploadedBy,createdAt:data.createdAt});}};}};}};
  const shared=loadModule('flower.com/backend/shared/firebase-admin.js',{'firebase-admin/app':{},'firebase-admin/auth':{},'firebase-admin/firestore':{}});
  const handler=loadModule('flower.com/backend/catalog/'+(input.kind==='product'?'product-images':'review-images')+'.js',{
   'firebase-admin/firestore':{FieldValue:{serverTimestamp:()=>'$serverTimestamp'}},
   '../shared/firebase-admin':{getAdminAuth(){if(input.authError)throw new Error('provider-private');return auth;},getAdminFirestore(){if(input.storeError)throw new Error('provider-private');return db;},verifyRequest:shared.verifyRequest,sendError:(res,status,code,error)=>res.status(status).json({code,error})}
  });
  const res=response();
  // Match lib/local-server.js: res.json replaces a partially set image Content-Type on errors.
  res.json=function(body){this.headers['Content-Type']='application/json; charset=utf-8';this.body=body;return this;};
  res.end=function(content){this.binary=Buffer.from(content).toString('base64');return this;};
  await handler({method:input.method||'GET',body:input.body,query:input.query||{},headers:{authorization:input.authorization??'Bearer token'}},res);
  output.push({...input,expected:{status:res.statusCode,body:res.body??null,binary:res.binary??null,cache:res.headers['Cache-Control']??null,allow:res.headers.Allow??null,nosniff:res.headers['X-Content-Type-Options'],contentType:res.headers['Content-Type']?.split(';')[0]??null,length:res.headers['Content-Length']??null,tokenReads,roleReads,reviewReads,photoReads,createAttempts,writes}});
 }
 process.stdout.write(JSON.stringify(output));
})().catch(error=>{console.error(error);process.exitCode=1;});
