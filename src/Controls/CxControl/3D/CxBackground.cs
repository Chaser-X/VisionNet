using System.Drawing;

namespace VisionNet.Controls
{
    /// <summary>
    /// A viewport background: either a single flat colour, or a vertical gradient (top → bottom).
    /// </summary>
    /// <remarks>
    /// Use <see cref="Solid"/> for a flat colour and <see cref="Gradient"/> for a vertical gradient.
    /// Assigned to <see cref="CxDisplay.Background"/>. A solid background clears the framebuffer with
    /// its colour; a gradient fills the viewport with a quad interpolated between the two colours,
    /// which is the common "viewer" backdrop that lifts the rendered object off the background.
    /// </remarks>
    public readonly struct CxBackground
    {
        /// <summary>Top colour (the whole background when <see cref="IsGradient"/> is <c>false</c>).</summary>
        public Color Top { get; }

        /// <summary>Bottom colour (equal to <see cref="Top"/> for a solid colour).</summary>
        public Color Bottom { get; }

        /// <summary><c>true</c> for a vertical gradient; <c>false</c> for a solid colour.</summary>
        public bool IsGradient { get; }

        private CxBackground(Color top, Color bottom, bool isGradient)
        {
            Top = top;
            Bottom = bottom;
            IsGradient = isGradient;
        }

        /// <summary>A single flat background colour.</summary>
        public static CxBackground Solid(Color color) => new CxBackground(color, color, false);

        /// <summary>A vertical background gradient, <paramref name="top"/> → <paramref name="bottom"/>.</summary>
        public static CxBackground Gradient(Color top, Color bottom) => new CxBackground(top, bottom, true);

        /// <inheritdoc/>
        public override string ToString()
            => IsGradient ? $"CxBackground.Gradient({Top} -> {Bottom})" : $"CxBackground.Solid({Top})";
    }
}
