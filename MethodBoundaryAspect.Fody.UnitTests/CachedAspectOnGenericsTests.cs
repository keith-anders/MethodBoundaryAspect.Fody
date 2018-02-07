using FluentAssertions;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly;
using System;
using Xunit;

namespace MethodBoundaryAspect.Fody.UnitTests
{
    public class CachedAspectOnGenericsTests : MethodBoundaryAspectTestBase
    {
        static readonly Type TestClassType = typeof(CachedAspectOnGenericTypeClass<>);
        static readonly Type ConcreteTestClassType = typeof(CachedAspectOnGenericTypeClass<string>);
        const string testMethodName = "ReturnFour";

        public CachedAspectOnGenericsTests()
        {
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);
        }
        
        [Fact]
        public void IfCachedAspectIsCalledOnGenericType_ThenTypeIsUsed()
        {
            // Act
            var result = (int)AssemblyLoader.InvokeMethod(ConcreteTestClassType.FullName, testMethodName);
            
            // Assert
            result.Should().Be(1);
        }
    }
}
