using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit.Sdk;

namespace SillyGeo.Tests
{
    public class TypeActivatorDataAttribute : DataAttribute
    {
        private readonly Type _type;

        public TypeActivatorDataAttribute(Type type)
        {
            _type = type;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            return new[] { new[] { Activator.CreateInstance(_type) } };
        }
    }
}
