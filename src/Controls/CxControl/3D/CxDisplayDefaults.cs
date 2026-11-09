using System.Drawing;

namespace VisionNet.Controls
{
    /// <summary>
    /// Process-wide default values for a freshly created <see cref="CxDisplay"/> and for the items
    /// it hosts. This is the single source of truth for the render / lighting defaults that used to
    /// be spread across <see cref="CxDisplay"/>'s field initializers, the render-item field
    /// initializers and the demo UI.
    /// <para>
    /// Semantics: a <see cref="CxDisplay"/> reads these values <b>when it is constructed</b>, so
    /// changing a property here only affects displays (and items) created afterwards. To push the
    /// current values onto an already-created display, call <see cref="ApplyTo"/>. Intended to be
    /// configured once at application startup (UI thread); the properties are plain statics and are
    /// not synchronised.
    /// </para>
    /// </summary>
    public static class CxDisplayDefaults
    {
        // ── View ─────────────────────────────────────────────────────────────────

        /// <summary>Initial camera view direction. Default <see cref="ViewMode.Top"/>.</summary>
        public static ViewMode ViewMode { get; set; } = ViewMode.Top;

        /// <summary>
        /// Whether the view matrix mirrors the world in Y (left-handed display). Default <c>false</c>.
        /// Must match the handedness convention of the data being displayed.
        /// </summary>
        public static bool IsLeftHanded { get; set; } = false;

        /// <summary>Whether the world-space coordinate axes are shown. Default <c>false</c>.</summary>
        public static bool ShowCoordinateSystem { get; set; } = false;

        /// <summary>Whether the colour-bar HUD overlay is drawn. Default <c>true</c>.</summary>
        public static bool IsColorBarVisible { get; set; } = true;

        // ── Surface ──────────────────────────────────────────────────────────────

        /// <summary>Initial surface rendering mode. Default <see cref="SurfaceMode.PointCloud"/>.</summary>
        public static SurfaceMode SurfaceMode { get; set; } = SurfaceMode.PointCloud;

        /// <summary>Initial surface colour mode. Default <see cref="SurfaceColorMode.ColorWithIntensity"/>.</summary>
        public static SurfaceColorMode SurfaceColorMode { get; set; } = SurfaceColorMode.ColorWithIntensity;

        // ── Lighting (Mesh mode) ─────────────────────────────────────────────────

        /// <summary>Mesh-mode lighting model. Default <see cref="SurfaceLightingMode.Pbr"/>.</summary>
        public static SurfaceLightingMode SurfaceLightingMode { get; set; } = SurfaceLightingMode.Pbr;

        /// <summary>Procedural IBL environment for PBR. Default <see cref="SurfaceEnvironmentPreset.Neutral"/>.</summary>
        public static SurfaceEnvironmentPreset EnvironmentPreset { get; set; } = SurfaceEnvironmentPreset.Neutral;

        /// <summary>IBL intensity multiplier for PBR (clamped to &gt;= 0). Default <c>1.3</c>.</summary>
        public static float IblIntensity { get; set; } = 1.3f;

        /// <summary>Whether the PBR environment rotates with the camera. Default <c>true</c>.</summary>
        public static bool EnvironmentFollowsCamera { get; set; } = true;

        // ── PBR material ─────────────────────────────────────────────────────────

        /// <summary>PBR metallic factor (clamped to [0, 1]). Default <c>0.2</c>.</summary>
        public static float PbrMetallic { get; set; } = 0.2f;

        /// <summary>PBR roughness factor (clamped to [0, 1]). Default <c>0.5</c>.</summary>
        public static float PbrRoughness { get; set; } = 0.5f;

        // ── Point cloud ──────────────────────────────────────────────────────────

        /// <summary>
        /// Whether point clouds are drawn as pseudo-3D sphere impostors (shaded highlight ball per
        /// point) instead of flat points. Default <c>true</c>.
        /// </summary>
        public static bool PointImpostor { get; set; } = true;

