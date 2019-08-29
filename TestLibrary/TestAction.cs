using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestLibrary
{
    public class TestAction
    {
        public bool Check(string pwd)
        {
            if (pwd=="123")
            {
                return true;
            }

            return false;
        }
    }
}
