const {chromium}=require('playwright');
const S=1.2;
(async()=>{
 const b=await chromium.launch({executablePath:process.env.CHROME,args:['--host-resolver-rules=MAP localhost 127.0.0.1']});
 const c=await b.newContext({viewport:{width:1600,height:900},deviceScaleFactor:S});
 await c.addCookies([{name:'lw_role',value:'lawyer',url:'http://localhost:3000'}]);
 const p=await c.newPage();
 await p.goto('http://localhost:3000/');
 await p.locator('a[href^="/reviews/"]',{hasText:'확인 필요 1'}).first().click();await p.waitForSelector('text=영향 검토 초안');
 await p.evaluate(()=>window.scrollTo(0,300));await p.waitForTimeout(500);
 const r=await p.evaluate(()=>{
  const out={};const walk=(needle,key,root)=>{const tw=document.createTreeWalker(root||document.body,NodeFilter.SHOW_TEXT);let n;
   // gather concatenated text to handle split spans
   const nodes=[];let txt='';while(n=tw.nextNode()){nodes.push([n,txt.length]);txt+=n.data;}
   const res=[];let i=-1;while((i=txt.indexOf(needle,i+1))>=0){const rg=document.createRange();
    const loc=(pos)=>{for(let k=nodes.length-1;k>=0;k--)if(nodes[k][1]<=pos)return [nodes[k][0],pos-nodes[k][1]];};
    const [a,ao]=loc(i),[z,zo]=loc(i+needle.length-1);rg.setStart(a,ao);rg.setEnd(z,zo+1);const bb=rg.getBoundingClientRect();res.push([bb.x,bb.y,bb.width,bb.height]);}
   out[key]=res;};
  walk('100분의 70','r70');walk('100분의 50','r50');
  const f=[...document.querySelectorAll('*')].find(e=>e.textContent.startsWith('인용한 근거 본문에 없는 표기')&&e.children.length<3);
  let box=f;for(let k=0;k<4&&box;k++){if(getComputedStyle(box).borderTopWidth!=='0px')break;box=box.parentElement;}
  const bb=box.getBoundingClientRect();out.findingBox=[bb.x,bb.y,bb.width,bb.height];return out;});
 const sc=o=>Object.fromEntries(Object.entries(o).map(([k,v])=>[k,Array.isArray(v[0])?v.map(a=>a.map(x=>Math.round(x*S))):v.map(x=>Math.round(x*S))]));
 console.log(JSON.stringify(sc(r)));
 await b.close();})();
