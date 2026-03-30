using System;
using UnityEngine;

namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Use with a <see cref="ScriptableObject"/> reference field. The inspector draws an object field
	/// and rejects assignments whose runtime type does not implement <paramref name="interfaceType"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class ScriptableObjectImplementsAttribute : PropertyAttribute
	{
		public Type InterfaceType { get; }

		public ScriptableObjectImplementsAttribute(Type interfaceType)
		{
			if (interfaceType == null || !interfaceType.IsInterface)
				throw new ArgumentException("Must be a non-null interface type.", nameof(interfaceType));
			InterfaceType = interfaceType;
		}
	}
}
