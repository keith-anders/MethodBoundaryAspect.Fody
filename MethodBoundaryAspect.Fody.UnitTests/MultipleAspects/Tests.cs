﻿using System;
using FluentAssertions;
using MethodBoundaryAspect.Fody.UnitTests.Unified;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace MethodBoundaryAspect.Fody.UnitTests.MultipleAspects
{
    public class Tests : UnifiedWeaverTestBase
    {
        private readonly Type _testType = typeof (TestMethods);
        
        [Fact]
        public void IfVoidEmptyMethodMethodIsWeaved_ThenPeVerifyShouldBeOk()
        {
            // Arrange
            var weaver = new ModuleWeaver();
            weaver.AddMethodFilter(_testType.FullName + ".VoidEmptyMethod");

            // Act
            weaver.Weave(Weave.DllPath);

            // Arrange
            AssertRunPeVerify();
            AssertUnifiedMethod(weaver.LastWeavedMethod);
        }

        [Fact]
        public void IfIntMethodIsWeaved_ThenPeVerifyShouldBeOk()
        {
            // Arrange
            var weaver = new ModuleWeaver();
            weaver.AddMethodFilter(_testType.FullName + ".IntMethod");

            // Act
            weaver.Weave(Weave.DllPath);

            // Arrange
            AssertRunPeVerify();
            AssertUnifiedMethod(weaver.LastWeavedMethod);
        }

        [Fact]
        public void IfVoidThrowMethodIsWeaved_ThenPeVerifyShouldBeOk()
        {
            // Arrange
            var weaver = new ModuleWeaver();
            weaver.AddMethodFilter(_testType.FullName + ".VoidThrowMethod");

            // Act
            weaver.Weave(Weave.DllPath);

            // Arrange
            AssertRunPeVerify();
            AssertUnifiedMethod(weaver.LastWeavedMethod, true);
        }

        [Fact]
        public void IfIntMethodIntWithMultipleReturnIsWeaved_ThenPeVerifyShouldBeOk()
        {
            // Arrange
            var weaver = new ModuleWeaver();
            weaver.AddMethodFilter(typeof (TestMethods).FullName + ".IntMethodIntWithMultipleReturn");

            // Act
            weaver.Weave(Weave.DllPath);

            // Arrange
            AssertRunPeVerify();
            AssertUnifiedMethod(weaver.LastWeavedMethod);
        }

        [Fact]
        public void IfVoidThrowMethodTryCatchIsWeaved_ThenPeVerifyShouldBeOk()
        {
            // Arrange
            var weaver = new ModuleWeaver();
            weaver.AddMethodFilter(typeof (TestMethods).FullName + ".VoidThrowMethodTryCatch");

            // Act
            weaver.Weave(Weave.DllPath);

            // Arrange
            AssertRunPeVerify();
            AssertUnifiedMethod(weaver.LastWeavedMethod);
        }

        private static void AssertUnifiedMethod(MethodDefinition method)
        {
            AssertUnifiedMethod(method, false);
        }

        private static void AssertUnifiedMethod(MethodDefinition method, bool methodThrows)
        {
            var instructions = method.Body.Instructions;
            instructions[0].OpCode.Should().Be(OpCodes.Nop);

            var lastIndex = instructions.Count - 1;
            instructions[lastIndex].OpCode.Should().Be(methodThrows ? OpCodes.Throw : OpCodes.Ret);
            if (method.ReturnType.Name == "Void")
            {
                if (methodThrows)
                {
                    instructions[lastIndex - 1].OpCode.Should().Be(OpCodes.Ldloc_S);
                    instructions[lastIndex - 9].OpCode.Should().Be(OpCodes.Ldloc_S);
                    instructions[lastIndex - 10].OpCode.Should().Be(OpCodes.Stloc_S);
                    instructions[lastIndex - 11].OpCode.Should().Be(OpCodes.Nop);
                }
                else
                {
                    instructions[lastIndex - 4].OpCode.Should().Be(OpCodes.Nop);
                    instructions[lastIndex - 5].OpCode.Should().Be(OpCodes.Nop);
                }
            }
            else
            {
                instructions[lastIndex - 1].OpCode.Should().Match(x => AllLdLocOpCodes.Contains((OpCode)x));

                if (instructions[lastIndex - 3].OpCode == OpCodes.Unbox_Any)
                {
                    lastIndex -= 4; // ldloc methodArgs; call get_ReturnValue; unbox.any; stloc returnValue;
                }

                instructions[lastIndex - 9].OpCode.Should().Be(OpCodes.Nop);
                instructions[lastIndex - 11].OpCode.Should().Be(OpCodes.Nop);
            }
        }
    }
}