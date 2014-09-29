using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace TCC
{
	public class CC
	{
		public enum OutputType
		{
			Memory = 0,
			Exe = 1,
			Dll = 2,
			Obj = 3,
			Preprocess = 4
		}

		public static readonly IntPtr RelocateSize = (IntPtr)0;
		public static readonly IntPtr RelocateAuto = (IntPtr)1;

		private static class Native
		{
			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern IntPtr tcc_new();

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern void tcc_delete(IntPtr state);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern void tcc_set_lib_path(IntPtr state, string path);

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			public delegate void NativeErrorDelegate(IntPtr opaque, string msg);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern void tcc_set_error_func(IntPtr state, IntPtr error_opaque, NativeErrorDelegate error_func);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_set_options(IntPtr state, string str);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_add_include_path(IntPtr state, string pathname);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_add_sysinclude_path(IntPtr state, string pathname);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern void tcc_define_symbol(IntPtr state, string sym, string value);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern void tcc_undefine_symbol(IntPtr state, string sym);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_add_file(IntPtr state, string filename);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_compile_string(IntPtr state, string buf);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_set_output_type(IntPtr state, OutputType output_type);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_add_library_path(IntPtr state, string pathname);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_add_library(IntPtr state, string libraryname);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_add_symbol(IntPtr state, string name, Delegate val);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_output_file(IntPtr state, string filename);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_run(IntPtr state, int argc, string[] argv);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int tcc_relocate(IntPtr state, IntPtr ptr);

			[DllImport("libtcc.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern IntPtr tcc_get_symbol(IntPtr state, string name);
		}

		public delegate void ErrorDelegate(string message);

		private IntPtr s;

		public CC()
		{
			s = Native.tcc_new();
		}

		~CC()
		{
			Native.tcc_delete(s);
		}

		public void SetLibPath(string path)
		{
			Native.tcc_set_lib_path(s, path);
		}

		public void SetErrorFunction(ErrorDelegate function)
		{
			Native.tcc_set_error_func(s, IntPtr.Zero, ((IntPtr opaque, string msg) => function(msg)));
		}

		public int SetOptions(string options)
		{
			return Native.tcc_set_options(s, options);
		}

		public int AddIncludePath(string path)
		{
			return Native.tcc_add_include_path(s, path);
		}

		public int AddSysIncludePath(string path)
		{
			return Native.tcc_add_sysinclude_path(s, path);
		}

		public void DefineSymbol(string symbol, string value)
		{
			Native.tcc_define_symbol(s, symbol, value);
		}

		public void UndefineSymbol(string symbol)
		{
			Native.tcc_undefine_symbol(s, symbol);
		}

		public int AddFile(string filename)
		{
			return Native.tcc_add_file(s, filename);
		}

		public int CompileString(string buffer)
		{
			return Native.tcc_compile_string(s, buffer);
		}

		public int SetOutputType(OutputType type)
		{
			return Native.tcc_set_output_type(s, type);
		}

		public int AddLibraryPath(string path)
		{
			return Native.tcc_add_library_path(s, path);
		}

		public int AddLibrary(string library)
		{
			return Native.tcc_add_library(s, library);
		}

		public int AddSymbolDynamic(string name, Delegate method)
		{
			return Native.tcc_add_symbol(s, name, method);
		}

		public int AddSymbol(string name, Delegate method)
		{
			return Native.tcc_add_symbol(s, name, DelegateWrapper.WrapDelegate(method));
		}

		public int OutputFile(string filename)
		{
			return Native.tcc_output_file(s, filename);
		}

		public int Run(int argc, string[] argv)
		{
			return Native.tcc_run(s, argc, argv);
		}

		public int Relocate(IntPtr ptr)
		{
			return Native.tcc_relocate(s, ptr);
		}

		public T GetSymbol<T>(string name)
		{
			IntPtr funcPtr = Native.tcc_get_symbol(s, name);
			if(funcPtr == IntPtr.Zero)
				throw new NullReferenceException();

			return (T)(object)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T));
		}
	}
}

