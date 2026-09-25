// Polyfill: enables C# 9 records / init setters on netstandard2.1 (also inside Unity).
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
