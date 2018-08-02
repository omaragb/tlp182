using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oneproject
{
    class DVariableComparer : IEqualityComparer<DVariable>
    {
        public bool Equals(DVariable x, DVariable y)
        {
            return x.name.CompareTo(y.name) == 0;
        }

        public int GetHashCode(DVariable obj)
        {
            return obj.name.GetHashCode();
        }
    }
}
