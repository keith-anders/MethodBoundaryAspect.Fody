using MethodBoundaryAspect.Fody.Attributes;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MethodBoundaryAspect.Fody
{
    internal class MethodWeaver : IDisposable
    {
        internal MethodWeaver(ModuleDefinition moduleDefinition, TypeReference typeBeingWoven, MethodDefinition method, Assembly loadedAssembly)
        {
            _typeBeingWoven = typeBeingWoven;
            _moduleDefinition = moduleDefinition;
            _method = method;

            string[] methodParams = _method.Parameters.Select(p => ToAssemblyQualifiedTypeName(p.ParameterType, _moduleDefinition)).ToArray();

            var typeInfo = loadedAssembly.GetTypes().FirstOrDefault(t => t.FullName == _typeBeingWoven.FullName);
            if (typeInfo == null)
                throw new InvalidOperationException(String.Format("Could not find type '{0}'.", _typeBeingWoven.FullName));

            Type[] parameters = methodParams.Select(Type.GetType).ToArray();
            _methodInfo = typeInfo.GetMethod(_method.Name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                null,
                parameters,
                new ParameterModifier[0]);
        }

        private MethodDefinition _method;
        private ModuleDefinition _moduleDefinition;
        private TypeReference _typeBeingWoven;
        private NamedInstructionBlockChain _createArgumentsArray;
        private MethodBodyPatcher _methodBodyChanger;
        private bool _finished;
        private MethodInfo _methodInfo;

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

        string ToAssemblyQualifiedTypeName(TypeReference typeRef, ModuleDefinition module)
        {
            string typeName = $"{typeRef.Namespace}.{typeRef.Name}";
            if (typeRef is GenericInstanceType gen)
                typeName += $"[[{String.Join(",", gen.GenericArguments.Select(p => ToAssemblyQualifiedTypeName(p, module)))}]]";
            typeName += ", ";
            typeName += module.ImportReference(typeRef).Resolve().Module.Assembly.FullName;
            return typeName;
        }

        public void Weave(CustomAttribute aspect, AspectMethods overriddenAspectMethods)
        {
            if (overriddenAspectMethods == AspectMethods.None)
                return;

            if (overriddenAspectMethods.HasFlag(AspectMethods.CompileTimeValidate))
            {
                string[] ctorParams = aspect.Constructor.Parameters.Select(p => p.ParameterType.FullName).ToArray();

                Type aspectType = Type.GetType(ToAssemblyQualifiedTypeName(aspect.AttributeType, _moduleDefinition));
                if (aspectType == null)
                    throw new InvalidOperationException("Could not find aspect type: " + aspect.AttributeType.FullName);
                
                var ctorInfo = aspectType.GetConstructor(ctorParams.Select(Type.GetType).ToArray());
                if (ctorInfo == null)
                    throw new InvalidOperationException("Could not find constructor for aspect.");

                object[] ctorArgs = GetRuntimeAttributeArgs(aspect.ConstructorArguments);
                var aspectInstance = ctorInfo.Invoke(ctorArgs) as OnMethodBoundaryAspect;
                if (aspectInstance == null)
                    throw new InvalidOperationException("Could not create aspect.");

                foreach (var fieldSetter in aspect.Fields)
                {
                    var field = aspectType.GetField(fieldSetter.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field == null)
                        throw new InvalidOperationException(String.Format("Could not find field named {0}", fieldSetter.Name));
                    field.SetValue(aspectInstance, fieldSetter.Argument.Value);
                }

                foreach (var propSetter in aspect.Properties)
                {
                    var prop = aspectType.GetProperty(propSetter.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop == null)
                        throw new InvalidOperationException(String.Format("Could not find property named {0}", propSetter.Name));
                    prop.SetValue(aspectInstance, propSetter.Argument.Value);
                }

                if (!aspectInstance.CompileTimeValidate(_methodInfo))
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

            var createAspectInstance = creator.CreateAspectInstance(aspect);
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
                
                _methodBodyChanger.AddOnExitCall(createAspectInstance, callAspectOnExit, setMethodExecutionArgsReturnValue);
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