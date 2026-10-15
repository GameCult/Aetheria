import numpy as np
from rosette import *
def hess(law,X,C):
    N=len(X); D=np.zeros((2*N,2*N))
    for i in range(N):
        for j in range(N):
            if i==j: continue
            d=X[j]-X[i]; s=np.hypot(*d); I=np.eye(2); dd=np.outer(d,d)
            B = C[j]*(I/s**2-2*dd/s**4) if law=='log' else C[j]*(I/s**3-3*dd/s**5)
            D[2*i:2*i+2,2*j:2*j+2]+=B; D[2*i:2*i+2,2*i:2*i+2]-=B
    return D
def maxre(law,X,C,w2):
    N=len(X); w=np.sqrt(w2); D=hess(law,X,C)+w2*np.eye(2*N)
    J=np.zeros((2*N,2*N))
    for i in range(N): J[2*i,2*i+1]=1; J[2*i+1,2*i]=-1
    A=np.block([[np.zeros((2*N,2*N)),np.eye(2*N)],[D,2*w*J]])
    return np.max(np.linalg.eigvals(A).real)
