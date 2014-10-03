using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TCC
{
	internal static class ILHelper
	{
		/// <summary>
		/// Gets the GCHandle struct for the top IntPtr on the stack.
		/// </summary>
		/// <param name="il">ILGenerator.</param>
		public static void GetGCHandle(this ILGenerator il)
		{
			var lb = il.DeclareLocal(typeof(GCHandle));
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("FromIntPtr"));
			il.Emit(OpCodes.Stloc, lb.LocalIndex);
			il.Emit(OpCodes.Ldloca_S, lb.LocalIndex);
		}

		/// <summary>
		/// Gets the class instance for the top IntPtr on the stack.
		/// </summary>
		/// <param name="il">ILGenerator.</param>
		/// <param name="klass">Class type.</param>
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

		/// <summary>
		/// Wraps the top instance of a class on the stack with a GCHandle, leaving the IntPtr.
		/// </summary>
		/// <param name="il">ILGenerator.</param>
		/// <param name="klass">Class.</param>
		public static void WrapClass(this ILGenerator il, Type klass)
		{
			if (klass.IsValueType)
				il.Emit(OpCodes.Box, klass);
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("Alloc", new Type[]{ typeof(object) }));
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("ToIntPtr"));
		}

		/// <summary>
		/// Marshals the arguments from the current function call, pushing them to the stack
		/// for another function call. Unwraps classes from GCHandle IntPtrs.
		/// </summary>
		/// <param name="il">ILGenerator.</param>
		/// <param name="klass">Class type.</param>
		/// <param name="isStatic">If set to <c>true</c>, klass is static.</param>
		/// <param name="marshalTypes">List of argument types and boolean specifying if wrapped in GCHandle.</param>
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

