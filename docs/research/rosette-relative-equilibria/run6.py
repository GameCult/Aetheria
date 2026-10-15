import numpy as np
def f(n,mu,r): return r*r*((n-1)/2+n*mu/(1+r**n))-((n-1)*mu/2+n*r**n/(1+r**n))
def nroots(n,mu):
    g=np.geomspace(1e-3,1e3,20001); v=f(n,mu,g); return g[:-1][np.sign(v[:-1])!=np.sign(v[1:])]
for n in (2,3,4,5,6):
    prev=None
    for mu in np.geomspace(1,1e-4,400):
        k=len(nroots(n,mu))
        if k!=prev: print(n, f"mu={mu:.5f} roots={k}", np.round(nroots(n,mu),4)); prev=k
