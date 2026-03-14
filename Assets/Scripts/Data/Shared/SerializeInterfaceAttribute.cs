using UnityEngine;

namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Place on a [SerializeReference] field (single or list) to get an inspector type-picker
	/// that discovers all concrete implementations automatically.
	/// The interface/base type is inferred from the field type -- no parameters needed.
	/// </summary>
	public class SerializeInterfaceAttribute : PropertyAttribute { }
}
