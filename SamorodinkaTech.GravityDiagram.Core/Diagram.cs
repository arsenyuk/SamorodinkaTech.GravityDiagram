using System.Collections.ObjectModel;

namespace SamorodinkaTech.GravityDiagram.Core;

public sealed class Diagram
{
	private readonly List<RectNode> _nodes = new();
	private readonly List<Port> _ports = new();
	private readonly List<Arc> _arcs = new();

	public bool AutoDistributePorts { get; set; } = true;

	public void DistributeAllPortsProportionally()
	{
		foreach (var g in _ports.GroupBy(p => (p.Ref.NodeId, p.Ref.Side)))
		{
			DistributePortsProportionally(g.Key.NodeId, g.Key.Side);
		}
	}

	public ReadOnlyCollection<RectNode> Nodes => _nodes.AsReadOnly();
	public ReadOnlyCollection<Port> Ports => _ports.AsReadOnly();
	public ReadOnlyCollection<Arc> Arcs => _arcs.AsReadOnly();

	public RectNode AddNode(RectNode node)
	{
		ArgumentNullException.ThrowIfNull(node);
		_nodes.Add(node);
		return node;
	}

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

	public Arc AddArc(Arc arc)
	{
		ArgumentNullException.ThrowIfNull(arc);
		ValidateArc(arc);
		_arcs.Add(arc);
		return arc;
	}

	public RectNode? TryGetNode(DiagramId nodeId) => _nodes.FirstOrDefault(n => n.Id == nodeId);
	public Port? TryGetPort(DiagramId portId) => _ports.FirstOrDefault(p => p.Id == portId);

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
