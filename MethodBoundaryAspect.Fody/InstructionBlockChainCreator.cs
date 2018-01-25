using MethodBoundaryAspect.Fody.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;
using System.Reflection;

namespace MethodBoundaryAspect.Fody
{
    public class InstructionBlockChainCreator
    {
        private readonly MethodDefinition _method;
        private readonly TypeReference _aspectTypeDefinition;
        private readonly ModuleDefinition _moduleDefinition;
        private readonly int _aspectCounter;

        private readonly ReferenceFinder _referenceFinder;
        private readonly InstructionBlockCreator _creator;

        public InstructionBlockChainCreator(MethodDefinition method, TypeReference aspectTypeDefinition, ModuleDefinition moduleDefinition, int aspectCounter, TypeReference typeBeingWoven)
        {
            _method = method;
            _aspectTypeDefinition = aspectTypeDefinition;
            _moduleDefinition = moduleDefinition;
            _aspectCounter = aspectCounter;

            _referenceFinder = new ReferenceFinder(_moduleDefinition);
            _creator = new InstructionBlockCreator(_method, _referenceFinder, typeBeingWoven);
        }

        private string CreateVariableName(string name)
        {
            return string.Format("__fody${0}${1}", _aspectCounter, name);
        }

        public NamedInstructionBlockChain CreateMethodArgumentsArray()
        {
            //  argument values
            var argumentsTypeReference = _referenceFinder.GetTypeReference(typeof(object[]));
            var argumentsArrayVariable = _creator.CreateVariable(argumentsTypeReference);
            var createObjectArrayWithMethodArgumentsBlock =
                _creator.CreateObjectArrayWithMethodArguments(argumentsArrayVariable,
                    _referenceFinder.GetTypeReference(typeof(object)));

            var blockChain = new NamedInstructionBlockChain(argumentsArrayVariable,
                argumentsTypeReference);
            blockChain.Add(createObjectArrayWithMethodArgumentsBlock);
            return blockChain;
        }

        public NamedInstructionBlockChain CreateMethodExecutionArgsInstance(NamedInstructionBlockChain argumentsArrayChain)
        {
            // instance value
            var objectType = _referenceFinder.GetTypeReference(typeof(object));
            var instanceVariable = _creator.CreateVariable(objectType);
            var createThisVariableBlock = _creator.CreateThisVariable(instanceVariable, objectType);

            // MethodExecutionArgs instance
            var onEntryMethodTypeRef =
                _aspectTypeDefinition.Resolve().BaseType.Resolve().Methods.Single(x => x.Name == "OnEntry");
            var firstParameterType = onEntryMethodTypeRef.Parameters.Single().ParameterType;
            var methodExecutionArgsTypeRef = _moduleDefinition.ImportReference(firstParameterType);
            var methodExecutionArgsVariable = _creator.CreateVariable(methodExecutionArgsTypeRef);
            var newObjectMethodExecutionArgsBlock = _creator.NewObject(
                methodExecutionArgsVariable,
                methodExecutionArgsTypeRef, 
                _moduleDefinition,
                _aspectCounter);

            InstructionBlock callSetInstanceBlock = null;
            if (!_method.IsStatic)
            {
                var methodExecutionArgsSetInstanceMethodRef =
                    _referenceFinder.GetMethodReference(methodExecutionArgsTypeRef, md => md.Name == "set_Instance");
                callSetInstanceBlock = _creator.CallVoidInstanceMethod(methodExecutionArgsVariable,
                    methodExecutionArgsSetInstanceMethodRef, instanceVariable);
            }

            var methodExecutionArgsSetArgumentsMethodRef =
                _referenceFinder.GetMethodReference(methodExecutionArgsTypeRef, md => md.Name == "set_Arguments");
            var callSetArgumentsBlock = _creator.CallVoidInstanceMethod(methodExecutionArgsVariable,
                methodExecutionArgsSetArgumentsMethodRef, argumentsArrayChain.Variable);

            var methodBaseTypeRef = _referenceFinder.GetTypeReference(typeof (MethodBase));
            var methodBaseVariable = _creator.CreateVariable(methodBaseTypeRef);
            var methodBaseGetCurrentMethod = _referenceFinder.GetMethodReference(methodBaseTypeRef,
                md => md.Name == "GetCurrentMethod");
            var callGetCurrentMethodBlock = _creator.CallStaticMethod(methodBaseGetCurrentMethod, methodBaseVariable);

            var methodExecutionArgsSetMethodBaseMethodRef =
                _referenceFinder.GetMethodReference(methodExecutionArgsTypeRef, md => md.Name == "set_Method");
            var callSetMethodBlock = _creator.CallVoidInstanceMethod(methodExecutionArgsVariable,
                methodExecutionArgsSetMethodBaseMethodRef, methodBaseVariable);

            var newMethodExectionArgsBlockChain = new NamedInstructionBlockChain(methodExecutionArgsVariable,
                methodExecutionArgsTypeRef);
            newMethodExectionArgsBlockChain.Add(newObjectMethodExecutionArgsBlock);
            newMethodExectionArgsBlockChain.Add(callSetArgumentsBlock);
            newMethodExectionArgsBlockChain.Add(callGetCurrentMethodBlock);
            newMethodExectionArgsBlockChain.Add(callSetMethodBlock);
            if (callSetInstanceBlock != null)
            {
                newMethodExectionArgsBlockChain.Add(createThisVariableBlock);
                newMethodExectionArgsBlockChain.Add(callSetInstanceBlock);
            }

            return newMethodExectionArgsBlockChain;
        }

