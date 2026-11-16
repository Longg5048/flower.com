// Read-only checks. No login, orders, payment callbacks or database writes.
const assert=require('node:assert/strict');
const origin=new URL(process.argv[2]||'http://127.0.0.1:5087');
if(origin.protocol!=='https:'&&!['localhost','127.0.0.1','[::1]'].includes(origin.hostname))throw new Error('Use HTTPS for a remote host.');
if(origin.username||origin.password)throw new Error('Do not put credentials in the URL.');
(async()=>{
 const paths=['/','/login.html','/checkout.html','/design-request.html','/payment-result.html','/js/order/order-store.js','/js/core/firebase-config.js','/flower.json','/health'];
 for(const path of paths){const response=await fetch(new URL(path,origin),{signal:AbortSignal.timeout(15000)});assert.equal(response.status,200,path);assert.equal(response.headers.get('x-content-type-options'),'nosniff',path);assert.ok(response.headers.get('content-security-policy'),path);}
 for(const path of ['/backend/shared/firebase-admin.js','/lib/local-server.js','/.env','/firestore.rules','/package.json']){const response=await fetch(new URL(path,origin),{signal:AbortSignal.timeout(15000)});assert.equal(response.status,404,path);}
 const ready=await fetch(new URL('/health/ready',origin),{signal:AbortSignal.timeout(15000)});
 if(process.argv.includes('--require-ready'))assert.equal(ready.status,200,'Backend is not ready for traffic.');
 else assert.ok([200,503].includes(ready.status));
 console.log(`Static assets, security headers and private-source isolation passed; readiness HTTP ${ready.status}.`);
})().catch(error=>{console.error(error.message);process.exitCode=1;});
