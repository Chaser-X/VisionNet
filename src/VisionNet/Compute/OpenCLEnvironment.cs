using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenCL.Net;
using OpenCL.Net.Extensions;

namespace VisionNet.Compute
{
    /// <summary>
    /// Thread-safe singleton that owns the shared OpenCL context, command queue,
    /// and compiled programs for the application lifetime.
    /// <para>
    /// Each <see cref="OpenCLComputation"/> subclass instance creates its own
    /// <see cref="Kernel"/> objects via <see cref="CreateKernelInstance"/>, keeping
    /// argument state isolated per instance and safe for concurrent use.
    /// </para>
    /// <para>
    /// Usage:
    /// <code>
    /// VisionOperator.InitialLib();          // once at startup
    /// // ... GPU operations ...
    /// VisionOperator.DestroyLib();          // once at shutdown
    /// </code>
    /// </para>
    /// </summary>
    public class OpenCLEnvironment
    {
        private static OpenCLEnvironment _instance;
        private static readonly object _instanceLock = new object();

        private readonly Dictionary<string, Program>                      _programs;
        private readonly Dictionary<string, Dictionary<string, Kernel>>   _kernels;
        private readonly object _buildLock = new object();

        /// <summary>Gets the OpenCL context. Valid only after <see cref="Initialize"/> returns <c>true</c>.</summary>
        public Context? Context  { get; private set; }

        /// <summary>Gets the OpenCL command queue. Valid only after successful initialisation.</summary>
        public CommandQueue? Queue { get; private set; }

        /// <summary>Gets the selected OpenCL device.</summary>
        public Device? Device { get; private set; }

        /// <summary>Gets the last OpenCL error code.</summary>
        public ErrorCode LastError { get; private set; }

        /// <summary>Gets whether the environment has been successfully initialised.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>Gets the global singleton instance.</summary>
        public static OpenCLEnvironment Instance
        {
            get
            {
                if (_instance == null)
                    lock (_instanceLock)
                        if (_instance == null)
                            _instance = new OpenCLEnvironment();
                return _instance;
            }
        }

        private OpenCLEnvironment()
        {
            _programs = new Dictionary<string, Program>();
            _kernels  = new Dictionary<string, Dictionary<string, Kernel>>();
        }

        /// <summary>
        /// Creates the OpenCL context and command queue for the specified device.
        /// Returns <c>true</c> immediately if already initialised.
        /// </summary>
        /// <param name="platformIndex">Index into the list of available platforms (default 0).</param>
        /// <param name="deviceIndex">Index into the list of devices on the chosen platform (default 0).</param>
        /// <param name="deviceType">Device type filter (default <see cref="DeviceType.Default"/>).</param>
        public bool Initialize(int platformIndex = 0, int deviceIndex = 0,
            DeviceType deviceType = DeviceType.Default)
        {
            if (IsInitialized) return true;

            try
            {
                var platforms = Cl.GetPlatformIDs(out ErrorCode err);
                if (err != ErrorCode.Success || platforms == null || !platforms.Any()
                    || platformIndex < 0 || platformIndex >= platforms.Length)
                {
                    Console.WriteLine("No OpenCL platform found or platform index out of range.");
                    return false;
                }

                var devices = Cl.GetDeviceIDs(platforms[platformIndex], deviceType, out err);
                if (err != ErrorCode.Success || devices == null || !devices.Any()
                    || deviceIndex < 0 || deviceIndex >= devices.Length)
                {
                    Console.WriteLine("No OpenCL device found or device index out of range.");
                    return false;
                }

                Device  = devices[deviceIndex];
                Context = Cl.CreateContext(null, 1, new[] { Device.Value }, null, IntPtr.Zero, out err);
                if (err != ErrorCode.Success || !Context.Value.IsValid())
                {
                    Console.WriteLine($"Failed to create OpenCL context: {err}");
                    return false;
                }

                Queue = Cl.CreateCommandQueue(Context.Value, Device.Value,
                    CommandQueueProperties.None, out err);
                if (err != ErrorCode.Success || !Queue.Value.IsValid())
                {
                    Console.WriteLine($"Failed to create OpenCL command queue: {err}");
                    return false;
                }

                IsInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OpenCL initialisation error: {ex.Message}");
                Cleanup();
                return false;
            }
        }

