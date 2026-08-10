using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public static class ReflectionExtensions
{
    private static Dictionary<Type,Type[]> InterfaceClasses = new Dictionary<Type, Type[]>();
    public static Type[] GetAllInterfaceClasses(this Type type)
    {
        if (InterfaceClasses.ContainsKey(type))
            return InterfaceClasses[type];
        return InterfaceClasses[type] = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(ass => ass.GetTypes()).Where(t => t.IsClass && t.GetInterfaces().Contains(type)).ToArray();
    }
    
    private static Dictionary<Type,Type[]> ParentTypes = new Dictionary<Type, Type[]>();

    public static Type[] GetParentTypes(this Type type)
    {
        if (ParentTypes.ContainsKey(type))
            return ParentTypes[type];
        return ParentTypes[type] = type.GetParents().ToArray();
    }
    
    private static IEnumerable<Type> GetParents(this Type type)
    {
        // is there any base type?
        if (type == null)
        {
            yield break;
        }

        yield return type;

        // return all implemented or inherited interfaces
        foreach (var i in type.GetInterfaces())
        {
            yield return i;
        }

        // return all inherited types
        var currentBaseType = type.BaseType;
        while (currentBaseType != null)
        {
            yield return currentBaseType;
            currentBaseType= currentBaseType.BaseType;
        }
    }
	
    private static Dictionary<Type,Type[]> ChildClasses = new Dictionary<Type, Type[]>();
    public static Type[] GetAllChildClasses(this Type type)
    {
        if (ChildClasses.ContainsKey(type))
            return ChildClasses[type];
        return ChildClasses[type] = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(ass => ass.GetTypes()).Where(type.IsAssignableFrom).ToArray();
    }
	
    private static Dictionary<Type,Type[]> GenericChildClasses = new Dictionary<Type, Type[]>();
    public static Type[] GetAllGenericChildClasses(this Type genericType)
    {
        if (GenericChildClasses.ContainsKey(genericType))
            return ChildClasses[genericType];
        return ChildClasses[genericType] = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(ass => ass.GetTypes()).Where(type=>type.IsAssignableToGenericType(genericType)).ToArray();
    }
    
    public static bool IsAssignableToGenericType(this Type givenType, Type genericType)
    {
        var interfaceTypes = givenType.GetInterfaces();

        foreach (var it in interfaceTypes)
        {
            if (it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
                return true;
        }

        if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
            return true;

        Type baseType = givenType.BaseType;
        if (baseType == null) return false;

        return IsAssignableToGenericType(baseType, genericType);
    }
	
    public static string SplitCamelCase( this string str )
    {
        return Regex.Replace( 
            Regex.Replace( 
                str, 
                @"(\P{Ll})(\P{Ll}\p{Ll})", 
                "$1 $2" 
            ), 
            @"(\p{Ll})(\P{Ll})", 
            "$1 $2" 
        );
    }
    
    public static string FormatTypeName(this string typeName)
    {
        return (typeName.EndsWith("Data", StringComparison.InvariantCultureIgnoreCase)
            ? typeName.Substring(0, typeName.Length - 4)
            : typeName).SplitCamelCase();
    }
    
    public static string GetFullName(this Type t)
    {
        if (!t.IsGenericType)
            return t.Name.FormatTypeName();

        var name = t.Name.FormatTypeName();
        return
            $"{name.Substring(0, name.LastIndexOf("`"))}{t.GetGenericArguments().Aggregate("<", (aggregate, type) => $"{aggregate}{(aggregate == "<" ? "" : ",")}{GetFullName(type)}")}>";
    }
}