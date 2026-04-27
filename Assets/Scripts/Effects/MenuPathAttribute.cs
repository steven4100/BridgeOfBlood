using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MenuPathAttribute : Attribute
{
	public string Path { get; }
	public MenuPathAttribute(string path) => Path = path;
}
