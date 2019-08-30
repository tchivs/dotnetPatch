using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using dnlib.DotNet.Resources;
using dnlib.W32Resources;
using tchivs.patch;
using OpCodes = dnlib.DotNet.Emit.OpCodes;

namespace ComponentOneCrack
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            this.textBox1.Text = @"D:\Program Files (x86)\ComponentOne\WinForms Edition\bin\v4.5.2\C1.Win.C1List.4.5.2.dll";
            this.textBox3.Text = @"C:\Users\tchivs\Source\Repos\tchivs\dotnetPatch\TestLibrary\bin\Debug\TestLibrary.dll";
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            string file = this.textBox1.Text;
            Patcher patcher = new Patcher(file);
            var info = new PatchInfo()
            {
                Class = "ProviderInfo",
                Namespace = "C1.Util.Licensing",
                Method = "ValidateDesigntime",
                Index = 6,
                Instruction = Instruction.CreateLdcI4(0)
            };
            //模糊破解
            var type = patcher.FindType(info);
            int i = 0;
            foreach (MethodDef method in type.Methods)
            {
                if (method.Body.Instructions.Count == 143 + 1)
                {

                    method.Body.Instructions[43] = Instruction.CreateLdcI4(0);
                    Console.WriteLine("ValidateRuntime Crack Success");
                    i++;
                }
                else if (method.Body.Instructions.Count == 28 + 1)
                {
                    method.Body.Instructions[6] = Instruction.CreateLdcI4(0);
                    Console.WriteLine("ValidateDesigntime Crack Success");
                    i++;
                }
                if (i == 2)
                {
                    break;

                }
                Console.WriteLine(method.Name);

            }
            if (i == 2)
            {
                Console.WriteLine("破解成功!");
                patcher.Save(Path.GetFileName(patcher.FileName));

            }
            //var info = new PatchInfo()
            //{
            //    Class = "ProviderInfo",
            //    Namespace = "C1.Util.Licensing",
            //    Method = "ValidateDesigntime",
            //    Index = 6,
            //    Instruction =  Instruction.CreateLdcI4(0)

            //};
            //var info2 = new PatchInfo()
            //{
            //    Class = "ProviderInfo",
            //    Namespace = "C1.Util.Licensing",
            //    Method = "ValidateRuntime",
            //    Index = 43,
            //    Instruction = Instruction.CreateLdcI4(0)

            //};
            //var method = patcher.FindMethod(info);
            //if (method!=null)
            //{
            //    MessageBox.Show("Find!");
            //    patcher.PatchOffsets(info);
            //    patcher.PatchOffsets(info2);
            //    patcher.Save("C1.C1Word.4.5.2_Crack.dll");
            //}

        }

        private void Button2_Click(object sender, EventArgs e)
        {
            //打开.NET程序集/模块
            ModuleDefMD module = ModuleDefMD.Load(this.textBox1.Text);

            RidList typeDef = module.Metadata.GetTypeDefRidList();
            for (int i = 0; i < module.Types.Count; i++)
            {
                var t = module.Types[i];
                if (t.Name == "TestAction")
                {
                    for (int j = 0; j < t.Methods.Count; j++)
                    {
                        if (t.Methods[j].Name == "Check")
                        {
                            var method = t.Methods[j];

                            Instruction[] opCodes = {
                                   Instruction.Create(OpCodes.Ldc_I4_1),
                                   Instruction.Create(OpCodes.Ret)
                               };
                            for (int l = 0; l < opCodes.Length; l++)
                            {
                                method.Body.Instructions[l] = opCodes[l];
                            }
                        }
                    }
                }
            }

            //保存程序集
            module.Write(@"C:\Users\tchivs\source\repos\ComponentOneCrack\TestLibrary\bin\Debug\TestLibraryCrack.dll", new ModuleWriterOptions(module)
            {
                MetadataOptions = { Flags = MetadataFlags.KeepOldMaxStack }
            });
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            string file = this.textBox3.Text;
            Patcher patcher = new Patcher(file);
            var res = patcher.Module.Resources.FindEmbeddedResource("app.resources").CreateReader();
            
            ResourceElement element = res.ResourceElements.First(s => s.Name == resxName);
            MemoryStream reStream = new MemoryStream();
            res.CreateReader().CopyTo(reStream);



          var rsA=  Assembly.Load(reStream.ToArray());

            PatchInfo patchInfo = new PatchInfo()
            {
                Namespace = "TestLibrary",
                Class = "TestAction",
                Method = "Check",
            };
            // var methodDef = patcher.FindMethod(patchInfo);
            patcher.WriteReturnBody(patchInfo, true);
            AssemblyDef assemblyDef = patcher.Module.Assembly;
            MemoryStream memoryStream = new MemoryStream();
            patcher.Module.Write(memoryStream, new ModuleWriterOptions(patcher.Module) { MetadataOptions = { Flags = MetadataFlags.KeepOldMaxStack } });

            Assembly assembly = Assembly.Load(memoryStream.ToArray());

            Type type = assembly.GetType("TestLibrary.TestAction");
            MethodInfo method = type.GetMethod("Check");
            var ins = Activator.CreateInstance(type);
            var parameters = new object[] { this.textBox2.Text };
            bool result = (bool)method.Invoke(ins, parameters);
            MessageBox.Show(result.ToString());
        }

        private void Button4_Click(object sender, EventArgs e)
        {
            string file = this.textBox3.Text;
            Assembly assembly = Assembly.LoadFile(file);

            Type type = assembly.GetType("TestLibrary.TestAction");
            MethodInfo method = type.GetMethod("Check");
            var ins = Activator.CreateInstance(type);
            var parameters = new object[] { this.textBox2.Text };
            bool result = (bool)method.Invoke(ins, parameters);
            MessageBox.Show(result.ToString());
        }
    }
}