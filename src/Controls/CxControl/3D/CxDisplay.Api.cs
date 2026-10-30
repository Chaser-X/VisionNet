using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Public API surface: surface data, geometric overlays, and view management.</summary>
    public partial class CxDisplay
    {
        #region Surface items - Set (表面对象：写入/替换)

        // Default keys used by the typed Set* overloads when no explicit key is supplied.
        private const string DefaultSurfaceKey    = "Surface";
        private const string DefaultPointCloudKey = "PointCloud";
        private const string DefaultMeshKey       = "Mesh";

        /// <summary>
        /// Adds or replaces the structured surface stored under <paramref name="key"/>: a new
        /// key appends an item, an existing key replaces the item occupying it in place so the
        /// draw order of the remaining items is kept. Keys are unique across all surface-class
        /// items, so an existing key is replaced regardless of the previous item's type.
        /// </summary>
        /// <param name="s">Structured surface to display.</param>
        /// <param name="key">Item key; <c>null</c> uses the default key <c>"Surface"</c>.</param>
        /// <param name="pose">Optional model matrix; identity when <c>null</c>.</param>
        /// <param name="mode">Optional surface mode; <c>null</c> uses the display's current mode.</param>
        /// <param name="colorMode">Optional colour mode; <c>null</c> uses the display's current mode.</param>
        /// <param name="maxPointCount">Upload budget; larger data is down-sampled before upload.</param>
        /// <param name="autoFit">When <c>true</c> (default) the camera refits to the combined bounds.</param>
        /// <returns>The created item, suitable for <see cref="CxGroupManager.Add(CxGroup, ICxObjRenderItem)"/>.</returns>
        public CxSurfaceItem SetSurface(CxSurface s, string key = null, CxMatrix4X4 pose = null,
            SurfaceMode? mode = null, SurfaceColorMode? colorMode = null,
            int maxPointCount = 2_000_000, bool autoFit = true)
        {
            var item = new CxSurfaceItem(s, ResolveMode(mode), ResolveColorMode(colorMode), maxPointCount)
                { ModelMatrix = pose ?? CxMatrix4X4.Identity() };
            item.PrepareRenderData();
            SetSurfaceItem(key ?? DefaultSurfaceKey, item, autoFit);
            return item;
        }

        /// <summary>
        /// Adds or replaces the ordered point cloud stored under <paramref name="key"/>. Data is
        /// sampled to the requested vertex budget before it is uploaded to the GPU.
        /// </summary>
        /// <param name="c">Ordered point cloud to display.</param>
        /// <param name="key">Item key; <c>null</c> uses the default key <c>"PointCloud"</c>.</param>
        /// <param name="pose">Optional model matrix; identity when <c>null</c>.</param>
        /// <param name="mode">Optional surface mode; <c>null</c> uses the display's current mode.</param>
        /// <param name="colorMode">Optional colour mode; <c>null</c> uses the display's current mode.</param>
        /// <param name="maxPointCount">Upload budget; larger data is down-sampled before upload.</param>
        /// <param name="autoFit">When <c>true</c> (default) the camera refits to the combined bounds.</param>
        /// <returns>The created item, suitable for <see cref="CxGroupManager.Add(CxGroup, ICxObjRenderItem)"/>.</returns>
        public CxPointCloudItem SetPointCloud(CxPointCloud c, string key = null, CxMatrix4X4 pose = null,
            SurfaceMode? mode = null, SurfaceColorMode? colorMode = null,
            int maxPointCount = 2_000_000, bool autoFit = true)
        {
            var item = new CxPointCloudItem(c, ResolveMode(mode), ResolveColorMode(colorMode), maxPointCount)
                { ModelMatrix = pose ?? CxMatrix4X4.Identity() };
            item.PrepareRenderData();
            SetSurfaceItem(key ?? DefaultPointCloudKey, item, autoFit);
            return item;
        }

        /// <summary>Adds or replaces the mesh stored under <paramref name="key"/>.</summary>
        /// <param name="m">Mesh to display.</param>
        /// <param name="key">Item key; <c>null</c> uses the default key <c>"Mesh"</c>.</param>
        /// <param name="pose">Optional model matrix; identity when <c>null</c>.</param>
        /// <param name="mode">Optional surface mode; <c>null</c> uses the display's current mode.</param>
        /// <param name="colorMode">Optional colour mode; <c>null</c> uses the display's current mode.</param>
        /// <param name="autoFit">When <c>true</c> (default) the camera refits to the combined bounds.</param>
        /// <returns>The created item, suitable for <see cref="CxGroupManager.Add(CxGroup, ICxObjRenderItem)"/>.</returns>
        public CxMeshItem SetMesh(CxMesh m, string key = null, CxMatrix4X4 pose = null,
            SurfaceMode? mode = null, SurfaceColorMode? colorMode = null, bool autoFit = true)
        {
            var item = new CxMeshItem(m, ResolveMode(mode), ResolveColorMode(colorMode))
                { ModelMatrix = pose ?? CxMatrix4X4.Identity() };
            item.PrepareRenderData();
            SetSurfaceItem(key ?? DefaultMeshKey, item, autoFit);
            return item;
        }

        /// <summary>
        /// Adds <paramref name="item"/> under <paramref name="key"/>, or replaces the item
        /// currently stored under that key. The replaced item is detached from its groups, its
        /// GL resources are queued for deferred release and it is disposed. An item is registered at
        /// most once: passing an already registered instance re-keys it (keeping its queue position)
        /// instead of adding a duplicate entry. This is the shared upsert core used by all typed
        /// <c>Set*</c> overloads.
        /// </summary>
        /// <param name="key">Unique, non-empty item key (compared case-sensitively).</param>
        /// <param name="item">Render item to store.</param>
        /// <param name="autoFit">When <c>true</c> (default) the camera refits to the combined bounds.</param>
        /// <exception cref="ArgumentException">When <paramref name="key"/> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentNullException">When <paramref name="item"/> is <c>null</c>.</exception>
        public void SetSurfaceItem(string key, ICxObjRenderItem item, bool autoFit = true)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must not be null or empty.", nameof(key));
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            CxBox3D? combined = null;
            bool alreadyRegistered;
            lock (_renderLock)
            {
                // Subscribe with a per-item delegate stored on the handle, so the dirty flag can be
                // raised for exactly this item and unsubscribed by the same instance.
                var handle = new GLResourceHandle { IsValid = false, NeedsUpdate = true };
                Action onDataChanged = () => OnItemRenderDataChanged(item);
                handle.RenderDataChangedHandler = onDataChanged;

                alreadyRegistered = _registry.RegisterSurfaceItem(key, item, handle,
                    OnSurfaceRegistrationDisplaced);

                if (!alreadyRegistered)
                {
                    item.OnRenderDataChanged += onDataChanged;

                    // Only needed for the camera fit; skipped entirely when the caller opts out.
                    combined = autoFit ? GetCombinedBoundingBox() : null;
                }
            }

            if (alreadyRegistered) return;

            if (autoFit)
                lock (_cameraLock)
                    _camera.FitView(combined);

            // Keyed group members keep their slot: re-apply the stored local pose to the new resource.
            Groups.OnSurfaceKeyChanged(key);
            SafeInvalidate();
        }
        #endregion Surface items - Set (表面对象：写入/替换)

        #region Surface items - Get (表面对象：查询)

        /// <summary>Gets the structured surface stored under <paramref name="key"/>.</summary>
        /// <param name="key">Item key; <c>null</c> uses the default key <c>"Surface"</c>.</param>
        /// <returns>The item, or <c>null</c> when the key is missing or holds another item type.</returns>
        public CxSurfaceItem GetSurface(string key = null)
        {
            return _registry.GetByKey(key ?? DefaultSurfaceKey) as CxSurfaceItem;
        }

        /// <summary>Gets the ordered point cloud stored under <paramref name="key"/>.</summary>
        /// <param name="key">Item key; <c>null</c> uses the default key <c>"PointCloud"</c>.</param>
        /// <returns>The item, or <c>null</c> when the key is missing or holds another item type.</returns>
        public CxPointCloudItem GetPointCloud(string key = null)
        {
            return _registry.GetByKey(key ?? DefaultPointCloudKey) as CxPointCloudItem;
        }

        /// <summary>Gets the mesh stored under <paramref name="key"/>.</summary>
        /// <param name="key">Item key; <c>null</c> uses the default key <c>"Mesh"</c>.</param>
        /// <returns>The item, or <c>null</c> when the key is missing or holds another item type.</returns>
        public CxMeshItem GetMesh(string key = null)
        {
            return _registry.GetByKey(key ?? DefaultMeshKey) as CxMeshItem;
        }

        /// <summary>
        /// Gets whichever surface-class item (structured surface, point cloud or mesh) is stored
        /// under <paramref name="key"/>, regardless of its concrete type.
        /// </summary>
        /// <returns>The item, or <c>null</c> when the key is <c>null</c>/empty or missing.</returns>
        public ICxObjRenderItem GetSurfaceItem(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return _registry.GetByKey(key);
        }
        #endregion Surface items - Get (表面对象：查询)

        #region Surface items - Remove / Clear (表面对象：移除/清空)

        /// <summary>Removes all surface items and queues their GL resources for deferred release.</summary>
        public void ClearSurfaceItems()
        {
            lock (_renderLock)
            {
                // Keyed group leaves are intentionally kept: a later resource under the same key
                // re-binds to them (see CxGroupManager).
                _registry.ClearSurfaceItems(OnSurfaceRegistrationDisplaced);
            }
            SafeInvalidate();
        }

        /// <summary>Removes the surface item stored under <paramref name="key"/>.</summary>
        /// <param name="key">Item key; <c>null</c> or empty removes nothing.</param>
        /// <returns><c>true</c> when an item was removed; <c>false</c> when the key was missing.</returns>
        public bool RemoveSurfaceItem(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            bool removed;
            lock (_renderLock)
            {
                removed = _registry.RemoveSurfaceItem(key, out var item, out var handle);
                if (removed) OnSurfaceRegistrationDisplaced(item, handle, disposeItem: true);
            }
            if (removed) SafeInvalidate();
            return removed;
        }

        /// <summary>Removes a single surface item and queues its GL resources for deferred release.</summary>
        /// <param name="item">Item to remove; ignored when <c>null</c> or not present.</param>
        public void RemoveSurfaceItem(ICxObjRenderItem item)
        {
            if (item == null) return;

            bool removed;
            lock (_renderLock)
            {
                removed = _registry.RemoveSurfaceItem(item, out var handle);
                if (removed) OnSurfaceRegistrationDisplaced(item, handle, disposeItem: true);
            }
            if (removed) SafeInvalidate();
        }
        #endregion Surface items - Remove / Clear (表面对象：移除/清空)

        #region Surface items - Pose (表面对象：位姿)

        /// <summary>Sets the model matrix (pose) of the structured surface items.</summary>
        /// <param name="pose">New model matrix.</param>
        /// <param name="key">Optional item key; <c>null</c> updates every <see cref="CxSurfaceItem"/>.</param>
        public void SetSurfacePose(CxMatrix4X4 pose, string key = null)
        {
            if (key == null)
            {
                foreach (var item in _registry.CopyItemsOfType<CxSurfaceItem>())
                    item.ModelMatrix = pose;
            }
            else if (_registry.GetByKey(key) is CxSurfaceItem surfaceItem)
            {
                surfaceItem.ModelMatrix = pose;
            }
            SafeInvalidate();
        }

        /// <summary>Sets the model matrix (pose) of the point-cloud items.</summary>
        /// <param name="pose">New model matrix.</param>
        /// <param name="key">Optional item key; <c>null</c> updates every <see cref="CxPointCloudItem"/>.</param>
        public void SetPointCloudPose(CxMatrix4X4 pose, string key = null)
        {
            if (key == null)
            {
                foreach (var item in _registry.CopyItemsOfType<CxPointCloudItem>())
                    item.ModelMatrix = pose;
            }
            else if (_registry.GetByKey(key) is CxPointCloudItem cloudItem)
            {
                cloudItem.ModelMatrix = pose;
            }
            SafeInvalidate();
        }

        /// <summary>Sets the model matrix (pose) of the mesh items.</summary>
        /// <param name="pose">New model matrix.</param>
        /// <param name="key">Optional item key; <c>null</c> updates every <see cref="CxMeshItem"/>.</param>
        public void SetMeshPose(CxMatrix4X4 pose, string key = null)
        {
            if (key == null)
            {
                foreach (var item in _registry.CopyItemsOfType<CxMeshItem>())
                    item.ModelMatrix = pose;
            }
            else if (_registry.GetByKey(key) is CxMeshItem meshItem)
            {
                meshItem.ModelMatrix = pose;
            }
            SafeInvalidate();
        }

        #endregion Surface items - Pose (表面对象：位姿)

        #region Display range (颜色/差分范围)

        /// <summary>
        /// Overrides the Z (height) range used for colour mapping in Color /
        /// ColorWithIntensity modes, applied to every surface item and the colour bar.
        /// Items whose <see cref="SurfaceColorMode"/> is Diff / Intensity / Lit ignore
        /// this (their range is independent). Pass <c>null</c> to restore the default
        /// auto-aggregated range.
        /// </summary>
        public void SetColorRange(float zMin, float zMax)
        {
            _manualColorRange = (zMin, zMax);

            foreach (var item in _registry.SnapshotItems())
                if (item != null && !item.IsDisposed)
                    item.SetGlobalZRange(zMin, zMax);

            SafeInvalidate();
        }

        /// <summary>
        /// Clears a previously set manual colour range, restoring the per-frame
        /// auto-aggregated global Z range.
        /// </summary>
        public void ClearColorRange()
        {
            _manualColorRange = null;
            SafeInvalidate();
        }

        /// <summary>
        /// Overrides the diff value range used for colour mapping in
        /// <see cref="SurfaceColorMode.Diff"/>, applied to every Diff-mode surface item
        /// and the colour bar. Each item auto-computes its diff range when this is never
        /// called. Symmetric to <see cref="SetColorRange"/>.
        /// </summary>
        public void SetDiffRange(float min, float max)
        {
            _manualDiffRange = (min, max);

            // Apply immediately to current Diff-mode items; the renderer re-propagates
            // each frame while _manualDiffRange is set.
            foreach (var item in _registry.SnapshotItems())
                if (item != null && !item.IsDisposed)
                    item.SetGlobalDiffRange(min, max);

            SafeInvalidate();
        }

        /// <summary>
        /// Clears a previously set manual diff range, restoring the per-frame
        /// auto-aggregated diff range. Symmetric to <see cref="ClearColorRange"/>.
        /// </summary>
        public void ClearDiffRange()
        {
            _manualDiffRange = null;
            SafeInvalidate();
        }
        #endregion Display range (颜色/差分范围)

        #region Overlay items - Set (叠加层：写入)

        /// <summary>Appends a set of 3D line segments to the overlay layer.</summary>
        /// <returns>The created item; configure <see cref="AbstractRenderItem.IsActiveObj"/> and
        /// subscribe to <see cref="AbstractRenderItem.OnChanged"/> before or after this call.</returns>
        public CxSegment3DItem SetSegment(CxSegment3D[] segment, Color color, float size = 1.0f, string key = null)
        {
            var item = new CxSegment3DItem(segment, color, size);
            RegisterOverlayItem(key, item);
            return item;
        }

        /// <summary>Appends a set of 3D points to the overlay layer.</summary>
        /// <returns>The created item.</returns>
        public CxPoint3DItem SetPoint(CxPoint3D[] point, Color color, float size = 1.0f,
            PointShape shape = PointShape.Point, string key = null)
        {
            var item = new CxPoint3DItem(point, color, size, shape);
            RegisterOverlayItem(key, item);
            return item;
        }

        /// <summary>Appends a set of 3D polygons to the overlay layer.</summary>
        /// <returns>The created item.</returns>
        public CxPolygon3DItem SetPolygon(CxPolygon3D[] polygon, Color color, float size = 1.0f, string key = null)
        {
            var item = new CxPolygon3DItem(polygon, color, size);
            RegisterOverlayItem(key, item);
            return item;
        }

        /// <summary>Appends a set of 3D planes to the overlay layer.</summary>
        /// <returns>The created item.</returns>
        public CxPlane3DItem SetPlane(CxPlane3D[] plane, Color color, float size = 100.0f, string key = null)
        {
            var item = new CxPlane3DItem(plane, color, size);
            RegisterOverlayItem(key, item);
            return item;
        }

        /// <summary>Appends a set of axis-aligned bounding boxes to the overlay layer.</summary>
        /// <returns>The created item.</returns>
        public CxBox3DItem SetBox(CxBox3D[] box, Color color, float size = 1.0f, string key = null)
        {
            var item = new CxBox3DItem(box, color, size);
            RegisterOverlayItem(key, item);
            return item;
        }

        /// <summary>Appends world-anchored text labels to the overlay layer.</summary>
        /// <returns>The created item.</returns>
        public CxTextInfoItem SetTextInfo(CxTextInfo[] textInfo, Color color, string key = null)
        {
            var item = new CxTextInfoItem(textInfo, color, 1);
            RegisterOverlayItem(key, item);
            return item;
        }

        /// <summary>Appends screen-space 2D text overlays.</summary>
        /// <returns>The created item.</returns>
        public CxText2DItem SetText2D(CxText2D[] text2Ds, Color color, string key = null)
        {
            var item = new CxText2DItem(text2Ds, color, 1);
            RegisterOverlayItem(key, item);
            return item;
        }
        #endregion Overlay items - Set (叠加层：写入)

        #region Overlay items - Key / Get / Remove (叠加层：key 与单项管理)

        /// <summary>
        /// Adds <paramref name="item"/> under <paramref name="key"/>, or replaces the item currently
        /// stored under that key in place (draw order kept) and disposes the previous one. An item is
        /// registered at most once: passing an already registered instance re-keys it in place
        /// instead of adding a duplicate entry. Overlay keys form a namespace of their own — they
        /// never collide with surface keys.
        /// </summary>
        /// <exception cref="ArgumentException">When <paramref name="key"/> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentNullException">When <paramref name="item"/> is <c>null</c>.</exception>
        public void SetOverlayItem(string key, AbstractRenderItem item)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must not be null or empty.", nameof(key));
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            AbstractRenderItem replaced;
            bool alreadyRegistered;
            lock (_renderLock)
            {
                alreadyRegistered = _registry.RegisterOverlayItem(key, item, out replaced);
            }

            if (alreadyRegistered) return;

            if (replaced != null)
            {
                // Keep the selection and the group membership pointing at the replacement.
                if (ReferenceEquals(_selectedItem, replaced))
                {
                    ClearSelection();
                    _isDraggingItem = false;
                }

                Groups.OnOverlayItemReplaced(replaced, item);
                replaced.Dispose();
            }

            SafeInvalidate();
        }

        /// <summary>Gets the overlay item stored under <paramref name="key"/>, or <c>null</c>.</summary>
        public AbstractRenderItem GetOverlayItem(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return _registry.GetOverlayByKey(key);
        }

        /// <summary>Removes and disposes the overlay item stored under <paramref name="key"/>.</summary>
        /// <returns><c>true</c> when an item was removed; <c>false</c> when the key was missing.</returns>
        public bool RemoveOverlayItem(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            AbstractRenderItem removed;
            lock (_renderLock)
            {
                removed = _registry.RemoveOverlayItem(key);
            }

            if (removed == null) return false;

            if (ReferenceEquals(_selectedItem, removed))
            {
                ClearSelection();
                _isDraggingItem = false;
            }

            Groups.ForgetOverlayMembers(new object[] { removed });
            removed.Dispose();
            SafeInvalidate();
            return true;
        }

        /// <summary>Removes and disposes a single overlay item; its key (if any) is released.</summary>
        /// <param name="item">Item to remove; ignored when <c>null</c> or not present.</param>
        public void RemoveOverlayItem(AbstractRenderItem item)
        {
            if (item == null) return;

            bool removed;
            lock (_renderLock)
            {
                removed = _registry.RemoveOverlayItem(item);
            }

            if (!removed) return;

            if (ReferenceEquals(_selectedItem, item))
            {
                ClearSelection();
                _isDraggingItem = false;
            }

            Groups.ForgetOverlayMembers(new object[] { item });
            item.Dispose();
            SafeInvalidate();
        }

        /// <summary>
        /// Registers a freshly created overlay item: appended when <paramref name="key"/> is
        /// <c>null</c> (the historical behaviour), upserted under the key otherwise.
        /// </summary>
        private void RegisterOverlayItem(string key, AbstractRenderItem item)
        {
            if (key == null)
            {
                _registry.AppendOverlayItem(item);
                SafeInvalidate();
                return;
            }

            SetOverlayItem(key, item);
        }

        #endregion Overlay items - Key / Get / Remove (叠加层：key 与单项管理)

        #region Overlay items - Clear (叠加层：清空)

        /// <summary>
        /// Clears every overlay item added through <see cref="SetSegment"/> / <see cref="SetPoint"/> /
        /// <see cref="SetPolygon"/> / <see cref="SetPlane"/> / <see cref="SetBox"/> /
        /// <see cref="SetTextInfo"/> / <see cref="SetText2D"/> / <see cref="SetCoordinate3DSystem"/>.
        /// Surface-class items and the built-in HUD (bottom-left axes, colour bar, coordinate tag)
        /// are left untouched.
        /// </summary>
        public void ClearOverlayItems()
        {
            // Drop the selection first, outside the locks: OnDeselected is an external callback and
            // must not run while the render / registry locks are held (the render pass takes the same
            // locks, so a re-entrant callback could deadlock). No-op when nothing is selected.
            ClearSelection();
            _isDraggingItem = false;

            lock (_renderLock)
            {
                var overlays = _registry.CopyOverlayItems();
                Groups.ForgetOverlayMembers(overlays);
                _registry.ClearOverlayItems(o => o.Dispose());
            }
            SafeInvalidate();
        }
        #endregion Overlay items - Clear (叠加层：清空)

        #region Interaction events (交互事件)

        /// <summary>
        /// Raised when the selected overlay item changes: the argument is the newly selected item,
        /// or <c>null</c> when the selection was cleared. Raised on the UI thread.
        /// </summary>
        public event Action<AbstractRenderItem> SelectionChanged;

        /// <summary>
        /// Raised on mouse move while hovering: the world-space location and the intensity (or
        /// diff value) under the cursor. A <c>null</c> location means the cursor is not over any
        /// geometry (or a drag is in progress). Consecutive moves that land on the same pixel are
        /// coalesced into a single notification. Raised on the UI thread.
        /// </summary>
        public event Action<CxPoint3D?, float?> CoordinatesChanged;

        /// <summary>
        /// Raised when a surface item cannot be uploaded (e.g. its shader failed to compile) and is
        /// skipped while drawing. Raised on the render thread, after the display's resource lock has
        /// been released, so calling back into the display from the handler is safe; WinForms
        /// controls must still be updated with <c>BeginInvoke</c> (a synchronous <c>Invoke</c> can
        /// deadlock the frame). A later retry (after the item reports new data) can raise it again.
        /// </summary>
        public event Action<ICxObjRenderItem> RenderItemFailed;

        #endregion Interaction events (交互事件)

        #region Coordinate system & camera (坐标系与相机)

        /// <summary>
        /// Sets whether the rendering coordinate system is left-handed.
        /// Default is right-handed (standard OpenGL convention).
        /// </summary>
        /// <param name="leftHanded"><c>true</c> for left-handed, <c>false</c> for right-handed.</param>
        [Obsolete("Use Camera.IsLeftHanded instead.")]
        public void SetCoordinateSystemLeftHanded(bool leftHanded)
        {
            lock (_cameraLock)
                _camera.IsLeftHanded = leftHanded;
        }

        /// <summary>
        /// Sets the Z-axis display scale factor.
        /// Values greater than 1 stretch the Z axis; values between 0 and 1 compress it.
        /// Can also be adjusted interactively via Shift + mouse wheel.
        /// </summary>
        /// <param name="scale">Scale factor, clamped to [0.01, ∞).</param>
        [Obsolete("Use Camera.ZScale instead.")]
        public void SetZScale(float scale)
        {
            lock (_cameraLock)
                _camera.ZScale = scale;
        }

        /// <summary>
        /// Appends a 3D coordinate-frame axes indicator to the overlay layer.
        /// If <paramref name="coordination"/> is <c>null</c>, the world-origin axes are used.
        /// </summary>
        public void SetCoordinate3DSystem(CxCoordination3D? coordination = null, float axisLength = 5, string key = null)
        {
            if (!coordination.HasValue)
                coordination = new CxCoordination3D
                {
                    Origin = new CxPoint3D(0, 0, 0),
                    XAxis  = new CxVector3D(1, 0, 0),
                    YAxis  = new CxVector3D(0, 1, 0),
                    ZAxis  = new CxVector3D(0, 0, 1),
                };

            var item = new CxCoordinateSystemItem(
                axisLength, axisLength / 50, axisLength / 10, axisLength / 25, coordination);
            RegisterOverlayItem(key, item);
        }
        #endregion Coordinate system & camera (坐标系与相机)

        #region Selection (选中项管理)

        /// <summary>
        /// Clears the current selection: notifies the selected item via
        /// <see cref="AbstractRenderItem.OnDeselected"/> and sets <see cref="SelectedItem"/> to <c>null</c>.
        /// </summary>
        public void ClearSelection()
        {
            if (_selectedItem == null) return;
            _selectedItem.OnDeselected();
            _selectedItem = null;
            SelectionChanged?.Invoke(null);
            SafeInvalidate();
        }

        /// <summary>
        /// Programmatically selects <paramref name="item"/>, clearing any previous selection.
        /// Does nothing when <paramref name="item"/> is <c>null</c> or its
        /// <see cref="AbstractRenderItem.IsActiveObj"/> is <c>false</c>.
        /// </summary>
        public void SelectItem(AbstractRenderItem item)
        {
            if (item == null || !item.IsActiveObj) return;
            ClearSelection();
            _selectedItem = item;
            _selectedItem.OnMouseDown(default);
            SelectionChanged?.Invoke(item);
            SafeInvalidate();
        }

        /// <summary>
        /// Sets <see cref="AbstractRenderItem.IsActiveObj"/> to <c>true</c> for all geometry
        /// overlay items currently in the display, enabling selection and drag interaction.
        /// </summary>
        public void ActivateAllItems()
        {
            foreach (var item in _registry.SnapshotOverlayItems())
                if (item is AbstractRenderItem ar)
                    ar.IsActiveObj = true;
            // IsActiveObj gates the selected-item marker, so a redraw may be needed.
            SafeInvalidate();
        }

        /// <summary>
        /// Sets <see cref="AbstractRenderItem.IsActiveObj"/> to <c>false</c> for all geometry
        /// overlay items, disabling selection and drag interaction.
        /// Also clears the current selection.
        /// </summary>
        public void DeactivateAllItems()
        {
            ClearSelection();
            foreach (var item in _registry.SnapshotOverlayItems())
                if (item is AbstractRenderItem ar)
                    ar.IsActiveObj = false;
        }
        #endregion Selection (选中项管理)

        #region Background (背景色)

        /// <summary>
        /// Sets the rendering background colour (OpenGL clear colour), preserving alpha.
        /// The colour is applied on the GL render thread each frame, so it is safe to call
        /// from any thread and survives GL context recreation.
        /// </summary>
        public void SetBackgroundColor(Color color)
        {
            _backColor = color;
            SafeInvalidate();
        }
        #endregion Background (背景色)

        #region View management (视图管理)

        /// <summary>
        /// Resets the display: disposes all overlay geometry and reinitialises HUD items.
        /// If <paramref name="resetAll"/> is <c>true</c>, also removes all surface items.
        /// </summary>
        public void ResetView(bool resetAll = true)
        {
            ClearOverlayItems();

            _coordinationItem = new CxCoordinateSystemItem();
            _coordTagItem     = new CxCoordinationTagItem();
            _colorBarItem     = new CxColorBarItem();

            if (resetAll)
            {
                ClearSurfaceItems();
                Groups.Clear();
            }

            SafeInvalidate();
        }

        /// <summary>Sets the camera rotation pivot to the given world-space position.</summary>
        [Obsolete("Use Camera.FocusOnPoint(Vector3) instead.")]
        public void SetViewCenter(CxPoint3D center)
        {
            lock (_cameraLock)
                _camera.FocusOnPoint(new Vector3(center.X, center.Y, center.Z));
            SafeInvalidate();
        }

        /// <summary>Sets the camera up-direction vector.</summary>
        [Obsolete("Use Camera.SetDefaultUpView(Vector3) instead.")]
        public void SetViewUpDirection(CxVector3D upDirection)
        {
            lock (_cameraLock)
                _camera.SetDefaultUpView(new Vector3(upDirection.X, upDirection.Y, upDirection.Z));
        }

        /// <summary>refresh fbo viewer size</summary>
        public void RefreshViewSize()
        {
            if (this.OpenGL.RenderContextProvider == null) return;
            this.SafeInvalidate();
        }
        /// <summary>
        /// Requests a repaint. Thread-safe: marshals to the UI thread when called from another thread.
        /// </summary>
        /// <remarks>
        /// Items returned by <c>Get*</c> keep a CPU-side cache, so changing their underlying data still
        /// requires re-setting them with <c>Set*(..., key)</c>. Pose and colour-mode changes take effect
        /// on the next repaint — call this after mutating an item directly.
        /// </remarks>
        public void Redraw() => SafeInvalidate();

        /// <summary>
        /// Fits the camera to the combined bounding box of all surface items; no-op when the display
        /// has no geometry to fit.
        /// </summary>
        public void FitView() => FitView(null);

        /// <summary>
        /// Fits the camera to <paramref name="viewBox"/>; pass <c>null</c> to fit all surface items.
        /// Complements the per-item <c>autoFit</c> flags, which only apply while setting an item.
        /// </summary>
        public void FitView(CxBox3D? viewBox)
        {
            CxBox3D? box = viewBox;
            if (box == null)
            {
                box = GetCombinedBoundingBox();
                if (box == null) return;
            }

            lock (_cameraLock)
                _camera.FitView(box);
            SafeInvalidate();
        }

        #endregion View management (视图管理)

        #region Internal helpers (内部辅助)

        /// <summary>
        /// Resolves an optional per-item surface mode; <c>null</c> falls back to the display's
        /// current <see cref="SurfaceMode"/>. Lets callers give individual items a mode different
        /// from the global default (e.g. a Mesh as Mesh + a PointCloud as PointCloud in one view).
        /// </summary>
        private SurfaceMode ResolveMode(SurfaceMode? mode) => mode ?? SurfaceMode;

        /// <summary>
        /// Resolves an optional per-item colour mode; <c>null</c> falls back to the display's
        /// current <see cref="SurfaceColorMode"/>.
        /// </summary>
        private SurfaceColorMode ResolveColorMode(SurfaceColorMode? colorMode) => colorMode ?? SurfaceColorMode;

        /// <summary>
        /// Cleans up a surface registration the registry displaced: unsubscribes the render-data
        /// handler, queues the GL handle for deferred release and optionally disposes the item.
        /// Called by the registry while it holds its lock (enqueueing is lock-free).
        /// </summary>
        private void OnSurfaceRegistrationDisplaced(ICxObjRenderItem item, GLResourceHandle handle,
            bool disposeItem)
        {
            if (handle?.RenderDataChangedHandler != null)
                item.OnRenderDataChanged -= handle.RenderDataChangedHandler;

            _registry.EnqueueRelease(handle);
            if (disposeItem) item.Dispose();
        }

        /// <summary>
        /// Called when a surface item's CPU data is invalidated (its cached render data was dropped,
        /// e.g. after switching to Diff mode). Marks only that item's handle as needing a GPU rebuild
        /// on the next render frame.
        /// </summary>
        private void OnItemRenderDataChanged(ICxObjRenderItem item)
        {
            if (item == null) return;

            _registry.MarkNeedsUpdate(item);
            SafeInvalidate();
        }
        #endregion Internal helpers (内部辅助)
    }
}
