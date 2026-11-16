const {fixture}=require('./commerce-fixture.cjs');
const seed={
 'products/ready':{title:'Bó hoa',image:'ready.jpg',price:100000,quantity:10,saleType:'ready_made',status:'active'},
 'products/rose':{title:'Hoa hồng',image:'rose.jpg',price:12000,priceUpdatedAt:'2026-11-16',quantity:30,saleType:'flower',status:'active'},
 'metadata/deliveryZones':{zones:[{id:'zone-a',province:'Hà Nội',district:'Ba Đình',fee:10000,enabled:true}]},
 'metadata/activePromotions':{items:[{id:'promo',active:true,discountPercent:10,allProducts:true}]},
 'metadata/bouquetWrapping':{tiers:[{maxStems:10,fee:5000},{maxStems:50,fee:15000}]},
 'adminRoles/admin':{role:'admin'},'adminRoles/owner':{role:'owner'}
};
const base={customerName:'Khách hàng',phone:'0901234567',address:'1 đường Hoa',deliveryDate:'2027-01-01',deliveryTime:'08:00–11:00',deliveryZoneId:'zone-a',paymentMethod:'qr',
 idempotencyKey:'12345678-1234-4123-8123-123456789abc',expectedTotals:{subtotal:100000,savings:0,tax:0,shippingFee:10000,total:110000},items:[{id:'ready',quantity:1}]};
const quote={action:'quote',items:base.items,deliveryZoneId:'zone-a'};
const cases=[
 {name:'quote',body:quote},{name:'quote missing delivery',body:{...quote,deliveryZoneId:''}},
 {name:'quote promotion',body:{...quote,promotionId:'promo'}},{name:'quote invalid promotion',body:{...quote,promotionId:'missing'}},
 {name:'quote short stock',body:{...quote,items:[{id:'ready',quantity:20}]}},
 {name:'quote hidden',body:quote,overrides:{'products/ready':{...seed['products/ready'],status:'hidden'}}},
 {name:'quote missing product',body:{...quote,items:[{id:'missing',quantity:1}]}},
 {name:'quote custom bouquet',body:{...quote,items:[{id:'rose',quantity:5,customBouquetId:'bouquet-a'}]}},
 {name:'quote flower must have bouquet',body:{...quote,items:[{id:'rose',quantity:1}]}},
 {name:'quantity limit',body:{...quote,items:[{id:'ready',quantity:51}]}},
 {name:'create guest',body:base},{name:'create signed customer',body:base,authorization:'Bearer customer'},
 {name:'create invalid token',body:base,authorization:'Bearer revoked'},
 {name:'create price changed',body:{...base,expectedTotals:{...base.expectedTotals,total:1}}},
 {name:'create past delivery',body:{...base,deliveryDate:'2026-11-01'}},
 {name:'create guest quantity cap',body:{...base,items:[{id:'ready',quantity:6}],expectedTotals:{subtotal:600000,savings:0,tax:0,shippingFee:10000,total:610000}}},
 {name:'idempotent replay',steps:[{body:base},{body:base}]},
 {name:'idempotent different payload',steps:[{body:base},{body:{...base,orderNote:'different'}}]},
 {name:'idempotent different owner',steps:[{body:base},{body:base,authorization:'Bearer customer'}]},
 {name:'lookup guest private data excluded',steps:[{body:base},{body:{action:'lookup',id:'auto-1',phone:base.phone}}]},
 {name:'lookup wrong phone',steps:[{body:base},{body:{action:'lookup',id:'auto-1',phone:'0999999999'}}]},
 {name:'lookup owner',steps:[{body:base,authorization:'Bearer customer'},{body:{action:'lookup',id:'auto-1'},authorization:'Bearer customer'}]},
 {name:'mine',steps:[{body:base,authorization:'Bearer customer'},{method:'GET',query:{action:'mine'},authorization:'Bearer customer'}]},
 {name:'expired reservation releases once',steps:[{body:base},{advance:1800001,method:'GET',query:{action:'expire'},authorization:'Bearer test-cron'},{method:'GET',query:{action:'expire'},authorization:'Bearer test-cron'}]},
 {name:'QR reconciliation',steps:[{body:base},{body:{action:'verify-qr-payment',orderId:'auto-1',reconciliationReference:'BANK-1',amount:110000},authorization:'Bearer admin'},{body:{action:'verify-qr-payment',orderId:'auto-1',reconciliationReference:'BANK-1',amount:110000},authorization:'Bearer admin'}]},
 {name:'QR mismatch no changes',steps:[{body:base},{body:{action:'verify-qr-payment',orderId:'auto-1',reconciliationReference:'BANK-1',amount:1},authorization:'Bearer admin'}]},
 {name:'refund cannot repeat',steps:[{body:base},{body:{action:'verify-qr-payment',orderId:'auto-1',reconciliationReference:'BANK-1',amount:110000},authorization:'Bearer admin'},
  {body:{action:'record-payment-refund',orderId:'auto-1',refundReference:'REFUND-1'},authorization:'Bearer admin'},{body:{action:'record-payment-refund',orderId:'auto-1',refundReference:'REFUND-1'},authorization:'Bearer admin'}]},
 {name:'unsupported method',method:'DELETE'},{name:'GET requires action',method:'GET'},{name:'mine needs login',method:'GET',query:{action:'mine'}},
 {name:'expire needs auth',method:'GET',query:{action:'expire'}},
 {name:'review must belong to delivered order',body:{action:'review',orderId:'delivered',rating:5,comment:'Hoa đẹp'},authorization:'Bearer customer',overrides:{'orders/delivered':{uid:'customer',status:'Đã giao',customerName:'Admin',items:[{id:'ready',title:'Bó hoa',quantity:1}]}}},
 {name:'review repeat rejected',steps:[{body:{action:'review',orderId:'delivered',rating:5,comment:'Hoa đẹp'},authorization:'Bearer customer'},{body:{action:'review',orderId:'delivered',rating:4,comment:'Khác'},authorization:'Bearer customer'}],overrides:{'orders/delivered':{uid:'customer',status:'Đã giao',customerName:'Khách',items:[{id:'ready',title:'Bó hoa',quantity:1}]}}},
 {name:'review nondelivered',body:{action:'review',orderId:'waiting',rating:5,comment:'Hoa đẹp'},authorization:'Bearer customer',overrides:{'orders/waiting':{uid:'customer',status:'Chờ xác nhận',items:[{id:'ready',title:'Bó hoa',quantity:1}]}}}
];
(async()=>{
 const output=[];
 for(const input of cases){const initial={...seed,...input.overrides};const context=fixture(initial);const steps=[];
  for(const operation of input.steps||[input]){if(operation.advance)context.advance(operation.advance);const result=await context.call('orders/controller.js',{method:operation.method||'POST',body:operation.body||{},query:operation.query||{},headers:operation.authorization?{authorization:operation.authorization}:{}});steps.push({operation,expected:result,states:Object.fromEntries(context.records)});}
  output.push({name:input.name,seed:initial,steps});
 }
 process.stdout.write(JSON.stringify(output));
})().catch(error=>{console.error(error);process.exitCode=1;});
