# Checks: A&S K0/K1 against a direct integral; each law's force equals -dphi/dr (grid equals field).
import numpy as np, sys, os
sys.path.insert(0, os.path.dirname(__file__))
from laws import *
x = np.array([0.05, 0.3, 1.0, 1.99, 2.01, 4.0, 10.0])
t = np.linspace(0, 12, 200001)
ref0 = np.array([np.trapezoid(np.exp(-xi * np.cosh(t)), t) for xi in x])
ref1 = np.array([np.trapezoid(np.exp(-xi * np.cosh(t)) * np.cosh(t), t) for xi in x])
print('K0 rel err', np.max(np.abs(k0(x) / ref0 - 1)), ' K1 rel err', np.max(np.abs(k1(x) / ref1 - 1)))
r = np.linspace(20, 5000, 20000); c = 300.0
for name, tf, pf, s in [('log', tail_log, phi_log, 0), ('k0', tail_k0, phi_k0, 800.0), ('cap', tail_cap, phi_cap, 1000.0),
                         ('switch', tail_switch, phi_switch, 1000.0), ('hyp', tail_hyp, phi_hyp, 1500.0)]:
    ph = pf(c, r, s); dphi = np.gradient(ph, r); F = tf(c, r, s)
    print(f'{name:7s} max |F - dphi/dr| / max F = {np.max(np.abs(F - dphi)[5:-5]) / F.max():.2e}')
