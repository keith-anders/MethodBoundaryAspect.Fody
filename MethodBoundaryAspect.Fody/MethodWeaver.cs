using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MethodBoundaryAspect.Fody
{
    internal class MethodWeaver : IDisposable
    {
        internal MethodWeaver(ModuleDefinition moduleDefinition, TypeReference typeBeingWoven, MethodDefinition method)
        {
            _typeBeingWoven = typeBeingWoven;
            _moduleDefinition = moduleDefinition;
            _method = method;
        }

        private MethodDefinition _method;
        private ModuleDefinition _moduleDefinition;
        private TypeReference _typeBeingWoven;
        private NamedInstructionBlockChain _createArgumentsArray;
        private MethodBodyPatcher _methodBodyChanger;
        private bool _finished;

        public int WeaveCounter { get; private set; }
        
        object[] GetRuntimeAttributeArgs(IEnumerable<CustomAttributeArgument> args)
        {
            return args.Select(GetRuntimeAttributeArg).ToArray();
        }

        object GetRuntimeAttributeArg(CustomAttributeArgument arg)
        {
            switch (arg.Value)
            {
                case CustomAttributeArgument[] array:
                    return GetRuntimeAttributeArgs(array);
                case CustomAttributeArgument arg2:
                    return GetRuntimeAttributeArg(arg2);
                default:
                    return arg.Value;
            }
        }
        
        public void Weave(CustomAttribute aspect, AspectMethods overriddenAspectMethods, string typeName, int methodToken, Validator compileTimeValidate)
        {
            if (overriddenAspectMethods == AspectMethods.None)
                return;

            if (overriddenAspectMethods.HasFlag(AspectMethods.CompileTimeValidate))
            {
                if (!compileTimeValidate(aspect, _typeBeingWoven, typeName, methodToken))
                    return;
            }

            var creator = new InstructionBlockChainCreator(_method, aspect.AttributeType, _moduleDefinition, WeaveCounter, _typeBeingWoven);

            _methodBodyChanger = new MethodBodyPatcher(_method.Name, _method);
            var saveReturnValue = creator.SaveReturnValue();
            var loadReturnValue = creator.LoadValueOnStack(saveReturnValue);
            _methodBodyChanger.Unify(saveReturnValue, loadReturnValue);

            if (WeaveCounter == 0)
                _createArgumentsArray = creator.CreateMethodArgumentsArray();

            var createMethodExecutionArgsInstance = creator.CreateMethodExecutionArgsInstance(_createArgumentsArray);
            _methodBodyChanger.AddCreateMethodExecutionArgs(createMethodExecutionArgsInstance);

            var createAspectInstance = creator.LoadAspectInstance(aspect, _typeBeingWoven);
            if (overriddenAspectMethods.HasFlag(AspectMethods.OnEntry))
            {
                var callAspectOnEntry = creator.CallAspectOnEntry(createAspectInstance,
                    createMethodExecutionArgsInstance);
                _methodBodyChanger.AddOnEntryCall(createAspectInstance, callAspectOnEntry);
            }

            if (overriddenAspectMethods.HasFlag(AspectMethods.OnExit))
            {
                var setMethodExecutionArgsReturnValue =
                    creator.SetMethodExecutionArgsReturnValue(createMethodExecutionArgsInstance, loadReturnValue);
                var callAspectOnExit = creator.CallAspectOnExit(createAspectInstance,
                    createMethodExecutionArgsInstance);
                var readReturnValue = creator.ReadReturnValue(createMethodExecutionArgsInstance, saveReturnValue);

                _methodBodyChanger.AddOnExitCall(createAspectInstance, callAspectOnExit, setMethodExecutionArgsReturnValue, readReturnValue);
            }

            if (overriddenAspectMethods.HasFlag(AspectMethods.OnException))
            {
                var setMethodExecutionArgsExceptionFromStack =
                    creator.SetMethodExecutionArgsExceptionFromStack(createMethodExecutionArgsInstance);

                var callAspectOnException = creator.CallAspectOnException(createAspectInstance,
                    createMethodExecutionArgsInstance);
                _methodBodyChanger.AddOnExceptionCall(createAspectInstance, callAspectOnException,
                    setMethodExecutionArgsExceptionFromStack);
            }

            if (_methodBodyChanger.HasMultipleReturnAndEndsWithThrow)
                _methodBodyChanger.ReplaceThrowAtEndOfRealBodyWithReturn();
            else if (_methodBodyChanger.EndsWithThrow)
            {
                var saveThrownException = creator.SaveThrownException();
                var loadThrownException = creator.LoadValueOnStack(saveThrownException);
                var loadThrownException2 = creator.LoadValueOnStack(saveThrownException);
                _methodBodyChanger.FixThrowAtEndOfRealBody(
                    saveThrownException,
                    loadThrownException,
                    loadThrownException2);
            }

            _methodBodyChanger.OptimizeBody();

            Catel.Fody.CecilExtensions.UpdateDebugInfo(_method);

            WeaveCounter++;
        }

        public void Finish()
        {
            if (_finished)
                return;

            if (_methodBodyChanger != null)
                _methodBodyChanger.AddCreateArgumentsArray(_createArgumentsArray);
            _finished = true;
        }

        public void Dispose()
        {
            Finish();
        }
    }
}