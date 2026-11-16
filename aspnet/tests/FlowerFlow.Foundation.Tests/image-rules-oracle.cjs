const path=require('node:path'),fs=require('node:fs'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../../..');
const rules=require(path.join(root,'flower.com/backend/catalog/image-rules'));
const jpeg=(width=100,height=100,marker=0xc0)=>Buffer.from([0xff,0xd8,0xff,marker,0,8,8,height>>8,height&255,width>>8,width&255,0,0xff,0xd9]);
const url=bytes=>'data:image/jpeg;base64,'+bytes.toString('base64');
const valid=jpeg();
const real=fs.readFileSync(path.join(root,'flower.com/images/start.jpeg'));
const sized=length=>Buffer.concat([valid.subarray(0,-2),Buffer.alloc(length-valid.length),valid.subarray(-2)]);
const dimensions=[
 ['baseline',valid],['progressive',jpeg(100,100,0xc2)],['extended',jpeg(100,100,0xc1)],
 ['real',real],['empty',Buffer.alloc(0)],['wrong SOI',Buffer.from('not-a-jpeg')],
 ['wrong EOI',Buffer.concat([valid.subarray(0,-2),Buffer.from([0,0])])],
 ['zero width',jpeg(0,100)],['1600',jpeg(1600,1600)],['1601',jpeg(1601,100)],
 ['unsupported SOF',jpeg(100,100,0xc3)],
 ['APP segment',Buffer.concat([valid.subarray(0,2),Buffer.from([0xff,0xe0,0,2]),valid.subarray(2)])],
 ['standalone marker',Buffer.concat([valid.subarray(0,2),Buffer.from([0xff,0x01]),valid.subarray(2)])],
 ['repeated FF',Buffer.concat([valid.subarray(0,2),Buffer.from([0xff,0xff]),valid.subarray(2)])],
 ['invalid segment length',Buffer.from([0xff,0xd8,0xff,0xc0,0,1,8,0,1,0,1,0,0xff,0xd9])],
 ['out of bounds length',Buffer.from([0xff,0xd8,0xff,0xc0,1,0,8,0,1,0,1,0,0xff,0xd9])],
 ['scan before frame',jpeg(100,100,0xda)]
];
const validations=[
 {name:'omitted'}, {name:'empty',photos:[]},{name:'null',photos:null},{name:'object',photos:{}},
 {name:'one real',photos:[url(real)]},{name:'three',photos:[url(valid),url(valid),url(valid)]},
 {name:'four',photos:[url(valid),url(valid),url(valid),url(valid)]},
 {name:'url',photos:['https://example.test/a.jpg']},{name:'PNG',photos:['data:image/png;base64,'+valid.toString('base64')]},
 {name:'SVG',photos:['data:image/svg+xml;base64,PHN2Zz4=']},{name:'text as JPEG',photos:[url(Buffer.from('not a JPEG'))]},
 {name:'max bytes',photos:[url(sized(rules.MAX_PHOTO_BYTES))]},
 {name:'max bytes plus one',photos:[url(sized(rules.MAX_PHOTO_BYTES+1))]},
 {name:'1600 dimensions',photos:[url(jpeg(1600,1600))]},{name:'1601 width',photos:[url(jpeg(1601,100))]},
 {name:'1601 height',photos:[url(jpeg(100,1601))]},{name:'zero dimension',photos:[url(jpeg(0,100))]},
 {name:'uppercase prefix',photos:[url(valid).replace('jpeg','JPEG')]},
 {name:'base64 missing padding',photos:[url(valid).replace(/=+$/,'')]},
 {name:'extra padding',photos:[url(valid)+'=']},{name:'trailing newline',photos:[url(valid)+'\n']},
 {name:'non string',photos:[123]},{name:'null photo',photos:[null]}
];
const output={maxPhotos:rules.MAX_REVIEW_PHOTOS,maxBytes:rules.MAX_PHOTO_BYTES,
 dimensions:dimensions.map(([name,bytes])=>{try{return{name,bytes:bytes.toString('base64'),expected:rules.jpegDimensions(bytes),throws:false};}catch{return{name,bytes:bytes.toString('base64'),throws:true};}}),
 validations:validations.map(input=>{try{const result=Object.hasOwn(input,'photos')?rules.validateReviewPhotos(input.photos):rules.validateReviewPhotos();return{...input,throws:false,expected:result===null?null:result.map(bytes=>({size:bytes.length,sha256:crypto.createHash('sha256').update(bytes).digest('hex')}))};}catch{return{...input,throws:true};}})};
process.stdout.write(JSON.stringify(output));
