using System;
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
			compiler.AddSymbol("gc_free", GenerateGCFree());
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
					compiler.AddSymbol(klassName + "_get_" + propName, GeneratePropertyGetter(klass, prop));

				if(prop.GetSetMethod().IsPublic)
					compiler.AddSymbol(klassName + "_set_" + propName, GeneratePropertySetter(klass, prop));
			}
		}

		private Delegate GeneratePropertyGetter(Type klass, PropertyInfo property)
		{
			bool isStatic = property.GetGetMethod().IsStatic;

			DynamicMethod propertyGetter = new DynamicMethod(
				"TCC" + klass.Name + property.Name + "Getter",
				property.PropertyType,
				isStatic ? new Type[] { } : new Type[]{ typeof(IntPtr) });

			ILGenerator il = propertyGetter.GetILGenerator();

			if(!isStatic)
				il.GetClass(klass);

			il.Emit(OpCodes.Callvirt, property.GetGetMethod());
			il.Emit(OpCodes.Ret);

			Type getterFunc = isStatic ? typeof(Func<>) : typeof(Func<,>);

			Type genericDelegate;
			if(isStatic)
				genericDelegate = getterFunc.MakeGenericType(property.PropertyType);
			else
				genericDelegate = getterFunc.MakeGenericType(typeof(IntPtr), property.PropertyType);

			return propertyGetter.CreateDelegate(genericDelegate);
		}

		private Delegate GeneratePropertySetter(Type klass, PropertyInfo property)
		{
			bool isStatic = property.GetGetMethod().IsStatic;

			DynamicMethod propertySetter = new DynamicMethod(
				"TCC" + klass.Name + property.Name + "Setter",
				typeof(void),
				isStatic ? new Type[] { property.PropertyType } : new Type[]{ typeof(IntPtr), property.PropertyType });

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

			Type setterFunc = isStatic ? typeof(Action<>) : typeof(Action<,>);
			Type genericDelegate;
			if(isStatic)
				genericDelegate = setterFunc.MakeGenericType(property.PropertyType);
			else
				genericDelegate = setterFunc.MakeGenericType(typeof(IntPtr), property.PropertyType);

			return propertySetter.CreateDelegate(genericDelegate);
		}

		private Delegate GenerateGCFree()
		{
			DynamicMethod gcFree = new DynamicMethod(
				"TCCGCFree",
				typeof(void),
				new Type[]{ typeof(IntPtr) });

			ILGenerator il = gcFree.GetILGenerator();
			il.GetGCHandle();
			il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod("Free"));
			il.Emit(OpCodes.Ret);

			Type freeFunc = typeof(Action<>);
			Type genericDelegate = freeFunc.MakeGenericType(typeof(IntPtr));

			return gcFree.CreateDelegate(genericDelegate);
		}
	}
}

