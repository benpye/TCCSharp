using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TCC
{
	internal static class ILHelper
	{
		public static void GetGCHandle(this ILGenerator il)
		{
			var lb = il.DeclareLocal(typeof(GCHandle));
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("FromIntPtr"));
			il.Emit(OpCodes.Stloc, lb.LocalIndex);
			il.Emit(OpCodes.Ldloca_S, lb.LocalIndex);
		}

		public static void GetClass(this ILGenerator il, Type klass)
		{
			il.GetGCHandle();
			il.Emit(OpCodes.Call, typeof(GCHandle).GetProperty("Target").GetGetMethod());
			il.Emit(OpCodes.Unbox_Any, klass);
			if (klass.IsValueType)
			{
				var lb = il.DeclareLocal(klass);
				il.Emit(OpCodes.Stloc, lb.LocalIndex);
				il.Emit(OpCodes.Ldloca_S, lb.LocalIndex);
			}
		}

		public static void WrapClass(this ILGenerator il, Type klass)
		{
			if (klass.IsValueType)
				il.Emit(OpCodes.Box, klass);
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("Alloc", new Type[]{ typeof(object) }));
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("ToIntPtr"));
		}

		public static void MarshalMethodArgs(this ILGenerator il, Type klass, bool isStatic, List<Tuple<Type, bool>> marshalTypes)
		{
			int argc = 0;

			if (!isStatic)
			{
				il.Emit(OpCodes.Ldarg_0);
				il.GetClass(klass);
				argc++;
			}

			foreach (var t in marshalTypes)
			{
				il.Emit(OpCodes.Ldarg, argc);
				argc++;
				if (t.Item2)
				{
					il.GetClass(t.Item1);
				}
			}
		}
	}
}

