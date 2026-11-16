const {fixture}=require('./commerce-fixture.cjs');const vnpay=require('../../../flower.com/backend/payments/vnpay');const crypto=require('node:crypto');
const first='01'.repeat(16),second='02'.repeat(16);
const seed={
 'products/ready':{title:'Bó hoa',price:100000,quantity:9},
 'orders/order-a':{uid:'customer',items:[{id:'ready',quantity:1}],total:110000,wrappingFee:0,shippingFee:10000,subtotal:100000,savings:0,
  paymentMethod:'vnpay',paymentReference:'FFREFERENCE',paymentStatus:'chua_thanh_toan',paymentVerified:false,status:'Chờ xác nhận',inventoryReserved:true,inventoryState:'reserved',
  reservationExpiresAt:{$timestamp:'2026-11-16T03:15:00.123Z'},trackingHistory:[]}
};
const create={body:{action:'create',orderId:'order-a',paymentReference:'FFREFERENCE'}};
const status=txnRef=>({method:'GET',query:{action:'status',txnRef}});
function ipn(txnRef=first,extra={},tamper=false){const query={vnp_TmnCode:'test-tmn',vnp_TxnRef:txnRef,vnp_Amount:'11000000',vnp_CurrCode:'VND',vnp_ResponseCode:'00',vnp_TransactionStatus:'00',vnp_TransactionNo:'BANK-1',...extra};query.vnp_SecureHash=crypto.createHmac('sha512','test-vnpay-hash').update(vnpay.canonicalQuery(query)).digest('hex');if(tamper)query.vnp_Amount='1';return{path:'/api/vnpay-ipn',method:'GET',query};}
const cases=[
 {name:'create and status',steps:[create,status(first)]},
 {name:'reuse pending link',steps:[create,create]},
 {name:'retry pending reuses transaction',steps:[create,{body:{action:'retry',txnRef:first}}]},
 {name:'invalid retry reference',steps:[{body:{action:'retry',txnRef:'bad'}}]},
 {name:'wrong payment capability',steps:[{body:{action:'create',orderId:'order-a',paymentReference:'WRONG'}}]},
 {name:'missing order',steps:[{body:{action:'create',orderId:'missing',paymentReference:'FFREFERENCE'}}]},
 {name:'bad order ID',steps:[{body:{action:'create',orderId:'a/b',paymentReference:'FFREFERENCE'}}]},
 {name:'wrong payment method',steps:[create],overrides:{'orders/order-a':{...seed['orders/order-a'],paymentMethod:'qr'}}},
 {name:'already paid',steps:[create],overrides:{'orders/order-a':{...seed['orders/order-a'],paymentVerified:true}}},
 {name:'successful IPN replay',steps:[create,ipn(),ipn(),status(first)]},
 {name:'signature rejects tamper',steps:[create,ipn(first,{},true),status(first)]},
 {name:'signed wrong amount',steps:[create,ipn(first,{vnp_Amount:'1'})]},
 {name:'signed wrong currency',steps:[create,ipn(first,{vnp_CurrCode:'USD'})]},
 {name:'signed repeated amount',steps:[create,ipn(first,{vnp_Amount:['11000000','11000000']})]},
 {name:'unknown signed transaction',steps:[ipn('aa'.repeat(16))]},
 {name:'failure then success',steps:[create,ipn(first,{vnp_ResponseCode:'24',vnp_TransactionStatus:'02'}),status(first),ipn(),status(first)]},
 {name:'late payment after expiry does not restore stock',steps:[create,{advance:900001,...ipn()},status(first)]},
 {name:'retry after expiry reserves again',steps:[create,{advance:900001,body:{action:'retry',txnRef:first}},status(second)]},
 {name:'old IPN failure cannot overwrite paid order',steps:[create,{advance:900001,body:{action:'retry',txnRef:first}},ipn(second),ipn(first,{vnp_ResponseCode:'24',vnp_TransactionStatus:'02'})]},
 {name:'extra successful payment attention',steps:[create,{advance:900001,body:{action:'retry',txnRef:first}},ipn(second),ipn(first)]},
 {name:'status invalid reference',steps:[status('bad')]},{name:'status missing payment',steps:[status(first)]},
 {name:'IPN method restricted',steps:[{path:'/api/vnpay-ipn',method:'POST',body:{}}]},
 {name:'payment action required',steps:[{method:'GET'}]},{name:'payment unknown action',steps:[{body:{action:'other'}}]}
];
(async()=>{
 const output=[];
 for(const input of cases){const initial={...seed,...input.overrides};const context=fixture(initial);const steps=[];
  for(const operation of input.steps){if(operation.advance)context.advance(operation.advance);const relative=operation.path==='/api/vnpay-ipn'?'payments/vnpay-ipn.js':'payments/payments.js';
   const result=await context.call(relative,{method:operation.method||'POST',body:operation.body||{},query:operation.query||{}});steps.push({operation,expected:result,states:Object.fromEntries(context.records)});}
  output.push({name:input.name,seed:initial,steps});
 }
 process.stdout.write(JSON.stringify(output));
})().catch(error=>{console.error(error);process.exitCode=1;});
