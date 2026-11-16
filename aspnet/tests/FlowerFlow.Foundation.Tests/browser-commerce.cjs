const {chromium}=require('../../../flower.com/node_modules/@playwright/test');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});
 try {
  const page=await browser.newPage();
  await page.route('**/browser-contract.html',r=>r.fulfill({contentType:'text/html',body:'<!doctype html><html><head></head><body>Frontend contract</body></html>'}));
  await page.route('https://www.gstatic.com/firebasejs/**',r=>{
   const url=r.request().url();const body=url.endsWith('firebase-app.js')?'export const getApps=()=>[]; export const initializeApp=()=>({});':url.endsWith('firebase-auth.js')?'export const getAuth=()=>({currentUser:null,authStateReady:async()=>{}});':'export const getFirestore=()=>({});';
   return r.fulfill({contentType:'text/javascript',headers:{'Access-Control-Allow-Origin':'*'},body});
  });
  await page.goto(process.argv[2]+'browser-contract.html');
  await page.addScriptTag({url:'/js/order/order-store.js'});
  const creationResponse=page.waitForResponse(r=>r.url().includes('/api/orders')&&r.status()===201);
  const result=await page.evaluate(async()=>{
   const items=[{id:'ready',quantity:1}], api=window.FlowerOrderStore;
   const quote=await api.quote(items,'','zone-a');
   const input={customerName:'Khách',phone:'0901234567',address:'1 đường Hoa',deliveryDate:'2027-01-01',deliveryTime:'08:00–11:00',deliveryZoneId:'zone-a',paymentMethod:'vnpay',idempotencyKey:crypto.randomUUID(),items};
   const order=await api.create(input,quote.totals), replay=await api.create(input,quote.totals);
   const tracking=await api.getPublicOrder(order,input.phone);
   return{quote,order,replay,tracking};
  });
  const created=(await (await creationResponse).json()).order;
  const payment=await page.evaluate(({id,paymentReference})=>window.FlowerOrderStore.createPaymentLink(id,paymentReference),created);
  assert.equal(result.quote.totals.total,110000);assert.equal(result.order,result.replay);
  assert.equal(result.tracking.id,result.order);assert.equal(result.tracking.address,undefined);
  assert.match(payment.paymentUrl,/^https:\/\/sandbox\.vnpayment\.vn\//);
  console.log('Browser frontend -> ASP.NET -> Firestore: quote, idempotent checkout, tracking privacy and VNPay link passed.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
