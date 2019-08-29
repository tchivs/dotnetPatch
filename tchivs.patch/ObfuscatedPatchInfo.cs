using System.Collections.Generic;
using dnlib.DotNet;

namespace tchivs.patch
{
    public class ObfuscatedPatchInfo
    {
        public TypeDef Type { get; set; }
        public MethodDef Method { get; set; }
        public List<int> Indices { get; set; }
        public List<string> NestedTypes = new List<string>();
    }
}