        /// <summary>
        /// Compiles an OpenCL program and creates prototype kernel objects if not already done.
        /// Subsequent calls with the same <paramref name="programId"/> are no-ops.
        /// </summary>
        /// <param name="programId">Unique identifier for this program (used as cache key).</param>
        /// <param name="kernelSource">OpenCL C source code.</param>
        /// <param name="kernelNames">Names of the kernel functions to create.</param>
        /// <returns><c>true</c> if the program was compiled and all kernels were created.</returns>
        public bool BuildProgram(string programId, string kernelSource, string[] kernelNames)
        {
            if (!IsInitialized) { Console.WriteLine("OpenCL not initialised."); return false; }

            lock (_buildLock)
            {
                if (_programs.ContainsKey(programId)) return true;

                try
                {
                    var program = Cl.CreateProgramWithSource(Context.Value, 1,
                        new[] { kernelSource }, null, out ErrorCode err);
                    if (err != ErrorCode.Success || !program.IsValid())
                    {
                        Console.WriteLine($"CreateProgram failed: {err}");
                        return false;
                    }

                    err = Cl.BuildProgram(program, 1, new[] { Device.Value },
                        string.Empty, null, IntPtr.Zero);
                    if (err != ErrorCode.Success)
                    {
                        var log = Cl.GetProgramBuildInfo(program, Device.Value,
                            ProgramBuildInfo.Log, out _).ToString();
                        Console.WriteLine($"BuildProgram failed: {err}\n{log}");
                        Cl.ReleaseProgram(program);
                        return false;
                    }

                    var kernelDict = new Dictionary<string, Kernel>();
                    foreach (string name in kernelNames)
                    {
                        var kernel = Cl.CreateKernel(program, name, out err);
                        if (err != ErrorCode.Success || !kernel.IsValid())
                        {
                            Console.WriteLine($"CreateKernel '{name}' failed: {err}");
                            foreach (var k in kernelDict.Values) Cl.ReleaseKernel(k);
                            Cl.ReleaseProgram(program);
                            return false;
                        }
                        kernelDict[name] = kernel;
                    }

                    _programs[programId] = program;
                    _kernels[programId]  = kernelDict;
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"BuildProgram exception: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Creates a new, independent kernel instance from the compiled program.
        /// Each caller receives its own <see cref="Kernel"/> object with separate argument
        /// state, making concurrent <c>SetKernelArg</c> calls safe across instances.
        /// The returned kernel is owned by the caller and must be released via
        /// <c>Cl.ReleaseKernel</c> when no longer needed (handled automatically by
        /// <see cref="OpenCLComputation.Dispose"/>).
        /// </summary>
        /// <param name="programId">Identifier of the compiled program (must have been built via <see cref="BuildProgram"/>).</param>
        /// <param name="kernelName">Name of the kernel function.</param>
        /// <returns>A fresh <see cref="Kernel"/> instance, or <c>null</c> on failure.</returns>
        public Kernel? CreateKernelInstance(string programId, string kernelName)
        {
            if (!IsInitialized) return null;

            Program program;
            lock (_buildLock)
            {
                if (!_programs.TryGetValue(programId, out program)) return null;
            }

            var kernel = Cl.CreateKernel(program, kernelName, out ErrorCode err);
            if (err != ErrorCode.Success || !kernel.IsValid())
            {
                Console.WriteLine($"CreateKernelInstance '{kernelName}' failed: {err}");
                return null;
            }
            return kernel;
        }

        /// <summary>
        /// Returns the shared prototype kernel for the given program and kernel name, or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// The returned kernel is shared across all callers with the same
        /// <paramref name="programId"/> / <paramref name="kernelName"/> pair.
        /// Concurrent <c>SetKernelArg</c> calls on this shared object are not thread-safe.
        /// Use <see cref="CreateKernelInstance"/> to obtain a per-instance kernel.
        /// </remarks>
        [Obsolete("Kernels from GetKernel are shared across instances and not safe for concurrent " +
                  "SetKernelArg calls. Use CreateKernelInstance for a per-instance kernel.")]
        public Kernel? GetKernel(string programId, string kernelName)
        {
            if (!IsInitialized) return null;
            if (!_kernels.TryGetValue(programId, out var dict)) return null;
            if (!dict.TryGetValue(kernelName, out var kernel)) return null;
            return kernel;
        }

        /// <summary>Blocks until all enqueued commands have finished.</summary>
        public void Finish()
        {
            if (IsInitialized && Queue.HasValue)
                Cl.Finish(Queue.Value);
        }

        /// <summary>
        /// Releases all kernels, programs, the command queue, and the context.
        /// Resets <see cref="IsInitialized"/> to <c>false</c>.
        /// After cleanup, <see cref="Initialize"/> may be called again to reinitialise.
        /// </summary>
        public void Cleanup()
        {
            foreach (var dict in _kernels.Values)
                foreach (var k in dict.Values)
                    if (k.IsValid()) Cl.ReleaseKernel(k);
            _kernels.Clear();

            foreach (var p in _programs.Values)
                if (p.IsValid()) Cl.ReleaseProgram(p);
            _programs.Clear();

            if (Queue.HasValue && Queue.Value.IsValid())
                Cl.ReleaseCommandQueue(Queue.Value);

            if (Context.HasValue && Context.Value.IsValid())
                Cl.ReleaseContext(Context.Value);

            Queue   = null;
            Context = null;
            Device  = null;
            IsInitialized = false;
        }
    }

    /// <summary>
    /// Abstract base class for OpenCL compute operations.
    /// <para>
    /// Each instance owns its own <see cref="Kernel"/> objects (created via
    /// <see cref="OpenCLEnvironment.CreateKernelInstance"/>), ensuring that concurrent
    /// instances of the same subclass do not share mutable kernel argument state.
    /// </para>
    /// <para>
    /// GPU buffers are tracked in two tiers:
    /// <list type="bullet">
    ///   <item><b>Persistent</b> — allocated once and released in <see cref="Dispose"/>.
    ///   Use for data that does not change across calls (e.g., configuration matrices).</item>
    ///   <item><b>Transient</b> — allocated per compute call and released by
    ///   <see cref="ReleaseTransient"/> at the end of each call.
    ///   Use for input/output data buffers.</item>
    /// </list>
    /// </para>
    /// </summary>
    public abstract class OpenCLComputation : IDisposable
    {
        private bool _disposed;
        private readonly Dictionary<string, Kernel> _ownedKernels = new Dictionary<string, Kernel>();
        private readonly List<IMem> _persistentMem = new List<IMem>();
        private readonly List<IMem> _transientMem  = new List<IMem>();

        /// <summary>Last OpenCL error code produced by this computation.</summary>
        protected ErrorCode Error;

        /// <summary>Unique program identifier used to look up compiled programs.</summary>
        protected string ProgramId { get; }

        /// <summary>Reference to the shared OpenCL environment.</summary>
        protected OpenCLEnvironment ClEnvironment => OpenCLEnvironment.Instance;

        /// <summary>Initialises the computation with the given program identifier.</summary>
        protected OpenCLComputation(string programId)
        {
            ProgramId = programId;
        }

        /// <summary>Returns the OpenCL C source code for this computation's kernel(s).</summary>
        protected abstract string GetKernelSource();

        /// <summary>Returns the names of all kernel functions defined in <see cref="GetKernelSource"/>.</summary>
        protected abstract string[] GetKernelNames();

        /// <summary>
        /// Ensures the OpenCL environment is ready, this computation's program is compiled,
        /// and per-instance kernel objects are created.
        /// </summary>
        private readonly object _kernelLock = new object();

        public bool EnsureInitialized(int platformIndex = 0, int deviceIndex = 0,
            DeviceType deviceType = DeviceType.Default)
        {
            if (!ClEnvironment.IsInitialized &&
                !ClEnvironment.Initialize(platformIndex, deviceIndex, deviceType))
                return false;

            if (!ClEnvironment.BuildProgram(ProgramId, GetKernelSource(), GetKernelNames()))
                return false;

            lock (_kernelLock)
            {
                foreach (var name in GetKernelNames())
                {
                    if (_ownedKernels.ContainsKey(name)) continue;
                    var k = ClEnvironment.CreateKernelInstance(ProgramId, name);
                    if (!k.HasValue) return false;
                    _ownedKernels[name] = k.Value;
                }
            }
            return true;
        }

        /// <summary>Loads an embedded text resource from the assembly.</summary>
        protected static string LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }

        // ── Buffer helpers ───────────────────────────────────────────────────────

        private IMem CreateBufferCore<T>(MemFlags flags, T[] data, List<IMem> target) where T : struct
        {
            int byteSize = Marshal.SizeOf<T>() * data.Length;
            if (byteSize == 0) return null;

            IMem mem;
            if ((flags & MemFlags.CopyHostPtr) != 0)
            {
                var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    mem = Cl.CreateBuffer(ClEnvironment.Context.Value, flags,
                        new IntPtr(byteSize), handle.AddrOfPinnedObject(), out Error);
                }
                finally { handle.Free(); }
            }
            else
            {
                mem = Cl.CreateBuffer(ClEnvironment.Context.Value, flags,
                    new IntPtr(byteSize), IntPtr.Zero, out Error);
            }

            if (Error == ErrorCode.Success) { target.Add(mem); return mem; }
            Console.WriteLine($"CreateBuffer failed: {Error}");
            return null;
        }

        private IMem CreateBufferWithSizeCore<T>(MemFlags flags, int elementCount, List<IMem> target) where T : struct
        {
            int byteSize = Marshal.SizeOf<T>() * elementCount;
            if (byteSize == 0) return null;

            var mem = Cl.CreateBuffer(ClEnvironment.Context.Value, flags,
                new IntPtr(byteSize), IntPtr.Zero, out Error);
            if (Error == ErrorCode.Success) { target.Add(mem); return mem; }
            Console.WriteLine($"CreateBuffer (sized) failed: {Error}");
            return null;
        }

        /// <summary>
        /// Allocates a GPU buffer with initial data, tracked as <b>persistent</b>
        /// (released only in <see cref="Dispose"/>).
        /// </summary>
        protected IMem AllocatePersistent<T>(MemFlags flags, T[] data) where T : struct
            => CreateBufferCore(flags, data, _persistentMem);

        /// <summary>
        /// Allocates an uninitialised GPU buffer of <paramref name="elementCount"/> elements,
        /// tracked as <b>persistent</b> (released only in <see cref="Dispose"/>).
        /// </summary>
        protected IMem AllocatePersistentWithSize<T>(MemFlags flags, int elementCount) where T : struct
            => CreateBufferWithSizeCore<T>(flags, elementCount, _persistentMem);

        /// <summary>
        /// Allocates a GPU buffer with initial data, tracked as <b>transient</b>
        /// (released by <see cref="ReleaseTransient"/> at the end of each compute call).
        /// </summary>
        protected IMem AllocateTransient<T>(MemFlags flags, T[] data) where T : struct
            => CreateBufferCore(flags, data, _transientMem);

        /// <summary>
        /// Allocates an uninitialised GPU buffer of <paramref name="elementCount"/> elements,
        /// tracked as <b>transient</b> (released by <see cref="ReleaseTransient"/>).
        /// </summary>
        protected IMem AllocateTransientWithSize<T>(MemFlags flags, int elementCount) where T : struct
            => CreateBufferWithSizeCore<T>(flags, elementCount, _transientMem);

        /// <summary>Reads <paramref name="data"/> back from a GPU buffer (blocking by default).</summary>
        protected bool ReadBuffer<T>(IMem buffer, T[] data, bool blocking = true) where T : struct
        {
            if (buffer == null || data == null || data.Length == 0) return false;
            int byteSize = Marshal.SizeOf<T>() * data.Length;
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Error = Cl.EnqueueReadBuffer(ClEnvironment.Queue.Value, buffer,
                    blocking ? Bool.True : Bool.False,
                    IntPtr.Zero, new IntPtr(byteSize),
                    handle.AddrOfPinnedObject(), 0, null, out _);
                if (Error != ErrorCode.Success)
                    Console.WriteLine($"ReadBuffer failed: {Error}");
                return Error == ErrorCode.Success;
            }
            finally { handle.Free(); }
        }

