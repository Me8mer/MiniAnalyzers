// This file provides a shim for the compiler-recognized type
// System.Runtime.CompilerServices.IsExternalInit.
//
// Why is this needed?
// - The C# compiler uses this type internally to support `record` types
//   and `init` accessors.
// - In .NET 5+, .NET Standard 2.1+, and .NET Core 3.1+, this type is
//   already defined in the framework.
// - But analyzer projects often target netstandard2.0 for compatibility.
//   That framework does not define IsExternalInit, so we provide it here.
//
// Safe to include: the compiler only cares that the type exists.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
