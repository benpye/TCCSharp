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

		/// <summary>
		/// Generates a delegate type at runtime given the return and parameter types.
		/// </summary>
		/// <returns>The Delegate type.</returns>
		/// <param name="returnType">Return type.</param>
		/// <param name="parameterTypes">Parameter types.</param>
		public static Type GetDelegateType(Type returnType, Type[] parameterTypes)
		{
			TypeBuilder tb = moduleBuilder.DefineType("DelegateWrapperDelegate" + Guid.NewGuid(),
				TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoClass, typeof(MulticastDelegate));

			var constructor = tb.DefineConstructor(MethodAttributes.RTSpecialName |
				MethodAttributes.Public | MethodAttributes.HideBySig,
				CallingConventions.Standard, new Type[] { typeof(object), typeof(IntPtr) });
			constructor.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

			var invoke = tb.DefineMethod("Invoke", MethodAttributes.Public |
				MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig,
				returnType, parameterTypes);
			invoke.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

			ConstructorInfo attributeCi = typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new Type[] { typeof(CallingConvention) });
			CustomAttributeBuilder cab = new CustomAttributeBuilder(attributeCi, new object[] { CallingConvention.Cdecl });
			tb.SetCustomAttribute(cab);

			return tb.CreateType();
		}

		/// <summary>
		/// Gets the return and parameter types from a given delegate.
		/// </summary>
		/// <param name="delegateType">Delegate type.</param>
		/// <param name="returnType">Return type.</param>
		/// <param name="parameterTypes">Parameter types.</param>
		public static void GetInvokeInfo(Type delegateType, out Type returnType, out Type[] parameterTypes)
		{
			MethodInfo invokeInfo = delegateType.GetMethod("Invoke");
			returnType = invokeInfo.ReturnType;
			ParameterInfo[] parameters = invokeInfo.GetParameters();
			parameterTypes = new Type[parameters.Length];

			for (int i = 0; i < parameters.Length; i++)
			{
				parameterTypes[i] = parameters[i].ParameterType;
			}
		}

		/// <summary>
		/// Gets an equivilent delegate type, generating it at runtime.
		/// This will also convert a generic delegate to a non generic Delegate
		/// suitable for P/Invoke.
		/// </summary>
		/// <returns>The new delegate type.</returns>
		/// <param name="delegateType">Delegate type.</param>
		public static Type GetStaticDelegateType(Type delegateType)
		{
			Type returnType;
			Type[] parameterTypes;
			GetInvokeInfo(delegateType, out returnType, out parameterTypes);
			return GetDelegateType(returnType, parameterTypes);
		}

		/// <summary>
		/// Wraps a Delegate with a new delegate type, used to P/Invoke
		/// delegates with Action<> or Func<> delegates.
		/// </summary>
		/// <returns>The wrapped delegate.</returns>
		/// <param name="method">Method.</param>
		/// <param name="type">Type.</param>
		public static Delegate GetWrappedDelegate(Delegate method, Type type = null)
		{
			if(type == null)
			{
				Type delegateType = method.GetType();
				type = GetStaticDelegateType(delegateType);
			}

			return Delegate.CreateDelegate(type, method.Target, method.Method, true);
		}

		/// <summary>
		/// Builds a delegate that will call a native method,
		/// </summary>
		/// <returns>The delegate.</returns>
		/// <param name="nativePointer">Native function pointer.</param>
		/// <param name="delegateType">Delegate type.</param>
		public static Delegate GetCalliDelegate(IntPtr nativePointer, Type delegateType)
		{
			Type returnType;
			Type[] parameterTypes;
			GetInvokeInfo(delegateType, out returnType, out parameterTypes);

			// TODO: This sort of code is all over the place, it should be refactored
			DynamicMethod delegateMethod = new DynamicMethod(
				"DelegateWrapperGetCalliDelegate" + Guid.NewGuid(),
				returnType,
				parameterTypes,
				// Not 100% sure why we need to set the type here, but it does
				// make .NET work
				typeof(DelegateWrapper),
				true);

			ILGenerator il = delegateMethod.GetILGenerator();

			for (int i = 0; i < parameterTypes.Length; i++)
				il.Emit(OpCodes.Ldarg, i);

			switch (IntPtr.Size)
			{
				case 4:
					il.Emit(OpCodes.Ldc_I4, nativePointer.ToInt32());
					break;
				case 8:
					il.Emit(OpCodes.Ldc_I8, nativePointer.ToInt64());
					break;
				default:
					throw new PlatformNotSupportedException();
			}

			il.EmitCalli(OpCodes.Calli, CallingConvention.Cdecl, returnType, parameterTypes);

			il.Emit(OpCodes.Ret);

			return delegateMethod.CreateDelegate(delegateType);
		}
	}
}

