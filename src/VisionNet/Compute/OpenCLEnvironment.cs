using System;
using System.Collections.Generic;
using OpenCL.Net;
using System.Runtime.InteropServices;
using System.Linq;
using OpenCL.Net.Extensions;

namespace VisionNet.Compute
{
    /// <summary>
    /// OpenCL环境单例类，管理全局OpenCL资源
    /// </summary>
    public class OpenCLEnvironment
    {
        private static OpenCLEnvironment _instance;
        private static readonly object _lock = new object();

        // OpenCL核心对象
        public Context? Context { get; private set; }
        public CommandQueue? Queue { get; private set; }
        public Device? Device { get; private set; }

        // 程序和内核字典
        private Dictionary<string, Program> _programs;
        private Dictionary<string, Dictionary<string, Kernel>> _kernels;

        // 错误代码
        public ErrorCode Error;

        // 是否已初始化
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static OpenCLEnvironment Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new OpenCLEnvironment();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private OpenCLEnvironment()
        {
            _programs = new Dictionary<string, Program>();
            _kernels = new Dictionary<string, Dictionary<string, Kernel>>();
            IsInitialized = false;
        }

        /// <summary>
        /// 初始化OpenCL环境
        /// </summary>
        /// <param name="platformIndex">平台索引</param>
        /// <param name="deviceIndex">设备索引</param>
        /// <param name="deviceType">设备类型</param>
        /// <returns>是否初始化成功</returns>
        public bool Initialize(int platformIndex = 0, int deviceIndex = 0, DeviceType deviceType = DeviceType.Default)
        {
            if (IsInitialized)
                return true;

            try
            {
                // 获取平台
                var platforms = Cl.GetPlatformIDs(out Error);
                if (Error != ErrorCode.Success || platforms == null || platforms.Count() == 0 || platformIndex < 0 || platformIndex >= platforms.Count())
                {
                    Console.WriteLine("未找到可用的OpenCL平台或平台索引越界");
                    return false;
                }

                // 获取设备
                var devices = Cl.GetDeviceIDs(platforms[platformIndex], deviceType, out Error);
                if (Error != ErrorCode.Success || devices == null || devices.Count() == 0 || deviceIndex < 0 || deviceIndex >= devices.Count())
                {
                    Console.WriteLine("未找到可用的OpenCL设备或设备索引越界");
                    return false;
                }

                Device = devices[deviceIndex];

                // 创建上下文
                Context = Cl.CreateContext(null, 1, new[] { Device.Value }, null, IntPtr.Zero, out Error);
                if (Error != ErrorCode.Success || !Context.IsValid())
                {
                    Console.WriteLine("创建OpenCL上下文失败");
                    return false;
                }

                // 创建命令队列
                Queue = Cl.CreateCommandQueue(Context.Value, Device.Value, CommandQueueProperties.None, out Error);
                if (Error != ErrorCode.Success || !Queue.IsValid())
                {
                    Console.WriteLine("创建OpenCL命令队列失败");
                    return false;
                }

                IsInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("初始化OpenCL环境时出错: " + ex.Message);
                Cleanup();
                return false;
            }
        }

