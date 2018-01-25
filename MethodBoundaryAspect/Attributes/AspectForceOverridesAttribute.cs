using System;

namespace MethodBoundaryAspect.Fody.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple =false, Inherited =false)]
    public sealed class AspectForceOverridesAttribute : Attribute
    {
    }
}
