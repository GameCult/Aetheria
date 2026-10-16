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
    for x0 in np.linspace(-box,box,61):
        for y0 in np.linspace(-box,box,61):
            x=np.array([x0,y0])
            for _ in range(60):
                g,H=grad_hess(X,C,w2,x)
                try: dx=np.linalg.solve(H,-g)
                except: break
                st=np.linalg.norm(dx)
                if st>0.2: dx*=0.2/st
                x=x+dx
                if np.linalg.norm(dx)<1e-13: break
            g,H=grad_hess(X,C,w2,x)
            if np.linalg.norm(g)<1e-9 and min(np.hypot(*(x-X[j])) for j in range(len(X)))>1e-3 and np.hypot(*x)<box*1.5:
                if all(np.hypot(*(x-p[0]))>1e-6 for p in pts):
                    ev=np.linalg.eigvalsh(H)
                    # potential phi = sum c ln s - w2 r^2/2 (a well is -inf at bodies: phi is the gravitational potential, force = -grad phi)
                    kind='min' if ev.min()>0 else ('max' if ev.max()<0 else 'saddle')
                    pts.append((x,kind))
    return pts
for mu in (1.0,0.7):
    r,_=closed_log_twisted(3,mu)
    X,C,Rc=config(3,1,mu,r,np.pi/3)
    w2,_=omega2('log',X,C,Rc,0)
    for label,ww in (('rotating effective',w2),('inertial (drawn)',0.0)):
        pts=stationary(X,C,ww,2.5)
        print(f"== hexagon mu={mu} rho={r:.4f} {label}: {len(pts)} stationary points")
        for x,k in sorted(pts,key=lambda p:(np.hypot(*p[0]),np.arctan2(p[0][1],p[0][0]))):
            print(f"   r={np.hypot(*x):.4f} theta={np.degrees(np.arctan2(x[1],x[0]))%360:7.2f} {k}")
