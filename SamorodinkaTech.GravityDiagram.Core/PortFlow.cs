namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Направление потока данных через порт. Используется как flags-enum,
/// поэтому порт может одновременно принимать и отдавать связи.
/// </summary>
[Flags]
public enum PortFlow
{
	/// <summary>Порт не участвует ни в каких связях.</summary>
	None = 0,

	/// <summary>Порт принимает входящие дуги (приёмник).</summary>
	Incoming = 1,

	/// <summary>Порт порождает исходящие дуги (источник).</summary>
	Outgoing = 2,

	/// <summary>Порт одновременно принимает и порождает дуги.</summary>
	Both = Incoming | Outgoing,
}
