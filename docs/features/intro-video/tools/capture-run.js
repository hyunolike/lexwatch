const {chromium}=require('playwright');
(async()=>{
 const b=await chromium.launch({executablePath:process.env.CHROME,args:['--host-resolver-rules=MAP localhost 127.0.0.1']});
 const c=await b.newContext({viewport:{width:1600,height:900},deviceScaleFactor:1.2});
 await c.addCookies([{name:'lw_role',value:'admin',url:'http://localhost:3000'}]);
 const p=await c.newPage();
 await p.goto('http://localhost:3000/runs');await p.waitForTimeout(2000);
 await p.locator('a[href^="/runs/"]',{hasText:'근로기준법'}).first().click();await p.waitForTimeout(3000);
 const max=await p.evaluate(()=>document.documentElement.scrollHeight-innerHeight);
 const end=Math.min(520,max);let i=0;
 for(let y=0;y<=end;y+=2){await p.evaluate(y=>window.scrollTo(0,y),y);await p.waitForTimeout(30);await p.screenshot({path:`shots/run-${String(i++).padStart(3,'0')}.png`});}
 console.log('frames',i,'end',end);await b.close();})();
