using System;

namespace DotStep.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExplicitRole : Attribute {
        public string RoleName { get; set; }
    }

}
