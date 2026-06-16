using System;
using System.Text.RegularExpressions;

public static class ReflectionExtensions
{
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
}
