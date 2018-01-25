using System.Reflection;
using MethodBoundaryAspect.Fody.Attributes;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects
{
    [AspectForceOverrides]
    public class ValidatableAspect : OnMethodBoundaryAspect
    {
        static int Called = 0;

        public static int TimesCalled()
        {
            var tmp = Called;
            Called = 0;
            return tmp;
        }

        public override bool CompileTimeValidate(MethodBase method)
        {
            return !method.Name.Contains("XXX");
        }

        public override void OnEntry(MethodExecutionArgs arg)
        {
            ++Called;
        }
    }
}
