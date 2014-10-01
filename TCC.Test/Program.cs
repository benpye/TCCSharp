using System;
using System.Runtime.InteropServices;
using TCC;

namespace TCC.Test
{
	class MainClass
	{
		public class TestingClass
		{
			public TestingClass()
			{
				TestString = "Default value";
			}

			public TestingClass(string x)
			{
				TestString = x;
			}

			~TestingClass()
			{
				Console.WriteLine("TestingClass destructor called");
			}

			public static TestingClass NewClass()
			{
				return new TestingClass();
			}

			public static void WriteLine(string msg)
			{
				Console.WriteLine(msg);
			}

			public string TestString { get; set; }

			public void PrintField()
			{
				Console.WriteLine(TestString);
			}
		}

		public static void Main(string[] args)
		{
			CC compiler = new CC();
			Binder bind = new Binder(compiler);

			bind.BindClass(typeof(TestingClass));

			compiler.SetOutputType(CC.OutputType.Memory);
			compiler.SetErrorFunction(Console.WriteLine);
			compiler.SetLibPath(AppDomain.CurrentDomain.BaseDirectory);

			compiler.CompileString(@"
int main()
{
	void *inst = (void *)testingclass_new_string(""Different string"");
	testingclass_printfield(inst);
	testingclass_set_teststring(inst, ""Another string"");
	testingclass_printfield(inst);
	testingclass_writeline(testingclass_get_teststring(inst));
	gc_free(inst);
	inst = (void *)testingclass_new();
	testingclass_printfield(inst);
	testingclass_set_teststring(inst, ""Another string"");
	testingclass_printfield(inst);
	for(int i = 0; i < 1000; i++)
		testingclass_writeline(testingclass_get_teststring(inst));
	gc_free(inst);
	testingclass_writeline(""Complete"");
	return 0;
}
");
			compiler.Run(0, new string[] { });
		}
	}
}
