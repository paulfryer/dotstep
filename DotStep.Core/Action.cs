using System;

namespace DotStep.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class Action : Attribute {
        public string ActionName { get; set; }
    }

}
