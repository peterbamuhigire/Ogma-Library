namespace OgmaLibrary.Application.Extensions;

/// <summary>
/// Marks a type as a stable extension surface that Phase 23's Extension SDK will
/// review before making public.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
public sealed class ExtensionPointAttribute : Attribute
{
}
