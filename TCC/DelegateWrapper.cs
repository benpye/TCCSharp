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

			TypeBuilder tb = moduleBuilder.DefineType("DelegateWrapperDelegate" + method.Method.Name + Guid.NewGuid(),
				TypeAttributes.Public | TypeAttributes.Sealed, typeof(MulticastDelegate));

			var constructor = tb.DefineConstructor(MethodAttributes.RTSpecialName |
				MethodAttributes.SpecialName | MethodAttributes.Public | MethodAttributes.HideBySig,
				CallingConventions.Standard, new Type[] { typeof(object), typeof(IntPtr) });
			constructor.SetImplementationFlags(MethodImplAttributes.Runtime);

			var invoke = tb.DefineMethod("Invoke", MethodAttributes.Public |
				MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig,
				CallingConventions.Standard, invokeReturnType, null,
				new Type[]{ typeof(CallConvCdecl) },
				invokeParameterTypes, null, null);
			invoke.SetImplementationFlags(MethodImplAttributes.Runtime);

			return Delegate.CreateDelegate(tb.CreateType(), method.Target, method.Method, true);
		}
	}
}