        // ── Kernel argument helpers ──────────────────────────────────────────────

        /// <summary>Binds a GPU memory object to the specified kernel argument slot.</summary>
        protected bool SetKernelArg(string kernelName, int index, IMem value)
        {
            if (!_ownedKernels.TryGetValue(kernelName, out var kernel))
            {
                Console.WriteLine($"Kernel '{kernelName}' not found. Call EnsureInitialized first.");
                return false;
            }
            Error = Cl.SetKernelArg(kernel, (uint)index, value);
            if (Error != ErrorCode.Success) Console.WriteLine($"SetKernelArg[{index}] failed: {Error}");
            return Error == ErrorCode.Success;
        }

        /// <summary>Binds a value-type scalar to the specified kernel argument slot.</summary>
        protected bool SetKernelArg<T>(string kernelName, int index, T value) where T : struct
        {
            if (!_ownedKernels.TryGetValue(kernelName, out var kernel))
            {
                Console.WriteLine($"Kernel '{kernelName}' not found. Call EnsureInitialized first.");
                return false;
            }
            Error = Cl.SetKernelArg(kernel, (uint)index, value);
            if (Error != ErrorCode.Success) Console.WriteLine($"SetKernelArg[{index}] failed: {Error}");
            return Error == ErrorCode.Success;
        }

