const {chromium}=require('playwright');const {spawn}=require('child_process');const path=require('path');
const mode=process.argv[2];const times=(process.argv[3]||'').split(',').filter(Boolean).map(Number);
(async()=>{
 const b=await chromium.launch({executablePath:process.env.CHROME,args:['--allow-file-access-from-files']});
 const p=await b.newPage({viewport:{width:1920,height:1080}});
 await p.goto('file://'+path.resolve('comp.html'));await p.evaluate(()=>window.ready);
 const frame=async t=>{await p.evaluate(async t=>{render(t);for(const id of['A','B']){const im=document.getElementById(id);if(im.src)try{await im.decode()}catch{}}},t);};
 if(mode==='stills'){for(const t of times){await frame(t);await p.screenshot({path:`still-${t}.png`});}}
 else{const fps=30,dur=await p.evaluate(()=>DUR),n=Math.round(dur*fps);
  const ff=spawn('ffmpeg',['-y','-loglevel','error','-f','image2pipe','-framerate',String(fps),'-i','-','-c:v','libx264','-pix_fmt','yuv420p','-crf','18','-preset','slow','video-noaudio.mp4'],{stdio:['pipe','inherit','inherit']});
  for(let i=0;i<n;i++){await frame(i/fps);const buf=await p.screenshot({type:'png'});if(!ff.stdin.write(buf))await new Promise(r=>ff.stdin.once('drain',r));}
  ff.stdin.end();await new Promise(r=>ff.on('close',r));console.log('frames',n);}
 await b.close();})();