        /// <summary>
        /// Point size in pixels for <see cref="SurfaceMode.PointCloud"/> (clamped at draw time to
        /// &gt;= 1). Default <c>3</c>. Ignored in <see cref="SurfaceMode.Mesh"/>.
        /// </summary>
        public static float PointSize { get; set; } = 3f;

        // ── Render quality ───────────────────────────────────────────────────────

        /// <summary>
        /// Whether the off-screen render target uses MSAA (4×). Default <c>true</c>. Only smooths
        /// mesh / line edges; point-cloud impostor discs (cut out with <c>discard</c>) are unaffected.
        /// Turning it off renders straight to the window.
        /// </summary>
        public static bool MsaaEnabled { get; set; } = true;

        // ── Colour / opacity ─────────────────────────────────────────────────────

        /// <summary>Flat colour for <see cref="SurfaceColorMode.Lit"/>. Default <c>RGB(217,217,217)</c>.</summary>
        public static Color SolidColor { get; set; } = Color.FromArgb(217, 217, 217);

        /// <summary>Opacity applied to every surface item (clamped to [0, 1]). Default <c>1</c>.</summary>
        public static float Opacity { get; set; } = 1f;

        /// <summary>Whether translucent items still write the depth buffer. Default <c>true</c>.</summary>
        public static bool TranslucentWritesDepth { get; set; } = true;

        /// <summary>
        /// Restores every property to the library default (the value it is initialised with above).
        /// </summary>
        public static void Reset()
        {
            ViewMode = ViewMode.Top;
            IsLeftHanded = false;
            ShowCoordinateSystem = false;
            IsColorBarVisible = true;

            SurfaceMode = SurfaceMode.PointCloud;
            SurfaceColorMode = SurfaceColorMode.ColorWithIntensity;

            SurfaceLightingMode = SurfaceLightingMode.Pbr;
            EnvironmentPreset = SurfaceEnvironmentPreset.Neutral;
            IblIntensity = 1.3f;
            EnvironmentFollowsCamera = true;

            PbrMetallic = 0.2f;
            PbrRoughness = 0.5f;

            PointImpostor = true;
            PointSize = 3f;

            MsaaEnabled = true;

            SolidColor = Color.FromArgb(217, 217, 217);
            Opacity = 1f;
            TranslucentWritesDepth = true;
        }

        /// <summary>
        /// Pushes the current default values onto <paramref name="display"/>. Existing surface items
        /// pick up the material / colour / opacity / point-size values through the display's own
        /// propagation; <see cref="ViewMode"/> is applied to the camera (call
        /// <see cref="CxDisplay.FitView()"/> afterwards if the framing should be recomputed).
        /// </summary>
        /// <param name="display">Display to configure; ignored when <c>null</c>.</param>
        public static void ApplyTo(CxDisplay display)
        {
            if (display == null) return;

            display.Camera.ViewMode = ViewMode;
            display.Camera.IsLeftHanded = IsLeftHanded;
            display.ShowCoordinateSystem = ShowCoordinateSystem;
            display.IsColorBarVisible = IsColorBarVisible;

            display.SurfaceMode = SurfaceMode;
            display.SurfaceColorMode = SurfaceColorMode;

            display.SurfaceLightingMode = SurfaceLightingMode;
            display.EnvironmentPreset = EnvironmentPreset;
            display.IblIntensity = IblIntensity;
            display.EnvironmentFollowsCamera = EnvironmentFollowsCamera;

            display.PbrMetallic = PbrMetallic;
            display.PbrRoughness = PbrRoughness;

            display.PointImpostor = PointImpostor;
            display.PointSize = PointSize;

            display.MsaaEnabled = MsaaEnabled;

            display.SolidColor = SolidColor;
            display.Opacity = Opacity;
            display.TranslucentWritesDepth = TranslucentWritesDepth;
        }
    }
}