        /// <summary>
        /// Binds multiple kernel arguments in order using a params array.
        /// Supports <see cref="IMem"/>, <see cref="int"/>, and <see cref="float"/>.
        /// </summary>
        protected bool SetKernelArgs(string kernelName, params object[] args)
        {
            if (!_ownedKernels.TryGetValue(kernelName, out var kernel))
            {
                Console.WriteLine($"Kernel '{kernelName}' not found. Call EnsureInitialized first.");
                return false;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is IMem mem)
                    Error = Cl.SetKernelArg(kernel, (uint)i, mem);
                else if (args[i] is int v)
                    Error = Cl.SetKernelArg(kernel, (uint)i, v);
                else if (args[i] is float f)
                    Error = Cl.SetKernelArg(kernel, (uint)i, f);
                else if (args[i] is uint u)
                    Error = Cl.SetKernelArg(kernel, (uint)i, u);
                else if (args[i] is short s)
                    Error = Cl.SetKernelArg(kernel, (uint)i, s);
                else if (args[i] is byte b)
                    Error = Cl.SetKernelArg(kernel, (uint)i, b);
                else
                {
                    Console.WriteLine($"SetKernelArg[{i}]: unsupported type {args[i]?.GetType()}");
                    return false;
                }

                if (Error != ErrorCode.Success)
                {
                    Console.WriteLine($"SetKernelArg[{i}] failed: {Error}");
                    return false;
                }
            }
            return true;
        }

