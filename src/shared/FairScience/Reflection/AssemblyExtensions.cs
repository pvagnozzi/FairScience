using System.Reflection;

namespace FairScience.Reflection;

public static class AssemblyExtensions
{
    public static bool Implements(this Type type, Type interfaceType) =>
        type.GetInterfaces().Any(x => x == interfaceType);

    public static IEnumerable<Type> FindSubClasses(
	    this Assembly assembly,
	    Type baseType, bool concrete = true)
    {
	    var types = assembly.GetTypes().Where(x => x.IsSubclassOf(baseType));
        return concrete ? types.Where(x => !x.IsAbstract) : types;
    }

    public static IEnumerable<Type> FindImplementations(
	    this Assembly assembly,
	    Type baseType,
	    bool concrete = true)
    {
	    var types = assembly.GetTypes().Where(x => x.Implements(baseType));
	    return concrete ? types.Where(x => !x.IsAbstract) : types;
    }
        


}
