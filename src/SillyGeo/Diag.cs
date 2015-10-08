using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SillyGeo
{
    public static class Diag
    {
        public static async Task ThrowElapsed(Func<Task> func)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                await func();
            }
            var res = sw.Elapsed;
            throw new Exception(res.ToString());
        }
    }
}
