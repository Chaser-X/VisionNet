using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Guards the SharpGL -> OpenTK migration at the assembly/API boundary: the library must not
    /// reference SharpGL any more, and no public member may expose a SharpGL type.
    /// </summary>
    public class OpenTkMigrationTests
    {
        private static Assembly CxControlAssembly => typeof(CxDisplay).Assembly;

        [Fact]
        public void Assembly_DoesNotReferenceSharpGL()
        {
            var sharpGlRefs = CxControlAssembly.GetReferencedAssemblies()
                .Where(a => a.Name.StartsWith("SharpGL", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.FullName)
                .ToArray();

            Assert.Empty(sharpGlRefs);
        }

        [Fact]
        public void Assembly_ReferencesOpenTK()
        {
            var names = CxControlAssembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToArray();

            Assert.Contains("OpenTK", names);
            Assert.Contains("OpenTK.GLControl", names);
        }

        [Fact]
        public void CxDisplay_DerivesFromOpenTkGlControl()
        {
            Assert.Equal("OpenTK.GLControl", typeof(CxDisplay).BaseType?.FullName);
        }

        [Fact]
        public void PublicApi_DoesNotExposeSharpGLTypes()
        {
            var offenders = new List<string>();

            foreach (var type in CxControlAssembly.GetExportedTypes())
            {
                // Only members declared by this assembly are under our control; inherited members
                // (e.g. from the OpenTK base control) are out of scope.
                const BindingFlags declared =
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

                foreach (var method in type.GetMethods(declared))
                {
                    Check(method.ReturnType, type, method.Name, offenders);
                    foreach (var p in method.GetParameters())
                        Check(p.ParameterType, type, method.Name, offenders);
                }

                foreach (var ctor in type.GetConstructors(declared))
                    foreach (var p in ctor.GetParameters())
                        Check(p.ParameterType, type, ".ctor", offenders);

                foreach (var property in type.GetProperties(declared))
                    Check(property.PropertyType, type, property.Name, offenders);

                foreach (var field in type.GetFields(declared))
                    Check(field.FieldType, type, field.Name, offenders);

                foreach (var evt in type.GetEvents(declared))
                    Check(evt.EventHandlerType, type, evt.Name, offenders);
            }

            Assert.True(offenders.Count == 0,
                "Public API still exposes SharpGL types:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }

        private static void Check(Type exposed, Type owner, string member, List<string> offenders)
        {
            if (exposed == null) return;

            var names = new List<Type>();
            if (exposed.IsByRef) exposed = exposed.GetElementType();
            if (exposed.IsArray) exposed = exposed.GetElementType();
            if (exposed.IsGenericType) names.AddRange(exposed.GetGenericArguments());
            names.Add(exposed);

            foreach (var t in names)
            {
                var ns = t.Namespace ?? string.Empty;
                if (ns.StartsWith("SharpGL", StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{owner.FullName}.{member} -> {t.FullName}");
            }
        }
    }
}
