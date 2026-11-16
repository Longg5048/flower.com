const {fixture}=require('./commerce-fixture.cjs'); const crypto=require('node:crypto');
const token=Buffer.alloc(32,1).toString('base64url');
const saved={customerName:'Khách',phone:'0901234567',status:'Chờ khách xác nhận',quotedTotal:150000,history:[],accessTokenHash:crypto.createHash('sha256').update(token).digest('hex'),createdAt:{$timestamp:'2026-11-16T03:00:00.123Z'},updatedAt:{$timestamp:'2026-11-16T03:00:00.123Z'}};
const seed={'products/rose':{title:'Hồng',saleType:'flower',price:12000,quantity:30,colors:['red']},'metadata/deliveryZones':{zones:[{id:'zone-a',province:'Hà Nội',district:'Ba Đình',fee:10000,enabled:true}]},'metadata/bouquetWrapping':{tiers:[{maxStems:50,fee:15000}]},'adminRoles/admin':{role:'admin'},'designRequests/existing':saved};
const base={action:'create',customerName:'Khách',phone:'0901234567',address:'1 đường Hoa',deliveryDate:'2027-01-01',deliveryTime:'08:00–11:00',deliveryZoneId:'zone-a',design:{wrapStyle:'Bó tròn'},stems:[{productId:'rose',quantity:5,color:'đỏ',role:'main'}]};
const update={action:'admin-update',id:'existing',status:'Chờ khách xác nhận',quotedTotal:170000,quoteNote:'Báo giá mới'};
const cases=[
 {name:'create guest then lookup',steps:[{body:base},{method:'GET',query:{id:'auto-1',token}}]},
 {name:'create signed customer',steps:[{body:base,authorization:'Bearer customer'}]},
 {name:'revoked identity',steps:[{body:base,authorization:'Bearer revoked'}]},
 {name:'invalid design',steps:[{body:{...base,design:{size:'bogus'}}}]},
 {name:'invalid phone',steps:[{body:{...base,phone:'123'}}]},
 {name:'surprise budget without stems',steps:[{body:{...base,design:{surprise:true,budget:200000},stems:[]}}]},
 {name:'wrong color',steps:[{body:{...base,stems:[{productId:'rose',quantity:1,color:'trắng'}]}}]},
 {name:'stock conflict',steps:[{body:{...base,stems:[{productId:'rose',quantity:31,color:'đỏ'}]}}]},
 {name:'duplicate stems rejected',steps:[{body:{...base,stems:[...base.stems,...base.stems]}}]},
 {name:'invalid image',steps:[{body:{...base,referenceImage:'https://wrong.example'}}]},
 {name:'lookup wrong token',steps:[{method:'GET',query:{id:'existing',token:'wrong'}}]},
 {name:'lookup valid',steps:[{method:'GET',query:{id:'existing',token}}]},
 {name:'list admin',steps:[{method:'GET',query:{manage:'1'},authorization:'Bearer admin'}]},
 {name:'list anonymous',steps:[{method:'GET',query:{manage:'1'}}]},
 {name:'list customer',steps:[{method:'GET',query:{manage:'1'},authorization:'Bearer customer'}]},
 {name:'accept then replay',steps:[{body:{action:'respond',id:'existing',accessToken:token,response:'accept'}},{body:{action:'respond',id:'existing',accessToken:token,response:'accept'}}]},
 {name:'reject',steps:[{body:{action:'respond',id:'existing',accessToken:token,response:'reject'}}]},
 {name:'response bad token',steps:[{body:{action:'respond',id:'existing',accessToken:'wrong',response:'accept'}}]},
 {name:'admin quote then accept',steps:[{body:update,authorization:'Bearer admin'},{body:{action:'respond',id:'existing',accessToken:token,response:'accept'}}]},
 {name:'admin status without quote',steps:[{body:{action:'admin-update',id:'existing',status:'Hoàn thành'},authorization:'Bearer admin'}]},
 {name:'admin invalid quote',steps:[{body:{...update,quotedTotal:-1},authorization:'Bearer admin'}]},
 {name:'admin missing request',steps:[{body:{...update,id:'missing'},authorization:'Bearer admin'}]},
 {name:'unknown action',steps:[{body:{action:'wrong'}}]}, {name:'method restricted',steps:[{method:'DELETE'}]}
];
(async()=>{const output=[];for(const input of cases){const context=fixture(seed),steps=[];for(const operation of input.steps){const expected=await context.call('design-requests/controller.js',{method:operation.method||'POST',body:operation.body||{},query:operation.query||{},headers:operation.authorization?{authorization:operation.authorization}:{}});steps.push({operation,expected,states:Object.fromEntries(context.records)});}output.push({name:input.name,seed,steps});}process.stdout.write(JSON.stringify(output));})().catch(e=>{console.error(e);process.exitCode=1;});
