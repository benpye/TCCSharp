using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace TCC
{
	internal static class ILHelper
	{
		public static void GetGCHandle(this ILGenerator il)
		{
			il.DeclareLocal(typeof(GCHandle));
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("FromIntPtr"));
			il.Emit(OpCodes.Stloc_0);
			il.Emit(OpCodes.Ldloca_S, 0);
		}

		public static void GetClass(this ILGenerator il, Type klass)
		{
			il.GetGCHandle();
			il.Emit(OpCodes.Call, typeof(GCHandle).GetProperty("Target").GetGetMethod());
			if(klass.IsValueType)
				il.Emit(OpCodes.Unbox, klass);
		}
	}
}

