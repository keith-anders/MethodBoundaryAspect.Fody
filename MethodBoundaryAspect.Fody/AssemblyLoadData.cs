using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MethodBoundaryAspect.Fody
{
    [Serializable]
    public class AssemblyLoadData
    {
        public byte[] AssemblyContents { get; set; }
        public List<string> ReferencePaths { get; set; }

        public void LoadAssembly()
        {
            AppDomain.CurrentDomain.AssemblyResolve += UnweavedCodeRunner.CurrentDomain_AssemblyResolve;
            HashSet<string> loaded = new HashSet<string>();
            Assembly LoadAssembly(byte[] contents)
            {
                var asm = Assembly.Load(contents);
                foreach (var dependent in asm.GetReferencedAssemblies())
                    if (!loaded.Contains(dependent.FullName))
                    {
                        var referencePath = ReferencePaths.FirstOrDefault(p =>
                            new FileInfo(p).Name == dependent.Name + ".dll" ||
                            new FileInfo(p).Name == dependent.Name + ".exe");
                        loaded.Add(dependent.FullName);
                        if (referencePath == null)
                            Assembly.Load(dependent);
                        else
                            LoadAssembly(File.ReadAllBytes(referencePath));
                    }
                return asm;
            }
            LoadAssembly(AssemblyContents);
        }
    }
}
