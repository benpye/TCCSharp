using NUnit.Framework;
using System;
using TCC;

namespace TCC.Test
{
	[TestFixture()]
	public class CCTest
	{
		private string TestGetSymbolCode = @"
			int times_two(int n)
			{
				return n + n;
			}";

		[Test()]
		public void GetSymbol()
		{
			CC compiler = new CC();

			// This must be called before any compilation
			compiler.SetOutputType(CC.OutputType.Memory);

			compiler.CompileString(TestGetSymbolCode);

			// Relocate the code to somewhere it can be executed from
			compiler.Relocate(CC.RelocateAuto);

			// Get the entry symbol
			var timesTwo = compiler.GetSymbol<Func<int, int>>("times_two");

			// Run the unmanaged code via the delegate
			var r = timesTwo(32);

			Assert.AreEqual(r, 64);
		}

		private string TestAddSymbolCode = @"
			int times_two(int n)
			{
				return add(n, n);
			}";

		[Test()]
		public void AddSymbol()
		{
			CC compiler = new CC();

			// This must be called before any compilation
			compiler.SetOutputType(CC.OutputType.Memory);

			compiler.CompileString(TestAddSymbolCode);

			compiler.AddSymbol("add", (Func<int, int, int>)((a, b) => {return a + b;}));

			// Relocate the code to somewhere it can be executed from
			compiler.Relocate(CC.RelocateAuto);

			// Get the entry symbol
			var timesTwo = compiler.GetSymbol<Func<int, int>>("times_two");

			// Run the unmanaged code via the delegate
			var r = timesTwo(32);

			Assert.AreEqual(r, 64);
		}

		private string TestSetErrorFunctionCode = @"
			#error ""Test""";

		[Test()]
		public void SetErrorFunction()
		{
			CC compiler = new CC();

			// This must be called before any compilation
			compiler.SetOutputType(CC.OutputType.Memory);

			string errorOut = "";
			compiler.SetErrorFunction(((string x) => { errorOut = x; }));

			try
			{
				compiler.CompileString(TestSetErrorFunctionCode);
			}
			catch(Exception){}

			Assert.AreEqual(errorOut, "<string>:2: error: #error \"Test\"");
		}

		[Test()]
		[ExpectedException(typeof(Exception))]
		public void CompileErrorException()
		{
			CC compiler = new CC();

			// This must be called before any compilation
			compiler.SetOutputType(CC.OutputType.Memory);

			compiler.CompileString(TestSetErrorFunctionCode);
		}

		private string TestRunCode = @"
			int main(char argc, char **argv)
			{
				return argc + argv[1][2];
			}";

		[Test()]
		public void Run()
		{
			CC compiler = new CC();

			// This must be called before any compilation
			compiler.SetOutputType(CC.OutputType.Memory);

			compiler.CompileString(TestRunCode);

			var r = compiler.Run(32, new string[]{ "Abc", "Def" });

			Assert.AreEqual(r, 134);
		}
	}
}

