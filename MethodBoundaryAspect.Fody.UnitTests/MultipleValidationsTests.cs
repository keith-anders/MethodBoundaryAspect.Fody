using FluentAssertions;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;
using System;
using Xunit;

namespace MethodBoundaryAspect.Fody.UnitTests
{
    public class MultipleValidationsTests : MethodBoundaryAspectTestBase
    {
        private static readonly Type TestClassType = typeof(ValidationClass4);

        int WasCalled()
        {
            return (int)AssemblyLoader.InvokeMethod(typeof(ValidatableAspect).FullName, nameof(ValidatableAspect.TimesCalled));
        }

        [Fact]
        public void IfOverriddenMethodHasMultipleCopiesOfAnAspect_ThenAllCopiesAreRun()
        {
            // Arrange
            const string testMethodName = "get_StringProperty";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(2);
        }
    }
}