        public NamedInstructionBlockChain CreateAspectInstance(CustomAttribute aspect)
        {
            var aspectTypeReference = _moduleDefinition.ImportReference(_aspectTypeDefinition);
            var aspectVariable = _creator.CreateVariable(aspectTypeReference);
            var newObjectAspectBlock = _creator.NewObject(aspectVariable, aspectTypeReference, _moduleDefinition, aspect, _aspectCounter);

            var newObjectAspectBlockChain = new NamedInstructionBlockChain(aspectVariable, aspectTypeReference);
            newObjectAspectBlockChain.Add(newObjectAspectBlock);
            return newObjectAspectBlockChain;
        }

        public InstructionBlockChain SetMethodExecutionArgsReturnValue(
            NamedInstructionBlockChain newMethodExectionArgsBlockChain, NamedInstructionBlockChain loadReturnValue)
        {
            if (!_creator.HasReturnValue())
                return new InstructionBlockChain();

            var methodExecutionArgsSetReturnValueMethodRef =
                _referenceFinder.GetMethodReference(newMethodExectionArgsBlockChain.TypeReference,
                    md => md.Name == "set_ReturnValue");
            var callSetReturnValueBlock = _creator.CallVoidInstanceMethod(newMethodExectionArgsBlockChain.Variable,
                methodExecutionArgsSetReturnValueMethodRef, loadReturnValue.Variable);

            var block = new InstructionBlockChain();
            block.Add(callSetReturnValueBlock);
            return block;
        }

        public NamedInstructionBlockChain SetMethodExecutionArgsExceptionFromStack(
            NamedInstructionBlockChain createMethodExecutionArgsInstance)
        {
            var exceptionTypeRef = _referenceFinder.GetTypeReference(typeof(Exception));
            var exceptionVariable = _creator.CreateVariable(exceptionTypeRef);
            var assignExceptionVariable = _creator.AssignValueFromStack(exceptionVariable);

            var methodExecutionArgsSetExceptionMethodRef =
                _referenceFinder.GetMethodReference(createMethodExecutionArgsInstance.TypeReference,
                    md => md.Name == "set_Exception");
            var callSetExceptionBlock = _creator.CallVoidInstanceMethod(createMethodExecutionArgsInstance.Variable,
                methodExecutionArgsSetExceptionMethodRef, exceptionVariable);

            var block = new NamedInstructionBlockChain(exceptionVariable, exceptionTypeRef);
            block.Add(assignExceptionVariable);
            block.Add(callSetExceptionBlock);
            return block;
        }

        public NamedInstructionBlockChain LoadValueOnStack(NamedInstructionBlockChain instructionBlock)
        {
            var block = new NamedInstructionBlockChain(instructionBlock.Variable, instructionBlock.TypeReference);
            if (instructionBlock.Variable == null)
                return block;

            var loadReturnValueBlock = _creator.PushValueOnStack(instructionBlock.Variable);
            block.Add(loadReturnValueBlock);
            return block;
        }

