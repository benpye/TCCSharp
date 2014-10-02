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

			// This must be called before any compilation
			compiler.SetOutputType(CC.OutputType.Memory);

			compiler.CompileString(ProgramCode);

			compiler.AddSymbol("add", (Func<int, int, int>)Add);

			compiler.Relocate(CC.RelocateAuto);

			var foo = compiler.GetSymbol<Func<int, int>>("foo");

			foo(32);
		}
	}
}
