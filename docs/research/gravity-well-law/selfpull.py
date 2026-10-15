# Self-pull check: a lone body (mass 100) at 200 u on a bodyless node that orbits a sun at 3000 u. Its rail reads the
# node's own terms; a law that counts the body's own mass in the node's family term pulls it toward the node.
# Reuses probe3.py's term builders (its definitions only; the probe's main loop is not run).
import os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, 'probe3.py'), encoding='utf-8').read()
g = {'__file__': os.path.join(HERE, 'probe3.py')}
exec(src[:src.index('rng = np.random')], g); g['rng'] = np.random.default_rng(7)
exec(src[src.index('def zone_struct'):src.index('pick = [int')], g)
par = np.array([-1, 0, 1]); D = np.array([0., 3000., 200.]); m = np.array([5000., 0., 100.]); body = np.array([True, False, True])
own = g['strength'](100.0) * 200 / (200 ** 2 + g['core_radius'](100.0) ** 2)
for name, build in (('tapered draft', g['build_terms_tapered']), ('band', g['build_terms_band'])):
    T = build(par, D, m, body, g['TIDAL'])[0]
    print(f'{name}: the node pulls the lone body at {g["own_pull"](T, 1, 200.0) / own:.3f} of its own pull')
