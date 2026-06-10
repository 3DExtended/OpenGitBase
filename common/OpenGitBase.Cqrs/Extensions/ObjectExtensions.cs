// Reimplementation of NeverNull
// Taken from https://github.com/Bomret/NeverNull
// Licensed under MIT License by @bomret

using System.Diagnostics.CodeAnalysis;

namespace OpenGitBase.Cqrs.Extensions;

public static class ObjectExtensions
{
    public static void ThrowIfNull(this object obj, string name)
    {
        if (obj is null)
        {
            ThrowArgumentNullException(name);
        }
    }

    [DoesNotReturn]
    private static void ThrowArgumentNullException(string name) =>
        throw new ArgumentNullException(name);
}
