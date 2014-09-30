using System;
using System.Runtime.InteropServices;
using TCC;

namespace TCC.Test
{
	class MainClass
	{
		public class TestClass
		{
			public int Test { get; set; }
			public string Test2 { get; set; }
			public static string TestStatic { get; set; }
		}

		/*public class TestTClass : TestClass
		{
			public static string Test2Static { get; set; }
		}*/

		public static class TestStatic
		{
			public static int Test { get; set; }
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void TestDelegate(string msg);

		public static void SetterTest(string arg)
		{
			TestClass.TestStatic = arg;
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
			compiler.SetOutputType(CC.OutputType.Memory);

			//bind.BindClass(typeof(TestTClass));
			bind.BindClass(typeof(TestClass));
			//bind.BindClass(typeof(TestStatic));

			GCHandle gch = GCHandle.Alloc(test);

			compiler.AddSymbol("GetTestClass", (Func<IntPtr>)(() =>
			{
				return GCHandle.ToIntPtr(gch);
			}));

			compiler.AddSymbol("PrintInt", (Action<int>)((int x) =>
			{
				Console.WriteLine(x);
			}));

			compiler.AddSymbol("Print", (Action<string>)((string x) =>
			{
				Console.WriteLine(x);
			}));

			Delegate testd = DelegateWrapper.WrapDelegate((Action<string>)((string x) => { Console.WriteLine(x); }));
			testd.DynamicInvoke("Test!");

			//Delegate test2 = bind.GeneratePropertyGetter(typeof(TestClass), typeof(TestClass).GetProperty("Test"));
			//int ai = (int)test2.DynamicInvoke(GCHandle.ToIntPtr(gch));

			//TestDelegate xa = new TestDelegate(((string msg) => {Console.WriteLine(msg);}));

			compiler.SetErrorFunction(new CC.ErrorDelegate((string x) => { Console.WriteLine(x); }));

			compiler.CompileString(@"
int main()
{
	void *t = (void *)GetTestClass();
	Print(""Hello world"");
	PrintInt(testclass_get_test(t));
	//testtclass_set_test2static(""hello !static world"");
	PrintInt(42);
	Print(testclass_get_test2(t));
	testclass_set_test2(t, ""testing"");
	Print(testclass_get_test2(t));
	testclass_set_teststatic(""hello static world"");
	Print(""Finished"");
	gc_free(t);
	Print(""Freed"");
	return 0;
}
");
			compiler.Run(0, new string[] { });
		}
	}
}
