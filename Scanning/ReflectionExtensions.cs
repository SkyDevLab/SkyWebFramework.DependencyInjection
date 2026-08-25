using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SkyWebFramework.DependencyInjection.Scanning
{
    /// <summary>
    /// Reflection helpers for safe assembly inspection and scanning.
    /// </summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Safely retrieves types from an assembly, gracefully handling <see cref="ReflectionTypeLoadException"/>.
        /// </summary>
        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly, Action<Exception>? onError = null)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                onError?.Invoke(ex);
                return ex.Types.Where(t => t != null)!;
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                return Enumerable.Empty<Type>();
            }
        }

        /// <summary>
        /// Determines if a type is a candidate for service registration (classes that are not abstract, compiler-generated, attributes, exceptions, delegates).
        /// </summary>
        public static bool IsServiceCandidate(this Type type)
        {
            if (type == null) return false;

            if (!type.IsClass || type.IsAbstract || type.IsInterface || type.IsValueType)
                return false;

            // Exclude compiler-generated types
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), false))
                return false;

            // Exclude attributes, exceptions, delegates
            if (typeof(Attribute).IsAssignableFrom(type) ||
                typeof(Exception).IsAssignableFrom(type) ||
                typeof(Delegate).IsAssignableFrom(type))
                return false;

            return true;
        }

        /// <summary>
        /// Determines if an interface is an infrastructure-only interface that should be ignored during convention matching.
        /// </summary>
        public static bool IsIgnoredInterface(this Type iface)
        {
            if (iface == null) return true;
            if (iface == typeof(IDisposable) || iface == typeof(IAsyncDisposable))
                return true;

            // Ignore standard system interfaces that shouldn't be convention-matched
            if (iface.FullName?.StartsWith("System.IComparable", StringComparison.Ordinal) == true ||
                iface.FullName?.StartsWith("System.IEquatable", StringComparison.Ordinal) == true ||
                iface.FullName?.StartsWith("System.IFormattable", StringComparison.Ordinal) == true ||
                iface.FullName?.StartsWith("System.ICloneable", StringComparison.Ordinal) == true)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if an open generic service type is assignable from an open generic implementation type.
        /// </summary>
        public static bool IsGenericTypeDefinitionAssignable(Type genericServiceDefinition, Type genericImplementationDefinition)
        {
            if (!genericServiceDefinition.IsGenericTypeDefinition || !genericImplementationDefinition.IsGenericTypeDefinition)
                return genericServiceDefinition.IsAssignableFrom(genericImplementationDefinition);

            if (genericServiceDefinition.IsInterface)
            {
                foreach (var iface in genericImplementationDefinition.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == genericServiceDefinition)
                    {
                        return true;
                    }
                }
            }
            else
            {
                var current = genericImplementationDefinition;
                while (current != null && current != typeof(object))
                {
                    if (current.IsGenericType && current.GetGenericTypeDefinition() == genericServiceDefinition)
                    {
                        return true;
                    }
                    current = current.BaseType;
                }
            }

            return false;
        }
    }
}
