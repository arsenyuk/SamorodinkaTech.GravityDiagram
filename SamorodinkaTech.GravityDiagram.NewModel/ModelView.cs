using System;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace SamorodinkaTech.GravityDiagram.NewModel;

public sealed class ModelView : Control
{
    public PhysicsModel Model { get; } = new();

    private readonly DispatcherTimer _timer;
    private DateTime _lastTickAt;
    private bool _firstSize = true;

    private const float SimSpeed = 60f;
    private const float MaxSubstepDt = 1f / 60f;
    private const int MaxSubstepsPerTick = 240;

    // Drag state
    private PhysicsNode? _dragNode;
    private Vector2 _dragOffset;

    private static readonly Brush NodeFill = new SolidColorBrush(Color.Parse("#4A90D9"));
    private static readonly Brush NodeStroke = new SolidColorBrush(Color.Parse("#2C5F8A"));
    private static readonly Pen NodePen = new(NodeStroke, 2);

    private static FormattedText MakeText(string text, double fontSize, IBrush brush)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            fontSize,
            brush);
    }

    public ModelView()
    {
        ClipToBounds = true;

        _lastTickAt = DateTime.UtcNow;
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            (_, _) => Tick());
    }

    public void SetNodeCount(int count)
    {
        var cx = (float)(Bounds.Width / 2);
        var cy = (float)(Bounds.Height / 2);
        Model.Nodes.Clear();
        foreach (var node in PhysicsModel.CreateDefaultNodes(count, cx, cy))
            Model.Nodes.Add(node);
        ResetSimulation();
    }

    public void ResetSimulation()
    {
        Model.ResetVelocities();
        _lastTickAt = DateTime.UtcNow;
        _timer.Start();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _lastTickAt = DateTime.UtcNow;
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_firstSize && Bounds.Width > 0 && Bounds.Height > 0)
        {
            _firstSize = false;
            SetNodeCount(5);
        }
        _timer.Start();
    }

    private void Tick()
    {
        try
        {
            if (Bounds.Width < 1 || Bounds.Height < 1) return;

            var now = DateTime.UtcNow;
            var dt = (float)(now - _lastTickAt).TotalSeconds;
            _lastTickAt = now;
            dt = Math.Clamp(dt, 0f, 0.05f);

            var simDt = dt * SimSpeed;
            var steps = (int)Math.Ceiling(simDt / MaxSubstepDt);
            steps = Math.Clamp(steps, 1, MaxSubstepsPerTick);
            var subDt = simDt / steps;

            for (var i = 0; i < steps; i++)
                Model.Step(subDt);

            InvalidateVisual();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Tick] {ex}");
        }
    }

    public override void Render(DrawingContext context)
    {
        try
        {
            context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

            foreach (var node in Model.Nodes)
            {
                var rect = new Rect(
                    node.Position.X - node.Width / 2,
                    node.Position.Y - node.Height / 2,
                    node.Width,
                    node.Height);

                context.FillRectangle(NodeFill, rect, 8);
                context.DrawRectangle(null, NodePen, rect, 8);

                var ft = MakeText(node.Label, 14, Brushes.White);

                var textX = node.Position.X - ft.Width / 2;
                var textY = node.Position.Y - ft.Height / 2;
                context.DrawText(ft, new Point(textX, textY));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Render] {ex}");
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var point = new Vector2((float)pos.X, (float)pos.Y);

        for (var i = Model.Nodes.Count - 1; i >= 0; i--)
        {
            var node = Model.Nodes[i];
            var halfW = node.Width / 2;
            var halfH = node.Height / 2;

            if (point.X >= node.Position.X - halfW &&
                point.X <= node.Position.X + halfW &&
                point.Y >= node.Position.Y - halfH &&
                point.Y <= node.Position.Y + halfH)
            {
                _dragNode = node;
                _dragOffset = new Vector2(node.Position.X - point.X, node.Position.Y - point.Y);
                e.Pointer.Capture(this);
                break;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragNode == null) return;

        var pos = e.GetPosition(this);
        _dragNode.Position = new Vector2(
            (float)pos.X + _dragOffset.X,
            (float)pos.Y + _dragOffset.Y);
        _dragNode.Velocity = Vector2.Zero;

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragNode != null)
        {
            _dragNode = null;
            e.Pointer.Capture(null);
        }
    }
}
