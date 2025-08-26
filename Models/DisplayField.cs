using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClinicalApplications.Models
{
    public sealed class DisplayField
    {
        public string Label { get; set; } = ""; 
        public string Value { get; set; } = ""; 
        public string Tooltip { get; set; } = ""; 
    }
}
