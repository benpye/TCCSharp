using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Collections.Generic;

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

		private IntPtr s;
		private List<Delegate> delegateCache;
		private Native.NativeErrorDelegate errorDelegate;
		private Action<string> errorHandler;

		public CC()
		{
			s = Native.tcc_new();
			if (s == IntPtr.Zero)
				throw new Exception("Could not create native TCC state");

			// We have to cache all delegates passed to native code else the GC
			// will collect them!
			delegateCache = new List<Delegate>();
			errorDelegate = DispatchError;
			Native.tcc_set_error_func(s, IntPtr.Zero, errorDelegate);
		}

		~CC()
		{
			Native.tcc_delete(s);
		}

		private void DispatchError(IntPtr opaque, string msg)
		{
			if (errorHandler != null)
				errorHandler(msg);
		}

		/// <summary>
		/// Set CONFIG_TCCDIR at runtime.
		/// </summary>
		/// <param name="path">Path.</param>
		public void SetLibPath(string path)
		{
			Native.tcc_set_lib_path(s, path);
		}

		/// <summary>
		/// Set error/warning display callback.
		/// </summary>
		/// <param name="function">Callback function.</param>
		public void SetErrorFunction(Action<string> function)
		{
			errorHandler = function;
		}

		/// <summary>
		/// Set options as from command line (multiple supported).
		/// </summary>
		/// <param name="options">Option string.</param>
		public void SetOptions(string options)
		{
			int r = Native.tcc_set_options(s, options);
			if (r < 0)
				throw new Exception();
		}

		/// <summary>
		/// Adds an include path.
		/// </summary>
		/// <param name="path">Path.</param>
		public void AddIncludePath(string path)
		{
			int r = Native.tcc_add_include_path(s, path);
			if (r < 0)
				throw new Exception();
		}

		/// <summary>
		/// Adds a system include path.
		/// </summary>
		/// <param name="path">Path.</param>
		public void AddSysIncludePath(string path)
		{
			int r = Native.tcc_add_sysinclude_path(s, path);
			if (r < 0)
				throw new Exception();
		}

		/// <summary>
		/// Defines the preprocessor symbol symbol. Can put optional value.
		/// </summary>
		/// <param name="symbol">Symbol name.</param>
		/// <param name="value">Value.</param>
		public void DefineSymbol(string symbol, string value = null)
		{
			Native.tcc_define_symbol(s, symbol, value);
		}

		/// <summary>
		/// Undefines the preprocessor symbol symbol.
		/// </summary>
		/// <param name="symbol">Symbol name.</param>
		public void UndefineSymbol(string symbol)
		{
			Native.tcc_undefine_symbol(s, symbol);
		}

		/// <summary>
		/// Add a file (C file, dll, object, library, ld script)
		/// </summary>
		/// <param name="filename">Filename.</param>
		public void AddFile(string filename)
		{
			int r = Native.tcc_add_file(s, filename);
			if (r < 0)
				throw new Exception();
		}

		/// <summary>
		/// Compile a string containing C source code.
		/// </summary>
		/// <param name="buffer">Code buffer.</param>
		public void CompileString(string buffer)
		{
			int r = Native.tcc_compile_string(s, buffer);
			if (r < 0)
				throw new Exception();
		}

		/// <summary>
		/// Sets the output type. Must be called before any compilation.
		/// </summary>
		/// <param name="type">Output type.</param>
		public void SetOutputType(OutputType type)
		{
			int r = Native.tcc_set_output_type(s, type);
			if (r < 0)
				throw new Exception();
		}

		/// <summary>
		/// Adds a library path. Equivilent to -Lpath
		/// </summary>
		/// <param name="path">Path.</param>
		public void AddLibraryPath(string path)
		{
			int r = Native.tcc_add_library_path(s, path);
			if (r < 0)
				throw new Exception();
		}

		/// <summary>
		/// Adds a library. Equivilent to -llibrary.
		/// </summary>
		/// <param name="library">Library name.</param>
		public void AddLibrary(string library)
		{
			int r = Native.tcc_add_library(s, library);
			if (r < 0)
				throw new Exception();
		}

		/// <summary>
		/// Adds a symbol to the program. Will wrap the delegate so it doesn't require
		/// any UnmanagedFunctionPointer attribute and can be Action<> or Func<>
		/// </summary>
		/// <param name="name">Symbol name.</param>
		/// <param name="method">Method.</param>
		public void AddSymbol(string name, Delegate method)
		{
			// We normally wrap delegates as a usability improvement, without doing this
			// we cannot allow for generic delegates (Task, Action), nor can we ensure Cdecl
			var d = DelegateWrapper.GetWrappedDelegate(method);
			delegateCache.Add(d);
			int r = Native.tcc_add_symbol(s, name, d);
			if (r < 0)
				throw new Exception();
		}

		/// <summary>
		/// Adds a symbol to the program. Must not have generic arguments and must have
		/// the UnmanagedFunctionPointer(CallingConvention.Cdecl) attribute
		/// </summary>
		/// <param name="name">Symbol name.</param>
		/// <param name="method">Method.</param>
		public void AddSymbolNative(string name, Delegate method)
		{
			// When using DynamicMethod we are able to use the correct Delegate type first
			// time, this may also be useful if performance is important, though TCC is likely
			// never going to be super high performance
			delegateCache.Add(method);
			int r = Native.tcc_add_symbol(s, name, method);
			if (r < 0)
				throw new Exception();
		}


		/// <summary>
		/// Output an executable, library or object file. Do not call Relocate first.
		/// </summary>
		/// <param name="filename">Filename.</param>
		public void OutputFile(string filename)
		{
			int r = Native.tcc_output_file(s, filename);
			if (r < 0)
				throw new Exception();
		}

		/// <summary>
		/// Link and run the main() function and return its value. Do not call Relocate first.
		/// </summary>
		/// <param name="argc">Argument count.</param>
		/// <param name="argv">Argument strings.</param>
		public int Run(int argc, string[] argv)
		{
			return Native.tcc_run(s, argc, argv);
		}

		/// <summary>
		/// Do all relocation, needed before using GetSymbol.
		/// RelocateAuto may be used to allocate and manage memory interally.
		/// RelocateSize may be used to get the size of memory required.
		/// Any other pointer is assumed to be the pointer to a block of memory.
		/// </summary>
		/// <param name="ptr">Memory pointer.</param>
		public int Relocate(IntPtr ptr)
		{
			int r = Native.tcc_relocate(s, ptr);

			if (r < 0)
				throw new Exception();

			return r;
		}

		/// <summary>
		/// Gets the symbol from native code, allowing calls to the compiled code.
		/// </summary>
		/// <returns>The delegate.</returns>
		/// <param name="name">Symbol name.</param>
		/// <typeparam name="T">Type of delegate returned.</typeparam>
		public T GetSymbol<T>(string name)
		{
			IntPtr funcPtr = Native.tcc_get_symbol(s, name);
			if (funcPtr == IntPtr.Zero)
				throw new Exception();

			// We use our own delegate vs the default marshal behaviour so we can
			// use generic delegate types.
			return (T)(object)DelegateWrapper.GetCalliDelegate(funcPtr, typeof(T));
		}
	}
}

