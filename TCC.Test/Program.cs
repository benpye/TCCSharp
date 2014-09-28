using System;
using System.Runtime.InteropServices;
using TCC;

namespace TCC.Test
{
	class MainClass
	{
		private struct TestClass
		{
			public int Test { get; set; }
			public string Test2 { get; set; }
			public static string TestStatic { get; set; }
		}

		public static class TestStatic
		{
			public static int Test { get; set; }
		}

		public static void Main(string[] args)
		{
			CC compiler = new CC();
			Binder bind = new Binder(compiler);

			TestClass test = new TestClass();
			test.Test = 42;
			test.Test2 = "hello!";
			TestClass.TestStatic = "Static!";

			compiler.SetLibPath(AppDomain.CurrentDomain.BaseDirectory);

			bind.BindClass(typeof(TestClass));
			bind.BindClass(typeof(TestStatic));

			GCHandle gch = GCHandle.Alloc(test);

			compiler.AddSymbol("GetTestClass", (Func<IntPtr>)(() => {
				return GCHandle.ToIntPtr(gch);
			}));

			compiler.AddSymbol("PrintInt", (Action<int>)((int x) => {
				Console.WriteLine(x);
			}));

			compiler.AddSymbol("Print", (Action<string>)((string x) => {
				Console.WriteLine(x);
			}));

			compiler.CompileString(@"
int main()
{
	void *t = (void *)GetTestClass();
	PrintInt(testclass_get_test(t));
	Print(testclass_get_test2(t));
	testclass_set_test2(t, ""testing"");
	Print(testclass_get_test2(t));
	testclass_set_teststatic(""hello static world"");
	gc_free(t);
	return 0;
}
");
			compiler.Run(0, new string[]{ });
		}
	}
}
