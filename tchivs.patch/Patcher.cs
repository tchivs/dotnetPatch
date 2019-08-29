using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace tchivs.patch
{
    public class Patcher
    {
        public readonly string FileName;
        public readonly ModuleDef Module;
        private readonly bool _keepOldMaxStack;

        public Patcher(string fileName)
        {
            this.FileName = fileName;
            Module = ModuleDefMD.Load(FileName);
        }

        public void PatchOffsets(PatchInfo patchInfo)
        {


            var method = FindMethod(patchInfo);
            var instructions = method.Body.Instructions;

            if (patchInfo.Indices != null && patchInfo.Instructions != null)
            {
                for (int i = 0; i < patchInfo.Indices.Length; i++)
                {
                    instructions[patchInfo.Indices[i]] = patchInfo.Instructions[i];
                }
            }
            else if (patchInfo.Index != -1 && patchInfo.Instruction != null)
            {
                instructions[patchInfo.Index] = patchInfo.Instruction;
            }
            else if (patchInfo.Index == -1)
            {
                throw new Exception("No index specified");
            }
            else if (patchInfo.Instruction == null)
            {
                throw new Exception("No instruction specified");
            }
            else if (patchInfo.Indices == null)
            {
                throw new Exception("No Indices specified");
            }
            else if (patchInfo.Instructions == null)
            {
                throw new Exception("No instructions specified");
            }
        }

        public void Patch(PatchInfo patchInfo)
        {
            if ((patchInfo.Indices != null || patchInfo.Index != -1) &&
                (patchInfo.Instruction != null || patchInfo.Instructions != null))
            {
                PatchOffsets(patchInfo);
            }
            else if ((patchInfo.Index == -1 && patchInfo.Indices == null) &&
                     (patchInfo.Instruction != null || patchInfo.Instructions != null))
            {
                PatchAndClear(patchInfo);
            }
            else
            {
                throw new Exception("Check your PatchInfo object for inconsistent assignments");
            }
        }

        public void Patch(PatchInfo[] patchInfos)
        {
            foreach (PatchInfo patchInfo in patchInfos)
            {
                if ((patchInfo.Indices != null || patchInfo.Index != -1) &&
                    (patchInfo.Instruction != null || patchInfo.Instructions != null))
                {
                    PatchOffsets(patchInfo);
                }
                else if ((patchInfo.Index == -1 && patchInfo.Indices == null) &&
                         (patchInfo.Instruction != null || patchInfo.Instructions != null))
                {
                    PatchAndClear(patchInfo);
                }
                else
                {
                    throw new Exception("Check your PatchInfo object for inconsistent assignments");
                }
            }
        }

        public Patcher(string fileName, bool keepOldMaxStack)
        {
            this.FileName = fileName;
            Module = ModuleDefMD.Load(FileName);
            _keepOldMaxStack = keepOldMaxStack;
        }

        public Patcher(Stream stream, bool keepOldMaxStack)
        {
            Module = ModuleDefMD.Load(stream);
            _keepOldMaxStack = keepOldMaxStack;
        }

        public Patcher(ModuleDefMD module, bool keepOldMaxStack)
        {
            Module = module;
            _keepOldMaxStack = keepOldMaxStack;
        }

        public Patcher(ModuleDef module, bool keepOldMaxStack)
        {
            Module = module;
            _keepOldMaxStack = keepOldMaxStack;
        }

        public MethodDef FindMethod(PatchInfo patchInfo)
        {
            TypeDef type = FindType(patchInfo);
            return FindMethod(type,  patchInfo.Method, patchInfo.Parameters, patchInfo.ReturnType);
        }

        public MethodDef FindMethod(TypeDef type, string methodName, string[] parameters, string returnType)
        {
            bool checkParams = parameters != null;
            foreach (var m in type.Methods)
            {
                bool isMethod = true;
                if (checkParams && parameters.Length != m.Parameters.Count) continue;
                if (methodName != m.Name) continue;
                if (!string.IsNullOrEmpty(returnType) && returnType != m.ReturnType.TypeName) continue;
                if (checkParams)
                {
                    if (m.Parameters.Where((param, i) => param.Type.TypeName != parameters[i]).Any())
                    {
                        isMethod = false;
                    }
                }

                if (isMethod) return m;
            }

            return null;
        }

        public TypeDef FindType(PatchInfo patchInfo)
        {
            return FindType(patchInfo.Namespace + "." + patchInfo.Class, patchInfo.NestedClasses);
            ;
        }

        public TypeDef FindType(string classPath, string[] nestedClasses)
        {
            if (classPath.First() == '.') classPath = classPath.Remove(0, 1);
            foreach (var module in Module.Assembly.Modules)
            {
                foreach (var type in Module.Types)
                {
                    if (type.FullName == classPath)
                    {
                        TypeDef t = null;
                        if (nestedClasses != null && nestedClasses.Length > 0)
                        {
                            foreach (var nc in nestedClasses)
                            {
                                if (t == null)
                                {
                                    if (!type.HasNestedTypes) continue;
                                    foreach (var typeN in type.NestedTypes)
                                    {
                                        if (typeN.Name == nc)
                                        {
                                            t = typeN;
                                        }
                                    }
                                }
                                else
                                {
                                    if (!t.HasNestedTypes) continue;
                                    foreach (var typeN in t.NestedTypes)
                                    {
                                        if (typeN.Name == nc)
                                        {
                                            t = typeN;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            t = type;
                        }

                        return t;
                    }
                }
            }

            return null;
        }

        public PatchInfo FixPatchInfo(PatchInfo patchInfo)
        {
            patchInfo.Indices = new int[] { };
            patchInfo.Index = -1;
            patchInfo.Instruction = null;
            return patchInfo;
        }

        /// <summary>
        /// /获取IL码
        /// </summary>
        /// <param name="patchInfo"></param>
        /// <returns></returns>
        public Instruction[] GetInstructions(PatchInfo patchInfo)
        {
            MethodDef method = FindMethod(patchInfo);
            ;
            return (Instruction[])method.Body.Instructions;
        }

        /// <summary>
        /// 清除方法体并应用破解
        /// </summary>
        /// <param name="patchInfo"></param>
        public void PatchAndClear(PatchInfo patchInfo)
        {


            var method = FindMethod(patchInfo);
            var instructions = method.Body.Instructions;
            instructions.Clear();
            if (patchInfo.Instructions != null)
            {
                for (int i = 0; i < patchInfo.Instructions.Length; i++)
                {
                    instructions.Insert(i, patchInfo.Instructions[i]);
                }
            }
            else
            {
                instructions.Insert(0, patchInfo.Instruction);
            }
        }

        public void PatchOperand(PatchInfo patchInfo, int operand)
        {
            MethodDef method = FindMethod(patchInfo);
            var instructions = method.Body.Instructions;
            if (patchInfo.Indices == null && patchInfo.Index != -1)
            {
                instructions[patchInfo.Index].Operand = operand;
            }
            else if (patchInfo.Indices != null && patchInfo.Index == -1)
            {
                foreach (var index in patchInfo.Indices)
                {
                    instructions[index].Operand = operand;
                }
            }
            else
            {
                throw new Exception("Operand error");
            }
        }

        public void PatchOperand(PatchInfo patchInfo, string operand)
        {
            MethodDef method = FindMethod(patchInfo);
            var instructions = method.Body.Instructions;
            if (patchInfo.Indices == null && patchInfo.Index != -1)
            {
                instructions[patchInfo.Index].Operand = operand;
            }
            else if (patchInfo.Indices != null && patchInfo.Index == -1)
            {
                foreach (var index in patchInfo.Indices)
                {
                    instructions[index].Operand = operand;
                }
            }
            else
            {
                throw new Exception("Operand error");
            }
        }

        public void PatchOperand(PatchInfo patchInfo, string[] operand)
        {
            MethodDef method = FindMethod(patchInfo);
            var instructions = method.Body.Instructions;
            if (patchInfo.Indices != null && patchInfo.Index == -1)
            {
                foreach (var index in patchInfo.Indices)
                {
                    instructions[index].Operand = operand[index];
                }
            }
            else
            {
                throw new Exception("Operand error");
            }
        }

        public void PatchOperand(PatchInfo patchInfo, int[] operand)
        {
            MethodDef method = FindMethod(patchInfo);
            var instructions = method.Body.Instructions;
            if (patchInfo.Indices != null && patchInfo.Index == -1)
            {
                foreach (var index in patchInfo.Indices)
                {
                    instructions[index].Operand = operand[index];
                }
            }
            else
            {
                throw new Exception("Operand error");
            }
        }

        public string GetOperand(PatchInfo patchInfo)
        {
            MethodDef method = FindMethod(patchInfo);
            return method.Body.Instructions[patchInfo.Index].Operand.ToString();
        }

        public int GetLdcI4Operand(PatchInfo patchInfo)
        {
            MethodDef method = FindMethod(patchInfo);
            return method.Body.Instructions[patchInfo.Index].GetLdcI4Value();
        }

        public int FindInstruction(PatchInfo patchInfo, Instruction instruction, int occurence)
        {
            occurence--; // Fix the occurence, e.g. second occurence must be 1 but hoomans like to write like they speak so why don't assist them?
            MethodDef method = FindMethod(patchInfo);
            var instructions = method.Body.Instructions;
            int index = 0;
            int occurenceCounter = 0;
            foreach (var i in instructions)
            {
                if (i.Operand == null && instruction.Operand == null)
                {
                    if (i.OpCode.Name == instruction.OpCode.Name && occurenceCounter < occurence)
                    {
                        occurenceCounter++;
                    }
                    else if (i.OpCode.Name == instruction.OpCode.Name && occurenceCounter == occurence)
                    {
                        return index;
                    }
                }
                else if (i.OpCode.Name == instruction.OpCode.Name &&
                         i.Operand.ToString() == instruction.Operand.ToString() && occurenceCounter < occurence)
                {
                    occurenceCounter++;
                }
                else if (i.OpCode.Name == instruction.OpCode.Name &&
                         i.Operand.ToString() == instruction.Operand.ToString() && occurenceCounter == occurence)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        public PropertyDef FindProperty(TypeDef type, string property)
        {
            return type.Properties.FirstOrDefault(prop => prop.Name == property);
        }

        public void RewriteProperty(PatchInfo patchInfo)
        {
            TypeDef type = FindType(patchInfo);
            PropertyDef property = FindProperty(type, patchInfo.Property);
            IList<Instruction> instructions = null;
            if (patchInfo.PropertyMethod == PropertyMethod.Get)
            {
                instructions = property.GetMethod.Body.Instructions;
            }
            else
            {
                instructions = property.SetMethod.Body.Instructions;
            }

            instructions.Clear();
            foreach (var instruction in patchInfo.Instructions)
            {
                instructions.Add(instruction);
            }
        }

        // See this: https://github.com/0xd4d/dnlib/blob/master/Examples/Example2.cs
        public void InjectMethod(PatchInfo patchInfo)
        {
            var type = FindType(patchInfo);
            type.Methods.Add(patchInfo.MethodDef);
            CilBody body = new CilBody();
            patchInfo.MethodDef.Body = body;
            if (patchInfo.ParameterDefs != null)
            {
                foreach (var param in patchInfo.ParameterDefs)
                {
                    patchInfo.MethodDef.ParamDefs.Add(param);
                }
            }

            if (patchInfo.Locals != null)
            {
                foreach (var local in patchInfo.Locals)
                {
                    body.Variables.Add(local);
                }
            }

            foreach (var il in patchInfo.Instructions)
            {
                body.Instructions.Add(il);
            }
        }

        public void AddCustomAttribute(PatchInfo patchInfo, CustomAttribute attribute)
        {
            TypeDef type = FindType(patchInfo);
            if (patchInfo.Method != null)
            {
                MethodDef method = FindMethod(patchInfo);
                method.CustomAttributes.Add(attribute);
            }
            else
            {
                type.CustomAttributes.Add(attribute);
            }
        }

        public void RemoveCustomAttribute(PatchInfo patchInfo, CustomAttribute attribute)
        {
            TypeDef type = FindType(patchInfo);
            if (patchInfo.Method != null)
            {
                MethodDef method = FindMethod(patchInfo);
                method.CustomAttributes.Remove(attribute);
            }
            else
            {
                type.CustomAttributes.Remove(attribute);
            }
        }

        public void RemoveCustomAttribute(PatchInfo patchInfo, int attributeIndex)
        {
            TypeDef type = FindType(patchInfo);
            if (patchInfo.Method != null)
            {
                MethodDef method = FindMethod(patchInfo);
                method.CustomAttributes.RemoveAt(attributeIndex);
            }
            else
            {
                type.CustomAttributes.RemoveAt(attributeIndex);
            }
        }

        public void ClearCustomAttributes(PatchInfo patchInfo)
        {
            TypeDef type = FindType(patchInfo);
            if (patchInfo.Method != null)
            {
                MethodDef method = FindMethod(patchInfo);
                method.CustomAttributes.Clear();
            }
            else
            {
                type.CustomAttributes.Clear();
            }
        }

        public void Save(string name)
        {
            if (_keepOldMaxStack)
                Module.Write(name,
                    new ModuleWriterOptions(Module) { MetadataOptions = { Flags = MetadataFlags.KeepOldMaxStack } });
            else
                Module.Write(name);
        }

        public void Save(bool backup)
        {
            if (string.IsNullOrEmpty(FileName))
            {
                throw new Exception(
                    "Assembly/module was loaded in memory, and no file was specified. Use Save(string) method to save the patched assembly.");
            }

            if (_keepOldMaxStack)
                Module.Write(FileName + ".tmp",
                    new ModuleWriterOptions(Module) { MetadataOptions = { Flags = MetadataFlags.KeepOldMaxStack } });
            else
                Module.Write(FileName + ".tmp");
            Module.Dispose();
            if (backup)
            {
                if (File.Exists(FileName + ".bak"))
                {
                    File.Delete(FileName + ".bak");
                }

                File.Move(FileName, FileName + ".bak");
            }
            else
            {
                File.Delete(FileName);
            }

            File.Move(FileName + ".tmp", FileName);
        }

        public PatchInfo[] FindInstructionsByOperand(string[] operand)
        {
            List<ObfuscatedPatchInfo> obfuscatedPatchInfos = new List<ObfuscatedPatchInfo>();
            List<string> operands = operand.ToList();
            foreach (var type in Module.Types)
            {
                if (!type.HasNestedTypes)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.Body != null)
                        {
                            List<int> indexList = new List<int>();
                            var obfuscatedPatchInfo = new ObfuscatedPatchInfo() { Type = type, Method = method };
                            int i = 0;
                            foreach (var instruction in method.Body.Instructions)
                            {
                                if (instruction.Operand != null)
                                {
                                    if (operands.Contains(instruction.Operand.ToString()))
                                    {
                                        indexList.Add(i);
                                        operands.Remove(instruction.Operand.ToString());
                                    }
                                }

                                i++;
                            }

                            if (indexList.Count == operand.Length)
                            {
                                obfuscatedPatchInfo.Indices = indexList;
                                obfuscatedPatchInfos.Add(obfuscatedPatchInfo);
                            }

                            operands = operand.ToList();
                        }
                    }
                }
                else
                {
                    var nestedTypes = type.NestedTypes;
                NestedWorker:
                    foreach (var nestedType in nestedTypes)
                    {
                        foreach (var method in type.Methods)
                        {
                            if (method.Body != null)
                            {
                                List<int> indexList = new List<int>();
                                var obfuscatedPatchInfo = new ObfuscatedPatchInfo() { Type = type, Method = method };
                                int i = 0;
                                obfuscatedPatchInfo.NestedTypes.Add(nestedType.Name);
                                foreach (var instruction in method.Body.Instructions)
                                {
                                    if (instruction.Operand != null)
                                    {
                                        if (operands.Contains(instruction.Operand.ToString()))
                                        {
                                            indexList.Add(i);
                                            operands.Remove(instruction.Operand.ToString());
                                        }
                                    }

                                    i++;
                                }

                                if (indexList.Count == operand.Length)
                                {
                                    obfuscatedPatchInfo.Indices = indexList;
                                    obfuscatedPatchInfos.Add(obfuscatedPatchInfo);
                                }

                                operands = operand.ToList();
                            }
                        }

                        if (nestedType.HasNestedTypes)
                        {
                            nestedTypes = nestedType.NestedTypes;
                            goto NestedWorker;
                        }
                    }
                }
            }

            List<PatchInfo> patchInfos = new List<PatchInfo>();
            foreach (var obfuscatedPatchInfo in obfuscatedPatchInfos)
            {
                PatchInfo t = new PatchInfo()
                {
                    Namespace = obfuscatedPatchInfo.Type.Namespace,
                    Class = obfuscatedPatchInfo.Type.Name,
                    Method = obfuscatedPatchInfo.Method.Name,
                    NestedClasses = obfuscatedPatchInfo.NestedTypes.ToArray()
                };
                if (obfuscatedPatchInfo.Indices.Count == 1)
                {
                    t.Index = obfuscatedPatchInfo.Indices[0];
                }
                else if (obfuscatedPatchInfo.Indices.Count > 1)
                {
                    t.Indices = obfuscatedPatchInfo.Indices.ToArray();
                }

                patchInfos.Add(t);
            }

            return patchInfos.ToArray();
        }

        public PatchInfo[] FindInstructionsByOperand(int[] operand)
        {
            List<ObfuscatedPatchInfo> obfuscatedPatchInfos = new List<ObfuscatedPatchInfo>();
            List<int> operands = operand.ToList();
            foreach (var type in Module.Types)
            {
                if (!type.HasNestedTypes)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.Body != null)
                        {
                            List<int> indexList = new List<int>();
                            var obfuscatedPatchInfo = new ObfuscatedPatchInfo() { Type = type, Method = method };
                            int i = 0;
                            foreach (var instruction in method.Body.Instructions)
                            {
                                if (instruction.Operand != null)
                                {
                                    if (operands.Contains(Convert.ToInt32(instruction.Operand.ToString())))
                                    {
                                        indexList.Add(i);
                                        operands.Remove(Convert.ToInt32(instruction.Operand.ToString()));
                                    }
                                }

                                i++;
                            }

                            if (indexList.Count == operand.Length)
                            {
                                obfuscatedPatchInfo.Indices = indexList;
                                obfuscatedPatchInfos.Add(obfuscatedPatchInfo);
                            }

                            operands = operand.ToList();
                        }
                    }
                }
                else
                {
                    var nestedTypes = type.NestedTypes;
                NestedWorker:
                    foreach (var nestedType in nestedTypes)
                    {
                        foreach (var method in type.Methods)
                        {
                            if (method.Body != null)
                            {
                                List<int> indexList = new List<int>();
                                var obfuscatedPatchInfo = new ObfuscatedPatchInfo() { Type = type, Method = method };
                                int i = 0;
                                obfuscatedPatchInfo.NestedTypes.Add(nestedType.Name);
                                foreach (var instruction in method.Body.Instructions)
                                {
                                    if (instruction.Operand != null)
                                    {
                                        if (operands.Contains(Convert.ToInt32(instruction.Operand.ToString())))
                                        {
                                            indexList.Add(i);
                                            operands.Remove(Convert.ToInt32(instruction.Operand.ToString()));
                                        }
                                    }

                                    i++;
                                }

                                if (indexList.Count == operand.Length)
                                {
                                    obfuscatedPatchInfo.Indices = indexList;
                                    obfuscatedPatchInfos.Add(obfuscatedPatchInfo);
                                }

                                operands = operand.ToList();
                            }
                        }

                        if (nestedType.HasNestedTypes)
                        {
                            nestedTypes = nestedType.NestedTypes;
                            goto NestedWorker;
                        }
                    }
                }
            }

            List<PatchInfo> patchInfos = new List<PatchInfo>();
            foreach (var obfuscatedPatchInfo in obfuscatedPatchInfos)
            {
                PatchInfo t = new PatchInfo()
                {
                    Namespace = obfuscatedPatchInfo.Type.Namespace,
                    Class = obfuscatedPatchInfo.Type.Name,
                    Method = obfuscatedPatchInfo.Method.Name,
                    NestedClasses = obfuscatedPatchInfo.NestedTypes.ToArray()
                };
                if (obfuscatedPatchInfo.Indices.Count == 1)
                {
                    t.Index = obfuscatedPatchInfo.Indices[0];
                }
                else if (obfuscatedPatchInfo.Indices.Count > 1)
                {
                    t.Indices = obfuscatedPatchInfo.Indices.ToArray();
                }

                patchInfos.Add(t);
            }

            return patchInfos.ToArray();
        }

        public PatchInfo[] FindInstructionsByOpcode(OpCode[] opcode)
        {
            List<ObfuscatedPatchInfo> obfuscatedPatchInfos = new List<ObfuscatedPatchInfo>();
            List<string> operands = opcode.Select(o => o.Name).ToList();
            foreach (var type in Module.Types)
            {
                if (!type.HasNestedTypes)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.Body != null)
                        {
                            List<int> indexList = new List<int>();
                            var obfuscatedPatchInfo = new ObfuscatedPatchInfo() { Type = type, Method = method };
                            int i = 0;
                            foreach (var instruction in method.Body.Instructions)
                            {
                                if (operands.Contains(instruction.OpCode.Name))
                                {
                                    indexList.Add(i);
                                    operands.Remove(instruction.OpCode.Name);
                                }

                                i++;
                            }

                            if (indexList.Count == opcode.Length)
                            {
                                obfuscatedPatchInfo.Indices = indexList;
                                obfuscatedPatchInfos.Add(obfuscatedPatchInfo);
                            }

                            operands = opcode.Select(o => o.Name).ToList();
                        }
                    }
                }
                else
                {
                    var nestedTypes = type.NestedTypes;
                NestedWorker:
                    foreach (var nestedType in nestedTypes)
                    {
                        foreach (var method in type.Methods)
                        {
                            if (method.Body != null)
                            {
                                List<int> indexList = new List<int>();
                                var obfuscatedPatchInfo = new ObfuscatedPatchInfo() { Type = type, Method = method };
                                int i = 0;
                                obfuscatedPatchInfo.NestedTypes.Add(nestedType.Name);
                                foreach (var instruction in method.Body.Instructions)
                                {
                                    if (operands.Contains(instruction.OpCode.Name))
                                    {
                                        indexList.Add(i);
                                        operands.Remove(instruction.OpCode.Name);
                                    }

                                    i++;
                                }

                                if (indexList.Count == opcode.Length)
                                {
                                    obfuscatedPatchInfo.Indices = indexList;
                                    obfuscatedPatchInfos.Add(obfuscatedPatchInfo);
                                }

                                operands = opcode.Select(o => o.Name).ToList();
                            }
                        }

                        if (nestedType.HasNestedTypes)
                        {
                            nestedTypes = nestedType.NestedTypes;
                            goto NestedWorker;
                        }
                    }
                }
            }

            List<PatchInfo> patchInfos = new List<PatchInfo>();
            foreach (var obfuscatedPatchInfo in obfuscatedPatchInfos)
            {
                PatchInfo t = new PatchInfo()
                {
                    Namespace = obfuscatedPatchInfo.Type.Namespace,
                    Class = obfuscatedPatchInfo.Type.Name,
                    Method = obfuscatedPatchInfo.Method.Name,
                    NestedClasses = obfuscatedPatchInfo.NestedTypes.ToArray()
                };
                if (obfuscatedPatchInfo.Indices.Count == 1)
                {
                    t.Index = obfuscatedPatchInfo.Indices[0];
                }
                else if (obfuscatedPatchInfo.Indices.Count > 1)
                {
                    t.Indices = obfuscatedPatchInfo.Indices.ToArray();
                }

                patchInfos.Add(t);
            }

            return patchInfos.ToArray();
        }

        public PatchInfo[] FindInstructionsByOperand(PatchInfo patchInfo, int[] operand, bool removeIfFound = false)
        {
            List<ObfuscatedPatchInfo> obfuscatedPatchInfos = new List<ObfuscatedPatchInfo>();
            List<int> operands = operand.ToList();
            TypeDef type = FindType(patchInfo);
            MethodDef m = null;
            if (patchInfo.Method != null)
                m = FindMethod(patchInfo);
            if (m != null)
            {
                List<int> indexList = new List<int>();
                var obfuscatedPatchInfo = new ObfuscatedPatchInfo() { Type = type, Method = m };
                int i = 0;
                foreach (var instruction in m.Body.Instructions)
                {
                    if (instruction.Operand != null)
                    {
                        if (operands.Contains(Convert.ToInt32(instruction.Operand.ToString())))
                        {
                            indexList.Add(i);
                            if (removeIfFound) operands.Remove(Convert.ToInt32(instruction.Operand.ToString()));
                        }
                    }

                    i++;
                }

                if (indexList.Count == operand.Length || removeIfFound == false)
                {
                    obfuscatedPatchInfo.Indices = indexList;
                    obfuscatedPatchInfos.Add(obfuscatedPatchInfo);
                }

                operands = operand.ToList();
            }
            else
            {
                foreach (var method in type.Methods)
                {
                    if (method.Body != null)
                    {
                        List<int> indexList = new List<int>();
                        var obfuscatedPatchInfo = new ObfuscatedPatchInfo() { Type = type, Method = method };
                        int i = 0;
                        foreach (var instruction in method.Body.Instructions)
                        {
                            if (instruction.Operand != null)
                            {
                                if (operands.Contains(Convert.ToInt32(instruction.Operand.ToString())))
                                {
                                    indexList.Add(i);
                                    if (removeIfFound) operands.Remove(Convert.ToInt32(instruction.Operand.ToString()));
                                }
                            }

                            i++;
                        }

                        if (indexList.Count == operand.Length || removeIfFound == false)
                        {
                            obfuscatedPatchInfo.Indices = indexList;
                            obfuscatedPatchInfos.Add(obfuscatedPatchInfo);
                        }

                        operands = operand.ToList();
                    }
                }
            }

            List<PatchInfo> patchInfos = new List<PatchInfo>();
            foreach (var obfuscatedPatchInfo in obfuscatedPatchInfos)
            {
                PatchInfo t = new PatchInfo()
                {
                    Namespace = obfuscatedPatchInfo.Type.Namespace,
                    Class = obfuscatedPatchInfo.Type.Name,
                    Method = obfuscatedPatchInfo.Method.Name,
                    NestedClasses = obfuscatedPatchInfo.NestedTypes.ToArray()
                };
                if (obfuscatedPatchInfo.Indices.Count == 1)
                {
                    t.Index = obfuscatedPatchInfo.Indices[0];
                }
                else if (obfuscatedPatchInfo.Indices.Count > 1)
                {
                    t.Indices = obfuscatedPatchInfo.Indices.ToArray();
                }

                patchInfos.Add(t);
            }

            return patchInfos.ToArray();
        }

        /// <summary>
        /// Find methods that contain a certain OpCode[] signature
        /// </summary>
        /// <returns></returns>
        public PatchInfo[] FindMethodsByOpCodeSignature(OpCode[] signature)
        {
            HashSet<MethodDef> found = new HashSet<MethodDef>();

            foreach (TypeDef td in Module.Types)
            {
                foreach (MethodDef md in td.Methods)
                {
                    if (md.HasBody)
                    {
                        if (md.Body.HasInstructions)
                        {
                            OpCode[] codes = md.Body.Instructions.GetOpCodes().ToArray();
                            if (codes.IndexOf<OpCode>(signature).Any())
                            {
                                found.Add(md);
                            }
                        }
                    }
                }
            }

            //cast each to PatchInfo
            return (from method in found select (PatchInfo)method).ToArray();
        }

        public PatchInfo[] FindInstructionsByOpcode(PatchInfo patchInfo, OpCode[] opcode, bool removeIfFound = false)
        {
            List<ObfuscatedPatchInfo> obfuscatedPatchInfos = new List<ObfuscatedPatchInfo>();
            List<string> operands = opcode.Select(o => o.Name).ToList();
            TypeDef type = FindType(patchInfo);
            MethodDef m = null;
            if (patchInfo.Method != null)
                m = FindMethod(patchInfo);
            if (m != null)
            {
                List<int> indexList = new List<int>();
                var obfuscatedPatchInfo = new ObfuscatedPatchInfo() { Type = type, Method = m };
                int i = 0;
                foreach (var instruction in m.Body.Instructions)
                {
                    if (operands.Contains(instruction.OpCode.Name))
                    {
                        indexList.Add(i);
                        if (removeIfFound) operands.Remove(instruction.OpCode.Name);
                    }

                    i++;
                }

                if (indexList.Count == opcode.Length || removeIfFound == false)
                {
                    obfuscatedPatchInfo.Indices = indexList;
                    obfuscatedPatchInfos.Add(obfuscatedPatchInfo);
                }
            }
            else
            {
                foreach (var method in type.Methods)
                {
                    if (method.Body != null)
                    {
                        List<int> indexList = new List<int>();
                        var obfuscatedPatchInfo = new ObfuscatedPatchInfo() { Type = type, Method = method };
                        int i = 0;
                        foreach (var instruction in method.Body.Instructions)
                        {
                            if (operands.Contains(instruction.OpCode.Name))
                            {
                                indexList.Add(i);
                                if (removeIfFound) operands.Remove(instruction.OpCode.Name);
                            }

                            i++;
                        }

                        if (indexList.Count == opcode.Length || removeIfFound == false)
                        {
                            obfuscatedPatchInfo.Indices = indexList;
                            obfuscatedPatchInfos.Add(obfuscatedPatchInfo);
                        }

                        operands = opcode.Select(o => o.Name).ToList();
                    }
                }
            }

            List<PatchInfo> patchInfos = new List<PatchInfo>();
            foreach (var obfuscatedPatchInfo in obfuscatedPatchInfos)
            {
                var t = new PatchInfo()
                {
                    Namespace = obfuscatedPatchInfo.Type.Namespace,
                    Class = obfuscatedPatchInfo.Type.Name,
                    Method = obfuscatedPatchInfo.Method.Name,
                    NestedClasses = obfuscatedPatchInfo.NestedTypes.ToArray()
                };
                if (obfuscatedPatchInfo.Indices.Count == 1)
                {
                    t.Index = obfuscatedPatchInfo.Indices[0];
                }
                else if (obfuscatedPatchInfo.Indices.Count > 1)
                {
                    t.Indices = obfuscatedPatchInfo.Indices.ToArray();
                }

                patchInfos.Add(t);
            }

            return patchInfos.ToArray();
        }

        public PatchInfo[] FindInstructionsByRegex(PatchInfo patchInfo, string pattern, bool ignoreOperand)
        {
            var patchInfos = new List<PatchInfo>();
            if (patchInfo.Namespace != null)
            {
                var type = FindType(patchInfo);
                if (patchInfo.Method != null)
                {
                    string body = "";
                    var method = FindMethod(patchInfo);
                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (!ignoreOperand)
                        {
                            body += instruction.OpCode + " " + instruction.Operand + "\n";
                        }
                        else
                        {
                            body += instruction.OpCode + "\n";
                        }
                    }

                    foreach (Match match in Regex.Matches(body, pattern))
                    {
                        int startIndex = body.Split(new string[] { match.Value }, StringSplitOptions.None)[0]
                                             .Split('\n')
                                             .Length - 1;
                        int[] indices = { };
                        for (int i = 0; i < match.Value.Split('\n').Length; i++)
                        {
                            indices[i] = startIndex + i;
                        }

                        var t = new PatchInfo()
                        {
                            Indices = indices,
                            Method = patchInfo.Method,
                            Class = patchInfo.Class,
                            Namespace = patchInfo.Namespace,
                            NestedClasses = patchInfo.NestedClasses,
                            NestedClass = patchInfo.NestedClass
                        };
                        patchInfos.Add(t);
                    }
                }
            }

            return patchInfos.ToArray();
        }

        private bool CheckParametersByType(ParameterInfo[] parameters, Type[] types)
        {
            return !parameters.Where((t, i) => types[i] != t.ParameterType).Any();
        }

        public IMethod BuildCall(Type type, string method, Type returnType, Type[] parameters)
        {
            Importer importer = new Importer(Module);
            foreach (var m in type.GetMethods())
            {
                if (m.Name == method && m.ReturnType == returnType && m.GetParameters().Length == parameters.Length &&
                    CheckParametersByType(m.GetParameters(), parameters))
                {
                    IMethod meth = importer.Import(m);
                    return meth;
                }
            }

            return null;
        }

        public void ReplaceInstruction(PatchInfo patchInfo)
        {


            var method = FindMethod(patchInfo);
            var instructions = method.Body.Instructions;
            if (patchInfo.Index != -1 && patchInfo.Instruction != null)
            {
                instructions[patchInfo.Index] = patchInfo.Instruction;
            }
            else if (patchInfo.Indices != null && patchInfo.Instructions != null)
            {
                for (int i = 0; i < patchInfo.Indices.Length; i++)
                {
                    var index = patchInfo.Indices[i];
                    instructions[index] = patchInfo.Instructions[i];
                }
            }
            else
            {
                throw new Exception("PatchInfo object built wrong");
            }
        }

        public void RemoveInstruction(PatchInfo patchInfo)
        {

            var method = FindMethod(patchInfo);
            var instructions = method.Body.Instructions;
            if (patchInfo.Index != -1 && patchInfo.Indices == null)
            {
                instructions.RemoveAt(patchInfo.Index);
            }
            else if (patchInfo.Index == -1 && patchInfo.Indices != null)
            {
                foreach (var index in patchInfo.Indices.OrderByDescending(v => v))
                {
                    instructions.RemoveAt(index);
                }
            }
            else
            {
                throw new Exception("PatchInfo object built wrong");
            }
        }

        /// <summary>
        /// 写入BOOL值作为方法返回体
        /// </summary>
        /// <param name="PatchInfo"></param>
        /// <param name="trueOrFalse"></param>
        public void WriteReturnBody(PatchInfo PatchInfo, bool trueOrFalse)
        {
            PatchInfo = FixPatchInfo(PatchInfo);
            if (trueOrFalse)
            {
                PatchInfo.Instructions = new Instruction[]
                {
                    Instruction.Create(OpCodes.Ldc_I4_1), Instruction.Create(OpCodes.Ret)
                };
            }
            else
            {
                PatchInfo.Instructions = new Instruction[]
                {
                    Instruction.Create(OpCodes.Ldc_I4_0), Instruction.Create(OpCodes.Ret)
                };
            }

            PatchAndClear(PatchInfo);
        }

        public void WriteEmptyBody(PatchInfo patchInfo)
        {
            patchInfo = this.FixPatchInfo(patchInfo);
            patchInfo.Instruction = Instruction.Create(OpCodes.Ret);
            PatchAndClear(patchInfo);
        }
    }
}