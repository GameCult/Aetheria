import numpy as np
from rosette import *
np.set_printoptions(precision=4)
print("== equal ring, no centre: max Re(eig) (log vs newton)")
for n in range(2,10):
    out=[]
    for law in ('log','newton'):
        X,C,Rc=config(n,1,1,1,0)  # A ring only: build n bodies
        X=X[:n];C=C[:n];Rc=Rc[:n]
        w2,_=omega2(law,X,C,Rc,0)
        m,_=stability(law,X,C,Rc,w2); out.append(m)
    print(n, "log w2r2=%.4f"%omega2('log',X,C,Rc,0)[0], "maxRe log %.2e newton %.2e"%tuple(out))
print("== log twisted alternating 2n-gon: closed form vs direct sum, stability")
for n in (2,3,4,5,6):
  for mu in (1,0.9,0.7,0.5,0.3,0.1,0.01):
    r,w2=closed_log_twisted(n,mu)
    rs=roots('log',n,1,mu,np.pi/n)
    X,C,Rc=config(n,1,mu,r,np.pi/n)
    d,wa,wb,t=resid('log',n,1,mu,r,np.pi/n)
    m,_=stability('log',X,C,Rc,wa)
    print(f"n={n} mu={mu:<5} rho={r:.5f} w2*ra^2={w2:.5f} direct_roots={[round(x,5) for x in rs]} resid={d:.1e} tang={t:.1e} maxRe={m:.2e} massratio(m^0.2)={mu**5:.3g}")
print("== newton twisted alternating")
for n in (3,4,6):
  for mu in (1,0.7,0.5,0.3,0.1,0.01):
    rs=roots('newton',n,1,mu,np.pi/n)
    res=[]
    for r in rs:
        X,C,Rc=config(n,1,mu,r,np.pi/n); d,wa,wb,t=resid('newton',n,1,mu,r,np.pi/n)
        m,_=stability('newton',X,C,Rc,wa); res.append((round(r,5),round(wa,4),f"{m:.1e}"))
    print(f"n={n} mu={mu} roots(rho,w2,maxRe)={res}")
print("== aligned (twist 0) nested")
for law in ('log','newton'):
  for n in (3,6):
    for mu in (1,0.5,0.1):
        rs=roots(law,n,1,mu,0.0)
        res=[]
        for r in rs:
            X,C,Rc=config(n,1,mu,r,0.0); d,wa,wb,t=resid(law,n,1,mu,r,0.0)
            m,_=stability(law,X,C,Rc,wa); res.append((round(r,5),f"{m:.1e}"))
        print(law,n,mu,res)
