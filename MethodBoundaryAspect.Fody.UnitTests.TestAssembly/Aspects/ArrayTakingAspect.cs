using MethodBoundaryAspect.Fody.Attributes;
using System;
using System.Linq;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects
{
    public class IntArrayTakingAspect : OnMethodBoundaryAspect
    {
        int[] _values;
        
        public IntArrayTakingAspect(params int[] values)
        {
            _values = values;
        }

        public override void OnExit(MethodExecutionArgs arg)
        {
            ArrayTakingAspectMethod.Result = _values;
        }
    }

    public class TypeArrayTakingAspect : OnMethodBoundaryAspect
    {
        Type[] _types;

        public TypeArrayTakingAspect(params Type[] types)
        {
            _types = types;
        }

        public override void OnExit(MethodExecutionArgs arg)
        {
            TypeAsObjectParameterClass.Result = _types.Select(t => t.ToString()).ToArray();
        }
    }
}
