import numpy as np
from rosette import *
from stab import *
print("== equal ring, no centre, analytic Hessian: maxRe log / newton")
for n in range(2,10):
    X,C,Rc=config(n,1,1,1,0); X=X[:n];C=C[:n]
    print(n, "%.1e %.1e"%(maxre('log',X,C,omega2('log',X,C,Rc[:n],0)[0]), maxre('newton',X,C,omega2('newton',X,C,Rc[:n],0)[0])))
print("== Muscas check: log ring n with central mass M=1, ring masses mu")
for n,mus in ((7,[0.05,4/36*0.99,4/36*1.01,0.5,0.99]),(12,[4/121*0.99,4/121*1.01,16/(144-96+8)*0.99,16/(144-96+8)*1.01])):
    for mu in mus:
        X,C,Rc=config(n,mu,mu,1,0,c0=1.0); X=np.vstack([X[:n],X[-1:]]); C=np.concatenate([C[:n],C[-1:]]); Rc=np.zeros(n+1)
        w2,_=omega2('log',X,C,Rc,0); print(n, round(mu,5), "maxRe %.1e"%maxre('log',X,C,w2))
print("== twisted alternating 2n-gon, all roots, analytic stability; rho=r_light/r_heavy, mu=c_light/c_heavy")
for law in ('log','newton'):
  for n in (2,3,4,5,6,7):
    for mu in (1,0.9,0.7,0.5,0.3,0.1):
        rs=roots(law,n,1,mu,np.pi/n,lo=0.02,hi=50,N=6000)
        res=[]
        for r in rs:
            X,C,Rc=config(n,1,mu,r,np.pi/n); d,wa,wb,t=resid(law,n,1,mu,r,np.pi/n)
            m=maxre(law,X,C,wa); res.append(f"rho={r:.4f} w2ra2={wa:.4f} {'STABLE' if m<1e-6 else 'unst %.2f'%m}")
        print(law,f"2n={2*n}",f"mu={mu}",f"(mass ratio m^0.2 -> {mu**5:.3g})"," | ".join(res))
