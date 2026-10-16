import sys; sys.argv=['x','64']
import trophic_probe as T
cands=T.cand_list(16.0)
for win,boost in ((2,1.6),(3,1.6),(2,0.6)):
    T.WIN, T.BOOST = win, boost
    for name in ("lattice","skein-web"):
        w,b,n=T.pop_probe(T.CANDS[name],cands,100.0,1500)
        print(f"WIN {win} BOOST {boost} {name}: presence step max {w:.4f}, {b} steps > .1 of {n} candidates", flush=True)