        /// <summary>
        /// 编译OpenCL程序
        /// </summary>
        /// <param name="programId">程序ID</param>
        /// <param name="kernelSource">内核源代码</param>
        /// <param name="kernelNames">内核函数名数组</param>
        /// <returns>是否编译成功</returns>
        public bool BuildProgram(string programId, string kernelSource, string[] kernelNames)
        {
            if (!IsInitialized)
            {
                Console.WriteLine("OpenCL环境未初始化");
                return false;
            }

            if (_programs.ContainsKey(programId))
            {
                // 已存在则直接返回
                return true;
            }

            try
            {
                // 创建程序
                Program program = Cl.CreateProgramWithSource(Context.Value, 1, new[] { kernelSource }, null, out Error);
                if (Error != ErrorCode.Success || !program.IsValid())
                {
                    Console.WriteLine($"创建OpenCL程序失败: {Error}");
                    return false;
                }

                // 编译程序
                Error = Cl.BuildProgram(program, 1, new[] { Device.Value }, string.Empty, null, IntPtr.Zero);
                if (Error != ErrorCode.Success)
                {
                    var log = Cl.GetProgramBuildInfo(program, Device.Value, ProgramBuildInfo.Log, out _).ToString();
                    Console.WriteLine($"编译OpenCL程序失败: {Error}\n编译日志:\n{log}");
                    Cl.ReleaseProgram(program);
                    return false;
                }

                _programs.Add(programId, program);
                _kernels.Add(programId, new Dictionary<string, Kernel>());

                // 创建内核
                foreach (string kernelName in kernelNames)
                {
                    Kernel kernel = Cl.CreateKernel(program, kernelName, out Error);
                    if (Error != ErrorCode.Success || !kernel.IsValid())
                    {
                        Console.WriteLine($"创建内核 '{kernelName}' 失败: {Error}");
                        // 释放已创建的内核和程序
                        foreach (var k in _kernels[programId].Values)
                            Cl.ReleaseKernel(k);
                        _kernels.Remove(programId);
                        Cl.ReleaseProgram(program);
                        _programs.Remove(programId);
                        return false;
                    }

                    _kernels[programId].Add(kernelName, kernel);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"编译程序 '{programId}' 时出错: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取内核
        /// </summary>
        /// <param name="programId">程序ID</param>
        /// <param name="kernelName">内核名称</param>
        /// <returns>内核对象</returns>
        public Kernel? GetKernel(string programId, string kernelName)
        {
            if (!IsInitialized || !_programs.ContainsKey(programId) || !_kernels[programId].ContainsKey(kernelName))
            {
                return null;
            }

            return _kernels[programId][kernelName];
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            foreach (var programKernels in _kernels.Values)
            {
                foreach (var kernel in programKernels.Values)
                {
                    if (kernel.IsValid())
                        Cl.ReleaseKernel(kernel);
                }
            }
            _kernels.Clear();

            foreach (var program in _programs.Values)
            {
                if (program.IsValid())
                    Cl.ReleaseProgram(program);
            }
            _programs.Clear();

            if (Queue.HasValue)
            {
                Cl.ReleaseCommandQueue(Queue.Value);
            }

            if (Context.HasValue)
            {
                Cl.ReleaseContext(Context.Value);
            }
            IsInitialized = false;
        }

        /// <summary>
        /// 等待所有命令完成
        /// </summary>
        public void Finish()
        {
            if (IsInitialized)
            {
                Cl.Finish(Queue.Value);
            }
        }
    }

    /// <summary>
    /// OpenCL计算基础类，用于实现OpenCL计算功能
    /// </summary>
    public abstract class OpenCLComputation : IDisposable
    {
        // 内存对象
        protected List<IMem> MemObjects { get; private set; }

        // 错误代码
        protected ErrorCode Error;

        // 程序ID
        protected string ProgramId { get; private set; }

        // OpenCL环境引用
        protected OpenCLEnvironment CLEnvironment => OpenCLEnvironment.Instance;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="programId">程序ID</param>
        public OpenCLComputation(string programId)
        {
            MemObjects = new List<IMem>();
            ProgramId = programId;
        }

        /// <summary>
        /// 确保OpenCL环境初始化
        /// </summary>
        /// <returns>是否初始化成功</returns>
        public bool EnsureInitialized(int platformIndex = 0, int deviceIndex = 0, DeviceType deviceType = DeviceType.Default)
        {
            if (!CLEnvironment.IsInitialized)
            {
                if (!CLEnvironment.Initialize(platformIndex, deviceIndex, deviceType))
                {
                    return false;
                }
            }

            // 编译程序和内核
            return CLEnvironment.BuildProgram(ProgramId, GetKernelSource(), GetKernelNames());
        }

        /// <summary>
        /// 获取内核源代码
        /// </summary>
        /// <returns>OpenCL C内核代码</returns>
        protected abstract string GetKernelSource();

        /// <summary>
        /// 获取内核函数名称数组
        /// </summary>
        /// <returns>内核函数名数组</returns>
        protected abstract string[] GetKernelNames();

        /// <summary>
        /// 创建内存对象
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="flags">内存标志</param>
        /// <param name="data">数据</param>
        /// <returns>内存对象</returns>
        protected IMem CreateBuffer<T>(MemFlags flags, T[] data = null) where T : struct
        {
            if (!CLEnvironment.Context.IsValid())
            {
                Console.WriteLine("OpenCL上下文未初始化");
                return null;
            }

            if (data == null)
            {
                return CreateBufferWithSize<T>(flags, 0);
            }

            int size = Marshal.SizeOf<T>() * data.Length;
            if (size == 0)
            {
                Console.WriteLine("缓冲区大小为0");
                return null;
            }

            IMem memObject = null;

            if ((flags & MemFlags.CopyHostPtr) != 0 && data != null)
            {
                var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    memObject = Cl.CreateBuffer(CLEnvironment.Context.Value, flags, new IntPtr(size), dataHandle.AddrOfPinnedObject(), out Error);
                }
                finally
                {
                    dataHandle.Free();
                }
            }
            else
            {
                memObject = Cl.CreateBuffer(CLEnvironment.Context.Value, flags, new IntPtr(size), IntPtr.Zero, out Error);
            }

            if (Error == ErrorCode.Success && memObject != null)
            {
                MemObjects.Add(memObject);
            }
            else
            {
                Console.WriteLine($"创建缓冲区失败: {Error}");
            }

            return memObject;
        }

        /// <summary>
        /// 创建指定大小的内存对象
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="flags">内存标志</param>
        /// <param name="elementCount">元素数量</param>
        /// <returns>内存对象</returns>
        protected IMem CreateBufferWithSize<T>(MemFlags flags, int elementCount) where T : struct
        {
            if (CLEnvironment.Context == null)
            {
                Console.WriteLine("OpenCL上下文未初始化");
                return null;
            }

            int size = Marshal.SizeOf<T>() * elementCount;
            if (size == 0)
            {
                Console.WriteLine("缓冲区大小为0");
                return null;
            }

            IMem memObject = Cl.CreateBuffer(CLEnvironment.Context.Value, flags, new IntPtr(size), IntPtr.Zero, out Error);

            if (Error == ErrorCode.Success && memObject != null)
            {
                MemObjects.Add(memObject);
            }
            else
            {
                Console.WriteLine($"创建缓冲区失败: {Error}");
            }

            return memObject;
        }

        /// <summary>
        /// 写入缓冲区
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="data">数据</param>
        /// <param name="blocking">是否阻塞</param>
        /// <returns>是否成功</returns>
        protected bool WriteBuffer<T>(IMem buffer, T[] data, bool blocking = true) where T : struct
        {
            if (buffer == null || data == null || data.Length == 0)
            {
                Console.WriteLine("写入缓冲区参数无效");
                return false;
            }
            int size = Marshal.SizeOf<T>() * data.Length;
            var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Error = Cl.EnqueueWriteBuffer(CLEnvironment.Queue.Value, buffer, blocking ? Bool.True : Bool.False,
                    IntPtr.Zero, new IntPtr(size), dataHandle.AddrOfPinnedObject(),
                    0, null, out Event _);
                if (Error != ErrorCode.Success)
                {
                    Console.WriteLine($"写入缓冲区失败: {Error}");
                }
                return Error == ErrorCode.Success;
            }
            finally
            {
                dataHandle.Free();
            }
        }

        /// <summary>
        /// 读取缓冲区
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="buffer">源缓冲区</param>
        /// <param name="data">数据</param>
        /// <param name="blocking">是否阻塞</param>
        /// <returns>是否成功</returns>
        protected bool ReadBuffer<T>(IMem buffer, T[] data, bool blocking = true) where T : struct
        {
            if (buffer == null || data == null || data.Length == 0)
            {
                Console.WriteLine("读取缓冲区参数无效");
                return false;
            }
            int size = Marshal.SizeOf<T>() * data.Length;
            var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Error = Cl.EnqueueReadBuffer(CLEnvironment.Queue.Value, buffer, blocking ? Bool.True : Bool.False,
                    IntPtr.Zero, new IntPtr(size), dataHandle.AddrOfPinnedObject(),
                    0, null, out Event _);
                if (Error != ErrorCode.Success)
                {
                    Console.WriteLine($"读取缓冲区失败: {Error}");
                }
                return Error == ErrorCode.Success;
            }
            finally
            {
                dataHandle.Free();
            }
        }

