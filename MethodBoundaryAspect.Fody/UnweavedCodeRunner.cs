using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MethodBoundaryAspect.Fody
{
    public class UnweavedCodeRunner : IDisposable
    {
        const string DataKey = "data";
        const string ResultKey = "result";
        AppDomain _appDomain;
        ModuleDefinition _module;
        Action<string> _logger;

        void IDisposable.Dispose()
        {
            AppDomain.Unload(_appDomain);
        }

        public UnweavedCodeRunner(ModuleDefinition module, string addinPath, byte[] unweavedAssembly, List<string> otherReferencePaths, Action<string> logger)
        {
            _module = module;
            var setup = AppDomain.CurrentDomain.SetupInformation;
            if (addinPath != null)
                setup = new AppDomainSetup()
                {
                    ShadowCopyFiles = "true",
                    ApplicationBase = addinPath
                };
            _appDomain = AppDomain.CreateDomain("unweavedCodeDomain", null, setup);
            _logger = logger;

            var loader = new AssemblyLoadData()
            {
                AssemblyContents = unweavedAssembly,
                ReferencePaths = otherReferencePaths ?? new List<string>()
            };

            _appDomain.DoCallBack(loader.LoadAssembly);
        }

        public static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return ((AppDomain)sender).GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
        }

        static UnweavedCodeRunner()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }
        
        public bool CompileTimeValidate(CustomAttribute attribute, TypeReference type, string typeName, int methodtoken)
        {
            var data = new CompileTimeValidationData();
            data.AspectType = attribute.AttributeType.ToAssemblyQualifiedTypeName(_module);
            data.Blob = attribute.GetBlob();
            data.ConstructorToken = attribute.Constructor.MetadataToken.ToInt32();
            data.MethodToken = methodtoken;
            data.TypeBeingWoven = type.ToAssemblyQualifiedTypeName(_module);
            data.TypeDeclaringMethod = typeName;

            _appDomain.DoCallBack(data.Validate);

            return (bool)_appDomain.GetData("result");
        }
    }
}
