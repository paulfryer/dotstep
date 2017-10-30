using System;
using System.Linq;
using System.Reflection;

namespace DotStep.Core
{
    public static class MethodExtensions {
        public static T GetAttributeValue<T, TAttributeType>(this object obj, Func<TAttributeType, T> func, T defaultValue) where TAttributeType : Attribute
        {
            var attribute = obj.GetType().GetTypeInfo().GetCustomAttributes<TAttributeType>().SingleOrDefault();
            var value = attribute != null ? func(attribute) : defaultValue;
            return value;
        }
    }


}
