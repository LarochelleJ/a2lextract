using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace a2lextract {
    internal class ComputeMethod {
        public string unit { get; }
        public string offset { get; }
        public string factor { get; }
        public ComputeMethod(string unit, string[] coeffs) {
            this.unit = unit;
            offset = coeffs[3];
            factor = coeffs[6];
        }
    }
}
