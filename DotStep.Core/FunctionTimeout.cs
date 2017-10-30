using System;

namespace DotStep.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FunctionTimeout : Attribute
    {
        public int Timeout { get; set; }
    }

}
