using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCL.Net;
using OpenCL.Net.Extensions;

namespace VisionNet.Compute
{
    /// <summary>
    /// Thread-safe singleton that owns the shared OpenCL context, command queue,
    /// compiled programs, and kernel objects for the application lifetime.
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
        /// Compiles an OpenCL program and creates its kernels if not already done.
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

        /// <summary>Returns the compiled kernel identified by program and kernel name, or <c>null</c>.</summary>
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
    /// Manages buffer allocation, kernel argument binding, and kernel execution
    /// through the shared <see cref="OpenCLEnvironment"/> singleton.
    /// </summary>
    public abstract class OpenCLComputation : IDisposable
    {
        private bool _disposed;

        /// <summary>All memory objects allocated by this computation (released on <see cref="Cleanup"/>).</summary>
        protected List<IMem> MemObjects { get; } = new List<IMem>();

        /// <summary>Last OpenCL error code produced by this computation.</summary>
        protected ErrorCode Error;

        /// <summary>Unique program identifier used to look up compiled kernels.</summary>
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
        /// Ensures the OpenCL environment is ready and this computation's program is compiled.
        /// </summary>
        public bool EnsureInitialized(int platformIndex = 0, int deviceIndex = 0,
            DeviceType deviceType = DeviceType.Default)
        {
            if (!ClEnvironment.IsInitialized &&
                !ClEnvironment.Initialize(platformIndex, deviceIndex, deviceType))
                return false;
            return ClEnvironment.BuildProgram(ProgramId, GetKernelSource(), GetKernelNames());
        }

        // ── Buffer helpers ───────────────────────────────────────────────────────

        /// <summary>Allocates a GPU buffer and optionally copies <paramref name="data"/> to it.</summary>
        protected IMem CreateBuffer<T>(MemFlags flags, T[] data) where T : struct
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

            if (Error == ErrorCode.Success) { MemObjects.Add(mem); return mem; }
            Console.WriteLine($"CreateBuffer failed: {Error}");
            return null;
        }

        /// <summary>Allocates a GPU buffer of <paramref name="elementCount"/> uninitialized elements.</summary>
        protected IMem CreateBufferWithSize<T>(MemFlags flags, int elementCount) where T : struct
        {
            int byteSize = Marshal.SizeOf<T>() * elementCount;
            if (byteSize == 0) return null;

            var mem = Cl.CreateBuffer(ClEnvironment.Context.Value, flags,
                new IntPtr(byteSize), IntPtr.Zero, out Error);
            if (Error == ErrorCode.Success) { MemObjects.Add(mem); return mem; }
            Console.WriteLine($"CreateBuffer (sized) failed: {Error}");
            return null;
        }

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
            var kernel = ClEnvironment.GetKernel(ProgramId, kernelName);
            if (!kernel.HasValue) { Console.WriteLine($"Kernel '{kernelName}' not found."); return false; }
            Error = Cl.SetKernelArg(kernel.Value, (uint)index, value);
            if (Error != ErrorCode.Success) Console.WriteLine($"SetKernelArg[{index}] failed: {Error}");
            return Error == ErrorCode.Success;
        }

        /// <summary>Binds a value-type scalar to the specified kernel argument slot.</summary>
        protected bool SetKernelArg<T>(string kernelName, int index, T value) where T : struct
        {
            var kernel = ClEnvironment.GetKernel(ProgramId, kernelName);
            if (!kernel.HasValue) { Console.WriteLine($"Kernel '{kernelName}' not found."); return false; }
            Error = Cl.SetKernelArg(kernel.Value, (uint)index, value);
            if (Error != ErrorCode.Success) Console.WriteLine($"SetKernelArg[{index}] failed: {Error}");
            return Error == ErrorCode.Success;
        }

        // ── Execution ────────────────────────────────────────────────────────────

        /// <summary>
        /// Enqueues an ND-range kernel dispatch and waits for completion.
        /// </summary>
        /// <param name="kernelName">Name of the kernel to execute.</param>
        /// <param name="globalWorkSize">Total number of work items per dimension.</param>
        /// <param name="localWorkSize">Work-group size, or <c>null</c> for driver-chosen.</param>
        protected bool ExecuteKernel(string kernelName, IntPtr[] globalWorkSize,
            IntPtr[] localWorkSize = null)
        {
            var kernel = ClEnvironment.GetKernel(ProgramId, kernelName);
            if (!kernel.HasValue) { Console.WriteLine($"Kernel '{kernelName}' not found."); return false; }

            Error = Cl.EnqueueNDRangeKernel(ClEnvironment.Queue.Value, kernel.Value,
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

        // ── Cleanup ──────────────────────────────────────────────────────────────

        /// <summary>Releases all GPU memory objects allocated by this computation.</summary>
        protected virtual void Cleanup()
        {
            foreach (var mem in MemObjects)
                if (mem != null) Cl.ReleaseMemObject(mem);
            MemObjects.Clear();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed) { Cleanup(); _disposed = true; }
            GC.SuppressFinalize(this);
        }

        /// <summary>Finalizer fallback in case <see cref="Dispose"/> was not called.</summary>
        ~OpenCLComputation() => Cleanup();
    }
}
