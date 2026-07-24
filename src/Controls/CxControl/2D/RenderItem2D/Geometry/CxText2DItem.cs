using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxText2D"/> values as plot-coordinate text labels
    /// on a <see cref="CxDisplay2D"/> control.
    /// <see cref="CxText2D.Location"/> is interpreted as plot coordinates (image pixel space).
    /// Each text entry is a separate ScottPlot <c>Text</c> plottable.
    /// </summary>
    public class CxText2DPlotItem : Abstract2DRenderItem
    {
        private readonly List<ScottPlot.Plottables.Text> _plottables = new List<ScottPlot.Plottables.Text>();

        /// <summary>Gets the text data being rendered.</summary>
        public CxText2D[] Texts { get; private set; }

        /// <summary>Initializes the item with the given text entries and colour.</summary>
        public CxText2DPlotItem(CxText2D[] texts, Color color)
        {
            Texts = texts ?? Array.Empty<CxText2D>();
            Color = color;
            Size  = 12f;
        }

        /// <inheritdoc/>
        public override void AddToPlot(Plot plot)
        {
            _plot = plot;
            BuildPlottables();
        }

        /// <inheritdoc/>
        public override void RemoveFromPlot(Plot plot)
        {
            foreach (var t in _plottables) plot.PlottableList.Remove(t);
            _plottables.Clear();
            _plot = null;
        }

        /// <inheritdoc/>
        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var t in _plottables) _plot.PlottableList.Remove(t);
            _plottables.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            var spColor = ToSPColor(DrawColor);
            foreach (var td in Texts)
            {
                if (string.IsNullOrEmpty(td.Text)) continue;
                var label = _plot.Add.Text(td.Text, td.Location.X, td.Location.Y);
                label.LabelStyle.ForeColor = spColor;
                label.LabelStyle.FontSize  = td.FontSize > 0 ? td.FontSize : 12;
                _plottables.Add(label);
            }
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            for (int i = 0; i < Texts.Length; i++)
                Texts[i] = new CxText2D(
                    new CxPoint2D(Texts[i].Location.X + dx, Texts[i].Location.Y + dy),
                    Texts[i].Text,
                    Texts[i].FontSize);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
                foreach (var t in _plottables) _plot.PlottableList.Remove(t);
        }
    }
}
