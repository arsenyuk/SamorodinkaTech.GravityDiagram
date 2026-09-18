using System.Collections.ObjectModel;
using System.Linq;

namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Контейнер диаграммы: хранит коллекции нод, портов и дуг.
/// Предоставляет методы добавления, поиска и валидации элементов.
/// </summary>
public sealed class Diagram
{
	private readonly List<RectNode> _nodes = new();
	private readonly List<Port> _ports = new();
	private readonly List<Arc> _arcs = new();

	/// <summary>
	/// Автоматически распределять порты по сторонам при добавлении.
	/// Если включено, все порты на одной стороне одной ноды распределяются равномерно.
	/// </summary>
	public bool AutoDistributePorts { get; set; } = true;

	/// <summary>
	/// Перераспределяет все порты пропорционально по сторонам нод.
	/// Каждая группа портов (нода + сторона) получает равномерное распределение.
	/// </summary>
	public void DistributeAllPortsProportionally()
	{
		foreach (var g in _ports.GroupBy(p => (p.Ref.NodeId, p.Ref.Side)))
		{
			DistributePortsProportionally(g.Key.NodeId, g.Key.Side);
		}
	}

	/// <summary>Только для чтения коллекция нод диаграммы.</summary>
	public ReadOnlyCollection<RectNode> Nodes => _nodes.AsReadOnly();
	/// <summary>Только для чтения коллекция портов диаграммы.</summary>
	public ReadOnlyCollection<Port> Ports => _ports.AsReadOnly();
	/// <summary>Только для чтения коллекция дуг диаграммы.</summary>
	public ReadOnlyCollection<Arc> Arcs => _arcs.AsReadOnly();

	/// <summary>Добавляет ноду в диаграмму и возвращает её.</summary>
	public RectNode AddNode(RectNode node)
	{
		ArgumentNullException.ThrowIfNull(node);
		_nodes.Add(node);
		return node;
	}

	/// <summary>
	/// Добавляет порт в диаграмму. При включённом AutoDistributePorts
	/// автоматически перераспределяет порты на стороне ноды.
	/// </summary>
	public Port AddPort(Port port)
	{
		ArgumentNullException.ThrowIfNull(port);
		_ports.Add(port);
		if (AutoDistributePorts)
		{
			DistributePortsProportionally(port.Ref.NodeId, port.Ref.Side);
		}
		return port;
	}

	/// <summary>
	/// Добавляет дугу в диаграмму с предварительной валидацией:
	/// проверяет существование портов и соответствие правилам потока данных.
	/// </summary>
	public Arc AddArc(Arc arc)
	{
		ArgumentNullException.ThrowIfNull(arc);
		ValidateArc(arc);
		_arcs.Add(arc);
		return arc;
	}

	/// <summary>Ищет ноду по идентификатору. Возвращает null, если не найдена.</summary>
	public RectNode? TryGetNode(DiagramId nodeId) => _nodes.FirstOrDefault(n => n.Id == nodeId);
	/// <summary>Ищет порт по идентификатору. Возвращает null, если не найден.</summary>
	public Port? TryGetPort(DiagramId portId) => _ports.FirstOrDefault(p => p.Id == portId);

	/// <summary>Сохраняет диаграмму в JSON-файл.</summary>
	public void SaveToFile(string path)
	{
		var arcsData = _arcs.Select(a => new {
			a.Id,
			a.FromPortId,
			a.ToPortId,
			a.Text,
			InternalPoints = a.InternalPoints.Select(p => new { p.X, p.Y }).ToList(),
			a.FixFirstPointToNormal,
			a.FixLastPointToNormal
		}).ToList();
		var diagramData = new {
			Nodes = _nodes,
			Ports = _ports,
			Arcs = arcsData
		};
		var json = System.Text.Json.JsonSerializer.Serialize(diagramData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
		System.IO.File.WriteAllText(path, json);
	}

	/// <summary>
	/// Равномерно распределяет порты на указанной стороне ноды.
	/// Порты сортируются по текущему смещению, затем им присваиваются
	/// равномерные смещения: 1/(n+1), 2/(n+1), ..., n/(n+1).
	/// </summary>
	private void DistributePortsProportionally(DiagramId nodeId, RectSide side)
	{
		var list = _ports
			.Where(p => p.Ref.NodeId == nodeId && p.Ref.Side == side)
			// Stable ordering: keep relative order by current offset, then by id.
			.OrderBy(p => p.Ref.Offset)
			.ThenBy(p => p.Id.Value, StringComparer.Ordinal)
			.ToList();
		if (list.Count == 0)
		{
			return;
		}

		// For a single port, always use the center of the side.
		if (list.Count == 1)
		{
			var p = list[0];
			p.Ref = p.Ref with { Offset = 0.5f };
			return;
		}

		// Even distribution with margins: 1/(n+1), 2/(n+1), ... n/(n+1)
		for (var i = 0; i < list.Count; i++)
		{
			var offset = (i + 1f) / (list.Count + 1f);
			var p = list[i];
			p.Ref = p.Ref with { Offset = offset };
		}
	}

	/// <summary>
	/// Проверяет, что порты дуги существуют, что ноды допускают
	/// исходящий/входящий поток данных через указанные стороны.
	/// </summary>
	private void ValidateArc(Arc arc)
	{
		var fromPort = TryGetPort(arc.FromPortId)
			?? throw new InvalidOperationException($"From-port '{arc.FromPortId}' not found.");
		var toPort = TryGetPort(arc.ToPortId)
			?? throw new InvalidOperationException($"To-port '{arc.ToPortId}' not found.");

		// Allow self-loops.
		if (fromPort.Ref.NodeId == toPort.Ref.NodeId)
		{
			return;
		}

		var fromNode = TryGetNode(fromPort.Ref.NodeId)
			?? throw new InvalidOperationException($"From-node '{fromPort.Ref.NodeId}' not found.");
		var toNode = TryGetNode(toPort.Ref.NodeId)
			?? throw new InvalidOperationException($"To-node '{toPort.Ref.NodeId}' not found.");

		var fromAllowed = fromNode.GetSideFlow(fromPort.Ref.Side);
		if (!fromAllowed.HasFlag(PortFlow.Outgoing))
		{
			throw new InvalidOperationException($"Arc '{arc.Id}': side '{fromPort.Ref.Side}' of node '{fromNode.Id}' forbids outgoing.");
		}

		var toAllowed = toNode.GetSideFlow(toPort.Ref.Side);
		if (!toAllowed.HasFlag(PortFlow.Incoming))
		{
			throw new InvalidOperationException($"Arc '{arc.Id}': side '{toPort.Ref.Side}' of node '{toNode.Id}' forbids incoming.");
		}
	}
}
