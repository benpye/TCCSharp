﻿using System;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace TCC
{
	public class Binder
	{
		private CC compiler;

		// Array of types that we can marshal directly, otherwise we use handles
		private Type[] simpleTypes = {
			typeof(IntPtr),
			typeof(Byte),
			typeof(Int16),
			typeof(UInt16),
			typeof(Int32),
			typeof(UInt32),
			typeof(Char),
			typeof(String),
			typeof(StringBuilder),
			typeof(Single),
			typeof(Double)
		};

		public Binder(CC tcc)
		{
			compiler = tcc;

			// Generic GC free
			compiler.AddSymbolDynamic("gc_free", GenerateGCFree());
		}

		public void BindClass(Type klass)
		{
			var klassName = klass.Name.ToLower();

			// Properties
			var props = klass.GetProperties();
			foreach(var prop in props)
			{
				var propName = prop.Name.ToLower();

				if(prop.GetGetMethod().IsPublic)
					compiler.AddSymbolDynamic(klassName + "_get_" + propName, GeneratePropertyGetter(klass, prop));

				if(prop.GetSetMethod().IsPublic)
					compiler.AddSymbolDynamic(klassName + "_set_" + propName, GeneratePropertySetter(klass, prop));
			}
		}

		private Delegate GeneratePropertyGetter(Type klass, PropertyInfo property)
		{
			bool isStatic = property.GetGetMethod().IsStatic;

			Type[] parameterTypes = isStatic ? new Type[] { } : new Type[] { typeof(IntPtr) };
			Type returnType = property.PropertyType;

			DynamicMethod propertyGetter = new DynamicMethod(
				"TCC" + klass.Name + property.Name + "Getter",
				returnType,
				parameterTypes);

			ILGenerator il = propertyGetter.GetILGenerator();

			if(!isStatic)
				il.GetClass(klass);

			il.Emit(OpCodes.Callvirt, property.GetGetMethod());
			il.Emit(OpCodes.Ret);

			Type getterFunc = DelegateWrapper.GenerateDelegateType(returnType, parameterTypes);

			return propertyGetter.CreateDelegate(getterFunc);
		}

		private Delegate GeneratePropertySetter(Type klass, PropertyInfo property)
		{
			bool isStatic = property.GetGetMethod().IsStatic;

			Type[] parameterTypes = isStatic ? new Type[] { property.PropertyType } : new Type[] { typeof(IntPtr), property.PropertyType };
			Type returnType = typeof(void);

			DynamicMethod propertySetter = new DynamicMethod(
				"TCC" + klass.Name + property.Name + "Setter",
				returnType,
				parameterTypes);

			ILGenerator il = propertySetter.GetILGenerator();

			if(!isStatic)
			{
				il.GetClass(klass);
				il.Emit(OpCodes.Ldarg_1);
			}
			else
				il.Emit(OpCodes.Ldarg_0);

			il.Emit(OpCodes.Callvirt, property.GetSetMethod());
			il.Emit(OpCodes.Ret);

			Type setterFunc = DelegateWrapper.GenerateDelegateType(returnType, parameterTypes);

			return propertySetter.CreateDelegate(setterFunc);
		}

		private Delegate GenerateGCFree()
		{
			Type[] parameterTypes = new Type[] { typeof(IntPtr) };
			Type returnType = typeof(void);

			DynamicMethod gcFree = new DynamicMethod(
				"TCCGCFree",
				returnType,
				parameterTypes);

			ILGenerator il = gcFree.GetILGenerator();
			il.GetGCHandle();
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("Free"));
			il.Emit(OpCodes.Ret);

			Type freeFunc = DelegateWrapper.GenerateDelegateType(returnType, parameterTypes);

			return gcFree.CreateDelegate(freeFunc);
		}
	}
}

