using System;

namespace DotStep.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FunctionMemory : Attribute {
        public int Memory { get; set; }
        
    }

}
