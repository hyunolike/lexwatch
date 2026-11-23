import numpy as np, wave
SR=48000; DUR=36.5; t=np.arange(int(SR*DUR))/SR
def hz(n): return 440*2**((n-69)/12)
# C장조 안의 느린 코드 진행: Fmaj9, Am7, Fmaj9, Cmaj9 (각 9초 남짓)
chords=[(0,[53,57,60,64,67]),(9.2,[45,52,55,60,64]),(18.4,[53,57,60,64,67]),(27.6,[48,55,59,64,67])]
pad=np.zeros_like(t)
for i,(st,notes) in enumerate(chords):
    en=chords[i+1][0] if i+1<len(chords) else DUR
    env=np.clip((t-st)/1.6,0,1)*np.clip((en+1.6-t)/1.6,0,1)
    env*= (t>=st-0.01)
    for k,n in enumerate(notes):
        f=hz(n); w=np.sin(2*np.pi*f*t)+0.5*np.sin(2*np.pi*f*1.003*t+1.3)+0.12*np.sin(2*np.pi*2*f*t)
        pad+=env*w*(0.9 if k==0 else 0.55)
# 천천히 숨쉬는 음량
pad*=0.75+0.25*np.sin(2*np.pi*t/7.0)
# 낮은 베이스 펄스(근음), 한 박 = 0.8초
roots=[41,45,41,36]; bass=np.zeros_like(t)
for i,(st,_) in enumerate(chords):
    en=chords[i+1][0] if i+1<len(chords) else DUR-3
    for b in np.arange(st,en,1.6):
        if b>DUR-3.5: break
        m=(t>=b)&(t<b+1.5); tt=t[m]-b
        bass[m]+=np.sin(2*np.pi*hz(roots[i])*tt)*np.exp(-tt*2.2)*np.clip(tt/0.02,0,1)
mix=pad*0.05+bass*0.11
# 클릭: C6, E6 짧은 감쇠음(곡과 같은 조)
def ping(at,f,amp,dec=18):
    m=(t>=at)&(t<at+0.6); tt=t[m]-at
    mix[m]+=amp*np.sin(2*np.pi*f*tt)*np.exp(-tt*dec)*np.clip(tt/0.004,0,1)
for c in [7.3,15.05,21.95,23.05]: ping(c,hz(84),0.05); ping(c,hz(72),0.03,12)
# 타이핑 두 번
for c in [19.7,20.0]: ping(c,hz(88),0.025,30)
# 마무리 종소리 C5 E5 G5
for k,n in enumerate([72,76,79,84]): ping(32.95+k*0.09,hz(n),0.06,2.2)
# 전체 페이드
mix*=np.clip(t/1.0,0,1)*np.clip((DUR-t)/2.5,0,1)
# 단순 저역 통과(1차)로 날카로움 줄이기
a=np.exp(-2*np.pi*5000/SR); y=np.empty_like(mix); acc=0.0
for i in range(0,len(mix)):
    acc=(1-a)*mix[i]+a*acc; y[i]=acc
y=y/np.max(np.abs(y))*0.5
pcm=(y*32767).astype(np.int16)
st=np.stack([pcm,pcm],1)
with wave.open('music.wav','wb') as w: w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR);w.writeframes(st.tobytes())
print('ok')
