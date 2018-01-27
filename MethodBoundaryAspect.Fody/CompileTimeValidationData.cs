using MethodBoundaryAspect.Fody.Attributes;
using System;
using System.IO;
using System.Linq;
using System.Reflection.Emit;

namespace MethodBoundaryAspect.Fody
{
    [Serializable]
    public class CompileTimeValidationData
    {
        public string AspectType { get; set; }
        public int ConstructorToken { get; set; }
        public byte[] Blob { get; set; }
        public string TypeBeingWoven { get; set; }
        public string TypeDeclaringMethod { get; set; }
        public int MethodToken { get; set; }
        
        static int NextType;

        public void Validate()
        {
            var data = this;

            Type aspectType = Type.GetType(data.AspectType);
            if (aspectType == null)
                throw new InvalidDataException("Could not find aspect type: " + data.AspectType);
            var ctor = aspectType.GetConstructors().FirstOrDefault(c => c.MetadataToken == data.ConstructorToken);
            if (ctor == null)
                throw new InvalidDataException("Could not find constructor for aspect.");

            var mod = AssemblyBuilder.DefineDynamicAssembly(new System.Reflection.AssemblyName("Tmp.dll"), System.Reflection.Emit.AssemblyBuilderAccess.Run).DefineDynamicModule("Tmp.dll");
            var t = mod.DefineType((++NextType).ToString(), System.Reflection.TypeAttributes.Sealed | System.Reflection.TypeAttributes.Abstract);
            t.SetCustomAttribute(ctor, data.Blob);
            var tmpType = t.CreateType();
            var aspect = tmpType.GetCustomAttributes(false)
                .OfType<OnMethodBoundaryAspect>()
                .Single();
            if (aspect == null)
                throw new InvalidOperationException("Could not find aspect on created type.");
            Type beingWoven = Type.GetType(data.TypeBeingWoven);
            if (beingWoven == null)
                throw new InvalidOperationException("Could not find type being woven: " + data.TypeBeingWoven);
            Type declaringMethod = Type.GetType(data.TypeDeclaringMethod);
            if (declaringMethod == null)
                throw new InvalidOperationException("Could not find type declaring the method: " + data.TypeDeclaringMethod);
            var m = declaringMethod.GetMethods(System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.FlattenHierarchy)
                .FirstOrDefault(md => md.MetadataToken == data.MethodToken);
            if (m == null)
                throw new InvalidOperationException($"Cannot find method on type {declaringMethod.FullName} with token {data.MethodToken}");

            AppDomain.CurrentDomain.SetData("result", aspect.CompileTimeValidate(beingWoven, m));
        }
    }
}
