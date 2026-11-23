const {chromium}=require('playwright');const path=require('path');
(async()=>{const b=await chromium.launch({executablePath:process.env.CHROME,args:['--allow-file-access-from-files']});
const p=await b.newPage({viewport:{width:1920,height:1080}});await p.goto('file://'+path.resolve('thumb.html'));await p.evaluate(()=>window.ready);await p.waitForTimeout(300);
await p.screenshot({path:'thumb.png'});await b.close();})();
