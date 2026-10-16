import numpy as np
from rosette import *
def grad_hess(X,C,w2,x):
    g=np.zeros(2); H=np.zeros((2,2))
    for j in range(len(X)):
        d=x-X[j]; s2=d@d
        g+=C[j]*d/s2; H+=C[j]*(np.eye(2)/s2-2*np.outer(d,d)/s2**2)
    g-=w2*x; H-=w2*np.eye(2)
    return g,H
def stationary(X,C,w2,box):
    pts=[]
    for x0 in np.linspace(-box,box,41):
        for y0 in np.linspace(-box,box,41):
            x=np.array([x0,y0])
            for _ in range(100):
                g,H=grad_hess(X,C,w2,x)
                try: dx=np.linalg.solve(H,-g)
                except: break
                st=np.linalg.norm(dx)
                if st>0.1: dx*=0.1/st
                x=x+dx
                if np.linalg.norm(dx)<1e-13: break
            g,H=grad_hess(X,C,w2,x)
            if np.hypot(*x)<0.02: continue
            if np.linalg.norm(g)<1e-9 and min(np.hypot(*(x-X[j])) for j in range(len(X)))>1e-3 and np.hypot(*x)<box*1.5:
                if all(np.hypot(*(x-p[0]))>1e-4 for p in pts):
                    ev=np.linalg.eigvalsh(H)
                    pts.append((x,'min' if ev.min()>0 else ('max' if ev.max()<0 else 'saddle')))
    return pts
np.seterr(all='ignore')
for mu in (1.0,0.7):
    r,_=closed_log_twisted(3,mu)
    X,C,Rc=config(3,1,mu,r,np.pi/3); w2,_=omega2('log',X,C,Rc,0)
    pts=stationary(X,C,0.0,2.5)
    print(f"inertial mu={mu}: {len(pts)} off-centre stationary points:",[(round(np.hypot(*x),4),round(np.degrees(np.arctan2(x[1],x[0]))%360,1),k) for x,k in pts])
    for i in (0,3):
        others=[j for j in range(len(X)) if j!=i]
        g,H=grad_hess(X[others],C[others],w2,X[i]); ev=np.linalg.eigvalsh(H)
        gi,Hi=grad_hess(X[others],C[others],0.0,X[i])
        print(f"  body {i} (c={C[i]}): rotating-frame field of the others: |grad|={np.linalg.norm(g):.1e}, Hessian eig={ev.round(4)} -> {'min' if ev.min()>0 else ('max' if ev.max()<0 else 'saddle')}; inertial |grad|={np.linalg.norm(gi):.4f} (= w2*r={w2*np.hypot(*X[i]):.4f})")
