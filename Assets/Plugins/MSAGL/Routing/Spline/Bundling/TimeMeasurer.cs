using System;
using Microsoft.Msagl.DebugHelpers;

namespace Microsoft.Msagl.Routing.Spline.Bundling {
    /// <summary>
    /// Outputs run time in debug mode 
    /// </summary>
    internal class TimeMeasurer {
#if DEBUG && TEST_MSAGL && REPORTING
        static DebugHelpers.Timer timer;
        static TimeMeasurer() {
            timer = new DebugHelpers.Timer();
            timer.Start();
        }
#endif

        internal delegate void Task();

        internal static void DebugOutput(string str) {
#if DEBUG && TEST_MSAGL && REPORTING
            timer.Stop();
            Console.Write("{0}: ", String.Format("{0:0.000}", timer.Duration));
            Console.WriteLine(str);
#endif
        }
    }
}
