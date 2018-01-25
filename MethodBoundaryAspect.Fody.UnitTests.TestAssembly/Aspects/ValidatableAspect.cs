﻿using System.Reflection;
using MethodBoundaryAspect.Fody.Attributes;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects
{
    public class ValidatableAspect : OnMethodBoundaryAspect
    {
        static bool Called = false;

        public static bool WasCalled() => Called;

        public override bool CompileTimeValidate(MethodBase method)
        {
            return !method.Name.Contains("XXX");
        }

        public override void OnEntry(MethodExecutionArgs arg)
        {
            Called = true;
        }
    }
}
