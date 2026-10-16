import sys; sys.argv=['x','64']
import trophic_probe as T
cands=T.cand_list(16.0); T.WEB_AT_CYCLE=True
for name in ("lattice","skein-web"):
    w,b,n=T.pop_probe(T.CANDS[name],cands,100.0,1500)
    a=T.members(T.CANDS[name],cands,0.0); b1=T.members(T.CANDS[name],cands,1.0)
    print(f"web at cycle start, {name}: presence step max {w:.4f}, {b} steps > .1 of {n}; members {len(a)}, changed in 1 s {len(a^b1)/max(1,len(a)):.3f}", flush=True)
