import numpy as np
from rosette import *
from stab import *
print("== soft core at game numbers: hexagon (n=3 twisted), c_heavy=304.9, R_heavy=79.8 (median sun); mu=c_l/c_h, R_l=R_h*mu^1.25")
ch,Rh=304.9,79.8
for mu in (1.0,0.7,0.555):
    Rl=Rh*mu**1.25
    rlog,_=closed_log_twisted(3,mu)
    for ra in (30,60,100,200,1000,3000):
        rs=roots('soft',3,ch,ch*mu,np.pi/3,lo=0.2,hi=5,N=3000,ra=ra,Ra=Rh,Rb=Rl)
        out=[]
        for r in rs:
            d,wa,wb,t=resid('soft',3,ch,ch*mu,r,np.pi/3,ra=ra,Ra=Rh,Rb=Rl)
            rb=r*ra; sep=np.sqrt(ra**2+rb**2-ra*rb)
            out.append(f"rho={r:.4f} T={2*np.pi/np.sqrt(wa):.1f}s v_h={np.sqrt(wa)*ra:.2f} minsep={min(sep,ra*np.sqrt(3)):.0f}")
        print(f"mu={mu} ra={ra}: log rho={rlog:.4f} | soft: "+" ; ".join(out))
print("== same-radius alternating (generator today): omega^2 r^2 per species, log: heavy feels (n-1)mu... ")
for n in (2,3):
    for mu in (1,0.7,0.555):
        wh=((n-1)*1+n*mu)/2; wl=((n-1)*mu+n*1)/2
        print(f"2n={2*n} mu={mu}: w2r2 heavy={wh:.3f} light={wl:.3f} period ratio T_h/T_l={np.sqrt(wl/wh):.4f}")