        public NamedInstructionBlockChain SaveReturnValue()
        {
            var retType = _moduleDefinition.ImportReference(_method.ReturnType);
            if (!_creator.HasReturnValue())
                return new NamedInstructionBlockChain(null, retType);

            var returnValueVariable = _creator.CreateVariable(retType);
            var block = new NamedInstructionBlockChain(returnValueVariable, retType);

            var instructions = _creator.SaveReturnValueFromStack(returnValueVariable);
            block.Add(instructions);
            return block;
        }

        public NamedInstructionBlockChain SaveThrownException()
        {
            var exceptionTypeRef = _referenceFinder.GetTypeReference(typeof(Exception));

            if (!_creator.HasThrowAsReturn())
                return new NamedInstructionBlockChain(null, exceptionTypeRef);

            var exceptionVariable = _creator.CreateVariable(exceptionTypeRef);
            var block = new NamedInstructionBlockChain(exceptionVariable, exceptionTypeRef);

            var instructions = _creator.AssignValueFromStack(exceptionVariable);
            block.Add(instructions);
            return block;
        }

        public InstructionBlockChain CallAspectOnEntry(NamedInstructionBlockChain createAspectInstance,
            NamedInstructionBlockChain newMethodExectionArgsBlockChain)
        {
            var onEntryMethodRef = _referenceFinder.GetMethodReference(createAspectInstance.TypeReference,
                md => md.Name == "OnEntry");
            var callOnEntryBlock = _creator.CallVoidInstanceMethod(createAspectInstance.Variable, onEntryMethodRef,
                newMethodExectionArgsBlockChain.Variable);

            var callAspectOnEntryBlockChain = new InstructionBlockChain();
            callAspectOnEntryBlockChain.Add(callOnEntryBlock);
            return callAspectOnEntryBlockChain;
        }

        public InstructionBlockChain ReadReturnValue(NamedInstructionBlockChain newMethodExecutionArgsBlockChain,
            NamedInstructionBlockChain returnValue)
        {
            if (!_creator.HasReturnValue())
                return new InstructionBlockChain();
            
            var getReturnValue =
                _referenceFinder.GetMethodReference(newMethodExecutionArgsBlockChain.TypeReference,
                    md => md.Name == "get_ReturnValue");

            var readValueBlock = _creator.CallInstanceMethod(newMethodExecutionArgsBlockChain.Variable, returnValue.Variable, getReturnValue);

            var readValueBlockChain = new InstructionBlockChain();
            readValueBlockChain.Add(readValueBlock);
            return readValueBlockChain;
        }

        public InstructionBlockChain CallAspectOnExit(NamedInstructionBlockChain createAspectInstance,
            NamedInstructionBlockChain newMethodExectionArgsBlockChain)
        {
            var onExitMethodRef = _referenceFinder.GetMethodReference(createAspectInstance.TypeReference,
                md => md.Name == "OnExit");
            var callOnExitBlock = _creator.CallVoidInstanceMethod(createAspectInstance.Variable, onExitMethodRef,
                newMethodExectionArgsBlockChain.Variable);

            var callAspectOnExitBlockChain = new InstructionBlockChain();
            callAspectOnExitBlockChain.Add(callOnExitBlock);
            return callAspectOnExitBlockChain;
        }

        public InstructionBlockChain CallAspectOnException(NamedInstructionBlockChain createAspectInstance,
            NamedInstructionBlockChain newMethodExectionArgsBlockChain)
        {
            var onExceptionMethodRef = _referenceFinder.GetMethodReference(createAspectInstance.TypeReference,
                md => md.Name == "OnException");
            var callOnExceptionBlock = _creator.CallVoidInstanceMethod(createAspectInstance.Variable,
                onExceptionMethodRef,
                newMethodExectionArgsBlockChain.Variable);

            var callAspectOnExceptionBlockChain = new InstructionBlockChain();
            callAspectOnExceptionBlockChain.Add(callOnExceptionBlock);
            return callAspectOnExceptionBlockChain;
        }
    }
}