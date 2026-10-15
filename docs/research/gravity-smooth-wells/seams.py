# Seam jumps of the piecewise shader forms (A&S K0/K1 at x = 2; A&S E1 at u = 1) and of today's core at R.
# Each branch is evaluated as its own analytic function in mpmath (50 digits) and differentiated at the seam,
# so the jump is the branch mismatch itself, not finite-difference noise.
import mpmath as mp
mp.mp.dps = 50
def i0(x): t = (x / 3.75) ** 2; return 1 + t*(3.5156229 + t*(3.0899424 + t*(1.2067492 + t*(0.2659732 + t*(0.0360768 + t*0.0045813)))))
def i1(x): t = (x / 3.75) ** 2; return x * (0.5 + t*(0.87890594 + t*(0.51498869 + t*(0.15084934 + t*(0.02658733 + t*(0.00301532 + t*0.00032411))))))
def k0_lo(x): y = (x / 2) ** 2; return -mp.log(x / 2) * i0(x) + (-0.57721566 + y*(0.42278420 + y*(0.23069756 + y*(0.03488590 + y*(0.00262698 + y*(0.00010750 + y*0.00000740))))))
def k0_hi(x): z = 2 / x; return mp.exp(-x) / mp.sqrt(x) * (1.25331414 + z*(-0.07832358 + z*(0.02189568 + z*(-0.01062446 + z*(0.00587872 + z*(-0.00251540 + z*0.00053208))))))
def k1_lo(x): y = (x / 2) ** 2; return mp.log(x / 2) * i1(x) + (1 / x) * (1 + y*(0.15443144 + y*(-0.67278579 + y*(-0.18156897 + y*(-0.01919402 + y*(-0.00110404 + y*(-0.00004686)))))))
def k1_hi(x): z = 2 / x; return mp.exp(-x) / mp.sqrt(x) * (1.25331414 + z*(0.23498619 + z*(-0.03655620 + z*(0.01504268 + z*(-0.00780353 + z*(0.00325614 + z*(-0.00068245)))))))
def e1_lo(u): return -mp.log(u) + (-0.57721566 + u*(0.99999193 + u*(-0.24991055 + u*(0.05519968 + u*(-0.00976004 + u*0.00107857)))))
def e1_hi(u):
    p = 0.2677737343 + u*(8.6347608925 + u*(18.0590169730 + u*(8.5733287401 + u)))
    q = 3.9584969228 + u*(21.0996530827 + u*(25.6329561486 + u*(9.5733223454 + u)))
    return mp.exp(-u) / u * p / q

def report(title, lo, hi, true, x0, unit):
    print(title)
    for n in range(4):
        a, b, t = mp.diff(lo, x0, n), mp.diff(hi, x0, n), mp.diff(true, x0, n)
        j = b - a
        print(f'  d^{n}: jump {mp.nstr(j, 3):>10s} {unit}^{n}   true value {mp.nstr(t, 6):>10s}   |jump|/|true| {mp.nstr(abs(j / t), 3)}')

# phi = -c K0(r/lam): phi^(n) jump = -c/lam^n * (K0_hi^(n) - K0_lo^(n)) at x = 2, i.e. at r = 2 lam
report('A&S K0 at x = 2 (phi = -c K0(r/lam); jumps in K0^(n), multiply by -c/lam^n):', k0_lo, k0_hi, lambda x: mp.besselk(0, x), 2, '1/lam')
report('A&S K1 at x = 2 (force = (c/lam) K1(r/lam)):', k1_lo, k1_hi, lambda x: mp.besselk(1, x), 2, '1/lam')
# Lamb-Oseen phi = (c/2) Ein(r^2/eps^2): seam at r = eps. Differentiate in r (eps = 1, c = 1) -> units c/eps^n
ein_lo = lambda r: (e1_lo(r * r) + mp.log(r * r) + mp.euler) / 2
ein_hi = lambda r: (e1_hi(r * r) + mp.log(r * r) + mp.euler) / 2
ein_true = lambda r: (mp.e1(r * r) + mp.log(r * r) + mp.euler) / 2
report('A&S E1 at u = 1 (phi = (c/2) Ein(r^2/eps^2), seam at r = eps; jumps in units c/eps^n):', ein_lo, ein_hi, ein_true, 1, '1/eps')
# today's core at R (c = 1, R = 1): inside r^2/2, outside ln r + 1/2
report("Today's core at r = R (phi: r^2/2R^2 inside, ln(r/R)+1/2 beyond; units c/R^n):",
       lambda r: r * r / 2, lambda r: mp.log(r) + 0.5, lambda r: mp.log(r) + 0.5, 1, '1/R')
