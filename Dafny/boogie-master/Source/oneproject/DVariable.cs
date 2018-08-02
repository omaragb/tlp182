using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oneproject
{
    class DVariable
    {
        public string name;
        Microsoft.Dafny.Type type;

        public DVariable(string name, Microsoft.Dafny.Type type) 
        {
            this.name = name;
            this.type = type;
        }

        public Microsoft.Dafny.Formal ToFormal()
        {
            return new Microsoft.Dafny.Formal(null, name, type, false, false);
        }

        public override string ToString()
        {
            return name+" "+type;
        }


    }

}
