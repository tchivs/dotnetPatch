using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace tchivs.patch
{
    #region Enum

    public enum PropertyMethod
    {
        Get,
        Set
    }

    #endregion Enum

    public class PatchInfo
    {
        #region Prop

        public string Namespace { get; set; }
        public string Class { get; set; }
        public string Method { get; set; }

        #region 单

        public string NestedClass { get; set; }
        public int Index { get; set; } = -1;
        public Instruction Instruction { get; set; }
        public string ReturnType { get; set; } // String[] etc.. if null or empty it means that you dont want to check it
        public string Property { get; set; }
        public PropertyMethod PropertyMethod { get; set; }
        public MethodDef MethodDef { get; set; }

        #endregion 单

        #region 多

        public ParamDef[] ParameterDefs { get; set; }
        private string[] _nestedClasses;

        public string[] NestedClasses
        {
            get
            {
                if (this._nestedClasses == null&& NestedClass!=null)
                {
                    _nestedClasses = new[] { NestedClass };
                }

                return _nestedClasses;
            }
            set => _nestedClasses = value;
        }

        public int[] Indices { get; set; }
        public Instruction[] Instructions { get; set; }
        public string[] Parameters { get; set; } // String[] etc.. if null it means that you dont want to check it
        public Local[] Locals { get; set; }

        #endregion 多

        #endregion Prop

        public PatchInfo()
        {
        }

        /// <summary>
        /// Cast MethodDef to Target -> (Target)MethodDef
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator PatchInfo(MethodDef value)
        {
            return new PatchInfo(value);
        }

        public PatchInfo(MethodDef method)
        {
            Namespace = method.DeclaringType.Namespace;
            Class = method.DeclaringType.Name;
            Method = method.Name;
        }
    }
}