        /// <summary>
        /// 执行内核
        /// </summary>
        /// <param name="kernelName">内核名称</param>
        /// <param name="globalWorkSize">全局工作组大小</param>
        /// <param name="localWorkSize">局部工作组大小</param>
        /// <returns>是否成功</returns>
        protected bool ExecuteKernel(string kernelName, IntPtr[] globalWorkSize, IntPtr[] localWorkSize = null)
        {
            Kernel? kernel = CLEnvironment.GetKernel(ProgramId, kernelName);
            if (kernel == null)
            {
                Console.WriteLine($"内核 '{kernelName}' 不存在");
                return false;
            }
            if (globalWorkSize == null || globalWorkSize.Length == 0)
            {
                Console.WriteLine("全局工作组大小无效");
                return false;
            }

            Error = Cl.EnqueueNDRangeKernel(CLEnvironment.Queue.Value, kernel.Value, (uint)globalWorkSize.Count(), null,
                globalWorkSize, localWorkSize, 0, null, out Event finishEvent);

            if (Error != ErrorCode.Success)
            {
                Console.WriteLine($"执行内核 '{kernelName}' 失败: {Error}");
            }
            finishEvent.Wait();
            return Error == ErrorCode.Success;
        }

        /// <summary>
        /// 设置内核参数，使用泛型自动识别类型
        /// </summary>
        /// <param name="kernelName">内核名称</param>
        /// <param name="index">参数索引</param>
        /// <param name="value">参数值</param>
        /// <returns>是否成功</returns>
        protected bool SetKernelArg(string kernelName, int index, IMem value)
        {
            Kernel? kernel = CLEnvironment.GetKernel(ProgramId, kernelName);
            if (!kernel.HasValue)
            {
                Console.WriteLine($"内核 '{kernelName}' 不存在");
                return false;
            }

            // 特殊处理IMem类型
            if (value is IMem memObject)
            {
                Error = Cl.SetKernelArg(kernel.Value, (uint)index, memObject);
                if (Error != ErrorCode.Success)
                {
                    Console.WriteLine($"设置内核参数失败: {Error}");
                }
                return Error == ErrorCode.Success;
            }

            // 使用Marshal处理所有值类型
            int size = Marshal.SizeOf(value);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, ptr, false);
                Error = Cl.SetKernelArg(kernel.Value, (uint)index, ptr);
                if (Error != ErrorCode.Success)
                {
                    Console.WriteLine($"设置内核参数失败: {Error}");
                }
                return Error == ErrorCode.Success;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// 设置内核参数，使用泛型自动识别类型
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="kernelName">内核名称</param>
        /// <param name="index">参数索引</param>
        /// <param name="value">参数值</param>
        /// <returns>是否成功</returns>
        protected bool SetKernelArg<T>(string kernelName, int index, T value) where T : struct
        {
            Kernel? kernel = CLEnvironment.GetKernel(ProgramId, kernelName);
            if (!kernel.HasValue)
            {
                Console.WriteLine($"内核 '{kernelName}' 不存在");
                return false;
            }
            Error = Cl.SetKernelArg(kernel.Value, (uint)index, value);
            if (Error != ErrorCode.Success)
            {
                Console.WriteLine($"设置内核参数失败: {Error}");
            }
            return Error == ErrorCode.Success;
        }

        /// <summary>
        /// 等待所有命令完成
        /// </summary>
        protected void Finish()
        {
            CLEnvironment.Finish();
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        protected virtual void Cleanup()
        {
            foreach (var memObject in MemObjects)
            {
                if (memObject != null)
                    Cl.ReleaseMemObject(memObject);
            }
            MemObjects.Clear();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Cleanup();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~OpenCLComputation()
        {
            Cleanup();
        }
    }
}