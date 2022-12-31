using System.Reflection;

namespace FairScience.Reflection;

public static class AssemblyExtensions
{
    public static bool Implements(this Type type, Type interfaceType) =>
        type.GetInterfaces().Any(x => x == interfaceType);

    public static IEnumerable<Type> FindSubClasses(
        this Assembly assembly,
        Type baseType) =>
        assembly.GetTypes().Where(x => x.IsSubclassOf(baseType));

    public static IEnumerable<Type> FindImplementations(
        this Assembly assembly,
        Type baseType) =>
        assembly.GetTypes().Where(x => x.Implements(baseType));


}
