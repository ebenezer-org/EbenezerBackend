namespace EbenezerBackend.Shared.CustomAttributes;

[AttributeUsage(AttributeTargets.Class)]
public class CollectionNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}