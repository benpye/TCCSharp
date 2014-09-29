using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace TCC
{
	public static class DelegateWrapper
	{
		private static AssemblyBuilder assemblyBuilder;
		private static ModuleBuilder moduleBuilder;

		static DelegateWrapper()
		{
			AssemblyName assemblyName = new AssemblyName("DelegateWrapperAssembly");
			assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
			moduleBuilder = assemblyBuilder.DefineDynamicModule("DelegateWrapperModule");
		}

		public static Type GenerateDelegateType(Type returnType, Type[] parameterTypes)
		{
			TypeBuilder tb = moduleBuilder.DefineType("DelegateWrapperDelegate" + Guid.NewGuid(),
				TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass, typeof(MulticastDelegate));

			var constructor = tb.DefineConstructor(MethodAttributes.RTSpecialName |
				MethodAttributes.SpecialName | MethodAttributes.Public | MethodAttributes.HideBySig,
				CallingConventions.Standard, new Type[] { typeof(object), typeof(IntPtr) });
			constructor.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

			var invoke = tb.DefineMethod("Invoke", MethodAttributes.Public |
				MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig,
				returnType, parameterTypes);
			invoke.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

			ConstructorInfo attributeCi = typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new Type[] { typeof(CallingConvention) });
			CustomAttributeBuilder cab = new CustomAttributeBuilder(attributeCi, new object[]{ CallingConvention.Cdecl });
			tb.SetCustomAttribute(cab);

			return tb.CreateType();
		}

		public static Delegate WrapDelegate(Delegate method)
		{
			Type delegateType = method.GetType();
			MethodInfo invokeInfo = delegateType.GetMethod("Invoke");
			Type invokeReturnType = invokeInfo.ReturnType;
			ParameterInfo[] invokeParameters = invokeInfo.GetParameters();
			Type[] invokeParameterTypes = new Type[invokeParameters.Length];

			for(int i = 0; i < invokeParameters.Length; i++)
			{
				invokeParameterTypes[i] = invokeParameters[i].ParameterType;
			}

			var type = GenerateDelegateType(invokeReturnType, invokeParameterTypes);

			return Delegate.CreateDelegate(type, method.Target, method.Method, true);
		}
	}
}

