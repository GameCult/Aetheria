import numpy as np, sys
# laws: pull magnitude toward source j at distance s, strength c, core R
def pull(law, c, R, s):
    if law == 'log':  return c/s
    if law == 'soft': return c*s/(R*R) if s < R else c/s
    if law == 'newton': return c/(s*s)
def acc(law, X, C, Rc, x, skip=None):
    a = np.zeros(2)
    for j in range(len(X)):
        if j == skip: continue
        d = X[j]-x; s = np.hypot(*d)
        if s == 0: continue
        a += d/s*pull(law, C[j], Rc[j], s)
    return a
def config(n, ca, cb, rho, twist, ra=1.0, Ra=0.0, Rb=0.0, c0=0.0, R0=0.0):
    X=[];C=[];Rc=[]
    for k in range(n):
        t=2*np.pi*k/n; X.append([ra*np.cos(t), ra*np.sin(t)]); C.append(ca); Rc.append(Ra)
    for k in range(n):
        t=2*np.pi*k/n+twist; X.append([rho*ra*np.cos(t), rho*ra*np.sin(t)]); C.append(cb); Rc.append(Rb)
    if c0>0: X.append([0,0]); C.append(c0); Rc.append(R0)
    return np.array(X,float), np.array(C,float), np.array(Rc,float)
def omega2(law, X, C, Rc, i):
    a = acc(law,X,C,Rc,X[i],skip=i); r=np.hypot(*X[i])
    radial = -a.dot(X[i])/r; tang = (a[0]*-X[i][1]+a[1]*X[i][0])/r
    return radial/r, tang
def resid(law,n,ca,cb,rho,twist,**kw):
    X,C,Rc=config(n,ca,cb,rho,twist,**kw)
    wa,ta=omega2(law,X,C,Rc,0); wb,tb=omega2(law,X,C,Rc,n)
    return wa-wb, wa, wb, max(abs(ta),abs(tb))
def roots(law,n,ca,cb,twist,lo=0.05,hi=20,N=4000,**kw):
    g=np.geomspace(lo,hi,N); out=[]
    f=[resid(law,n,ca,cb,r,twist,**kw)[0] for r in g]
    for i in range(N-1):
        if np.isfinite(f[i]) and np.isfinite(f[i+1]) and f[i]*f[i+1]<0 and abs(f[i])<1e3 and abs(f[i+1])<1e3:
            a,b=g[i],g[i+1]
            for _ in range(80):
                m=(a+b)/2; fm=resid(law,n,ca,cb,m,twist,**kw)[0]
                if fm*resid(law,n,ca,cb,a,twist,**kw)[0]<=0: b=m
                else: a=m
            out.append((a+b)/2)
    return out
def closed_log_twisted(n,mu,kappa=0.0):
    # rho^2[(n-1)/2 + n mu/(1+rho^n) + kappa] - [(n-1)mu/2 + n rho^n/(1+rho^n) + kappa] = 0, rho=r_b/r_a, mu=c_b/c_a, kappa=c0/c_a
    f=lambda r: r*r*((n-1)/2+n*mu/(1+r**n)+kappa)-((n-1)*mu/2+n*r**n/(1+r**n)+kappa)
    a,b=1e-6,1e6
    for _ in range(200):
        m=np.sqrt(a*b)
        if f(a)*f(m)<=0: b=m
        else: a=m
    r=np.sqrt(a*b); w2=(n-1)/2+n*mu/(1+r**n)+kappa
    return r,w2
# linear stability in rotating frame; masses = strengths (valid for pure log / newton)
def stability(law,X,C,Rc,w2):
    N=len(X); w=np.sqrt(w2); q0=X.flatten()
    def F(q):
        Q=q.reshape(N,2); out=np.zeros_like(Q)
        for i in range(N): out[i]=acc(law,Q,C,Rc,Q[i],skip=i)+w2*Q[i]
        return out.flatten()
    h=1e-6; D=np.zeros((2*N,2*N))
    for k in range(2*N):
        e=np.zeros(2*N); e[k]=h; D[:,k]=(F(q0+e)-F(q0-e))/(2*h)
    J=np.zeros((2*N,2*N))
    for i in range(N): J[2*i,2*i+1]=1; J[2*i+1,2*i]=-1   # x' term: -2w J v gives (2w vy, -2w vx)
    A=np.block([[np.zeros((2*N,2*N)),np.eye(2*N)],[D,2*w*J]])
    ev=np.linalg.eigvals(A); return np.max(ev.real), ev
