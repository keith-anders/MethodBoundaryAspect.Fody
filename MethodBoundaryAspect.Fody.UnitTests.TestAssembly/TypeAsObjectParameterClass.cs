using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly
{
    public class TypeAsObjectParameterClass
    {
        public string GetTypeName(string arg)
        {
            return GetTypeName2(Type.GetType(arg));
        }

        [OverwriteParametersAspect(typeof(TypeAsObjectParameterClass))]
        public string GetTypeName2(Type type)
        {
            return type.ToString();
        }
    }
}
