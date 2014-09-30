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
			public string TestField;
			public TestClass2 TestClassTwo { get; set; }
		}

		/*public class TestTClass : TestClass
		{
			public static string Test2Static { get; set; }
		}*/

		public class TestClass2
		{
			public int Test { get; set; }
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void TestDelegate(string msg);

		public static int GetterTest(IntPtr arg)
		{
			return ((TestClass)GCHandle.FromIntPtr(arg).Target).Test;
		}

		public static void SetterTest(IntPtr arg, int test)
		{
			((TestClass)GCHandle.FromIntPtr(arg).Target).Test = test;
		}

		public static IntPtr GetHandle(IntPtr o)
		{
			return GCHandle.ToIntPtr(GCHandle.Alloc(((TestClass)GCHandle.FromIntPtr(o).Target).TestClassTwo));
		}

		public static void SetHandle(IntPtr o, IntPtr b)
		{
			((TestClass)GCHandle.FromIntPtr(o).Target).TestClassTwo = (TestClass2)(GCHandle.FromIntPtr(b).Target);
		}

		public static void Main(string[] args)
		{
			CC compiler = new CC();
			Binder bind = new Binder(compiler);

			TestClass test = new TestClass();
			test.Test = 42;
			test.Test2 = "hello!";
			test.TestField = "Hi!";
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

			SetterTest(GCHandle.ToIntPtr(gch), 50);
			Console.WriteLine(GetterTest(GCHandle.ToIntPtr(gch)));

			compiler.SetErrorFunction(Console.WriteLine);

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
	Print(testclass_get_testfield(t));
	testclass_set_testfield(t, ""New!"");
	Print(testclass_get_testfield(t));
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
