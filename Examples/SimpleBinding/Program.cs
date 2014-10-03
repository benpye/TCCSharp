using System;
using TCC;

// Port of TCC's libtcc_test.c test application

namespace TCC.Examples.SimpleBinding
{
	class MainClass
	{
		public static int Add(int a, int b)
		{
			return a + b;
		}

		public static string ProgramCode = @"
			int fib(int n)
			{
				if (n <= 2)
					return 1;
				else
					return fib(n-1) + fib(n-2);
			}

			int foo(int n)
			{
				printf(""Hello World!\n"");
				printf(""fib(%d) = %d\n"", n, fib(n));
				printf(""add(%d, %d) = %d\n"", n, 2 * n, add(n, 2 * n));
				return 0;
			}";

		public static void Main(string[] args)
		{
			CC compiler = new CC();

			// If libtcc1.a is not in the default location where is it
			if (args.Length > 0)
				compiler.SetLibPath(args[0]);

			// Pipe any tcc errors straight to Console.WriteLine
			compiler.SetErrorFunction(Console.WriteLine);

			// This must be called before any compilation
			compiler.SetOutputType(CC.OutputType.Memory);

			compiler.CompileString(ProgramCode);

			// As a test we add a symbol the compiled program can use. You may 
			// also add a library with CC.AddLibrary and use symbols from there.
			compiler.AddSymbol("add", (Func<int, int, int>)Add);

			// Relocate the code to somewhere it can be executed from
			compiler.Relocate(CC.RelocateAuto);

			// Get the entry symbol
			var foo = compiler.GetSymbol<Func<int, int>>("foo");

			// Run the unmanaged code via the delegate
			foo(32);
		}
	}
}
