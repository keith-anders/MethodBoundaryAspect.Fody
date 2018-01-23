﻿using FluentAssertions;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;
using System;
using Xunit;

namespace MethodBoundaryAspect.Fody.UnitTests
{
    public class ValidationAspectTests : MethodBoundaryAspectTestBase
    {
        static readonly Type TestClassType = typeof(ValidationClass);
        
        bool WasCalled()
        {
            return (bool)AssemblyLoader.InvokeMethod(typeof(ValidatedAspect).FullName, nameof(ValidatedAspect.WasCalled));
        }

        [Fact]
        public void IfCompileTimeValidateReturnsFalseForMethod_ThenMethodIsNotIntercepted()
        {
            // Arrange
            const string testMethodName = "Method";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void IfCompileTimeValidateReturnsFalseForPropertyAccessor_ThenAccessorIsNotIntercepted()
        {
            // Arrange
            const string testMethodName = "set_StringProperty";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName, "test");
            var result = WasCalled();

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void IfCompileTimeValidateReturnsTrueForPropertyAccessor_ThenAccessorIsIntercepted()
        {
            // Arrange
            const string testMethodName = "get_StringProperty";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void IfCompileTimeValidateReturnsFalseForMethodWithRefParam_ThenMethodIsNotIntercepted()
        {
            // Arrange
            const string testMethodName = "MethodWithRefParam";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName, new object[] { 42 });
            var result = WasCalled();

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void IfCompileTimeValidateReturnsTrueForMethodWithRefParam_ThenMethodIsIntercepted()
        {
            // Arrange
            const string testMethodName = "MethodWithRefParamX";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName, new object[] { 42 });
            var result = WasCalled();

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void IfCompileTimeValidateReturnsFalseForMethodWithOutParam_ThenMethodIsNotIntercepted()
        {
            // Arrange
            const string testMethodName = "MethodWithOutParam";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName, new object[] { 0 });
            var result = WasCalled();

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void IfCompileTimeValidateReturnsTrueForMethodWithOutParam_ThenMethodIsIntercepted()
        {
            // Arrange
            const string testMethodName = "MethodWithOutParamX";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName, new object[] { 0 });
            var result = WasCalled();

            // Assert
            result.Should().Be(true);
        }

        static readonly Type TestClassType2 = typeof(ValidationClass2);

        [Fact]
        public void IfCompileTimeValidateHasCtorParam_ThenParamAppearsInCalculation()
        {
            // Arrange
            const string testMethodName = "Method";
            WeaveAssemblyMethodAndLoad(TestClassType2, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType2.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void IfCompileTimeValidateHasPropertySet_ThenPropertyAppearsInCalculation()
        {
            // Arrange
            const string testMethodName = "Method2";
            WeaveAssemblyMethodAndLoad(TestClassType2, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType2.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void IfCompileTimeValidateHasFieldSet_ThenFieldAppearsInCalculation()
        {
            // Arrange
            const string testMethodName = "Method3";
            WeaveAssemblyMethodAndLoad(TestClassType2, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType2.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void IfCompileTimeValidateHasArrayCtorParam_ThenArrayAppearsInCalculation()
        {
            // Arrange
            const string testMethodName = "Method4";
            WeaveAssemblyMethodAndLoad(TestClassType2, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType2.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void IfCompileTimeValidateHasArrayCtorParamAndDoesNotIntercept_ThenMethodIsNotIntercepted()
        {
            // Arrange
            const string testMethodName = "Method5";
            WeaveAssemblyMethodAndLoad(TestClassType2, testMethodName);

            // Act
            AssemblyLoader.InvokeMethod(TestClassType2.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(false);
        }
    }
}