        // ── Execution ────────────────────────────────────────────────────────────

        /// <summary>
        /// Enqueues an ND-range kernel dispatch and waits for completion (blocking).
        /// </summary>
        /// <param name="kernelName">Name of the kernel to execute.</param>
        /// <param name="globalWorkSize">Total number of work items per dimension.</param>
        /// <param name="localWorkSize">Work-group size, or <c>null</c> for driver-chosen.</param>
        protected bool ExecuteKernel(string kernelName, IntPtr[] globalWorkSize,
            IntPtr[] localWorkSize = null)
        {
            if (!_ownedKernels.TryGetValue(kernelName, out var kernel))
            {
                Console.WriteLine($"Kernel '{kernelName}' not found. Call EnsureInitialized first.");
                return false;
            }

            Error = Cl.EnqueueNDRangeKernel(ClEnvironment.Queue.Value, kernel,
                (uint)globalWorkSize.Length, null,
                globalWorkSize, localWorkSize, 0, null, out Event ev);
            if (Error != ErrorCode.Success)
            {
                Console.WriteLine($"EnqueueNDRangeKernel '{kernelName}' failed: {Error}");
                return false;
            }
            ev.Wait();
            return true;
        }

        /// <summary>
        /// Enqueues an ND-range kernel dispatch asynchronously.
        /// The returned <see cref="Task{T}"/> completes when the GPU finishes execution.
        /// The blocking wait is offloaded to a thread-pool thread so the calling thread
        /// remains free to perform CPU work in parallel.
        /// </summary>
        /// <param name="kernelName">Name of the kernel to execute.</param>
        /// <param name="globalWorkSize">Total number of work items per dimension.</param>
        /// <param name="localWorkSize">Work-group size, or <c>null</c> for driver-chosen.</param>
        protected Task<bool> ExecuteKernelAsync(string kernelName, IntPtr[] globalWorkSize,
            IntPtr[] localWorkSize = null)
        {
            return Task.Run(() => ExecuteKernel(kernelName, globalWorkSize, localWorkSize));
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        /// <summary>
        /// Releases all transient GPU buffers allocated since the last call to this method.
        /// Call this at the end of each compute operation to free per-call resources.
        /// Persistent buffers and owned kernels are unaffected.
        /// </summary>
        protected void ReleaseTransient()
        {
            foreach (var mem in _transientMem)
                if (mem != null) Cl.ReleaseMemObject(mem);
            _transientMem.Clear();
        }

        /// <summary>
        /// Releases all GPU resources: transient buffers, persistent buffers, and owned kernels.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            ReleaseTransient();
            foreach (var mem in _persistentMem)
                if (mem != null) Cl.ReleaseMemObject(mem);
            _persistentMem.Clear();
            foreach (var k in _ownedKernels.Values)
                if (k.IsValid()) Cl.ReleaseKernel(k);
            _ownedKernels.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>Finalizer fallback in case <see cref="Dispose"/> was not called.</summary>
        ~OpenCLComputation() => Dispose();
    }
}
