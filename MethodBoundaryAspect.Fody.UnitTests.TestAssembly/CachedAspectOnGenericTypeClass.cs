using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly
{
    [CachedAspect]
    public class CachedAspectOnGenericTypeClass<T>
    {
        public int ReturnFour()
        {
            return 4;
        }
    }
}
